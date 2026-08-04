using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;
using System.Linq;
using TownsPeople.Data;
using TownsPeople.GamePlay;
using TownsPeople.UI;

namespace TownsPeople.CustomEditor
{
    // NOTE: This wizard automates the creation of modular NPC prefabs.
    // The CanvasGroup hierarchy has been permanently patched, and UI font sizing is now fully exposed for Asset Store distribution.
    //
    // v25: The per-NPC AudioSource previously had no spatial configuration at all — now
    // configured as a real 3D spatial source at generation time.
    //
    // v26: New NPCs previously always spawned at world origin (0,0,0) — now optionally
    // positioned at whatever point the Scene view camera is currently looking at.
    //
    // v27: Every full NPC (Common/Vendor/QuestGiver) now automatically gets an
    // NPCWitnessReaction component, defaulting to Present Rumor mode.
    //
    // v28: Generated NPC GameObject naming changed from "NPC_AssetStore_<name>" to
    // "TownsPeople_NPC_<name>".
    //
    // v29: Locomotion add-on integration. Every reference to Locomotion-specific types
    // (LocomotionAgent, LocomotionRootMotionRelay) goes through Type.GetType() reflection
    // rather than a direct compile-time reference — Locomotion is a separate, optional add-on
    // that will not be present in every buyer's project, and this file must compile and
    // function correctly whether or not it's installed. When present and "Include Locomotion"
    // is checked, generated NPCs get LocomotionAgent (wired) and LocomotionRootMotionRelay (on
    // the mesh child), with per-pose playback rates auto-synced from a Blend Tree state named
    // "Locomotion" if one exists, and NPCAnimationBridge's Animation Layer Index defaulted to 1
    // to match Locomotion's recommended 2-layer Animator setup. Available for every NPC
    // variant, including Non-Dialogue — Locomotion has no dependency on NPCGossipMemory.
    //
    // v30 FIX: Animation Layer Index alone wasn't enough — NPCAnimationBridge's Default Idle
    // States previously kept its component-level default ("Idle_Neutral", a real clip). On the
    // Reactions layer (Override blending), reverting to a real clip permanently masks the Base
    // Layer's Locomotion Blend Tree the first time any reactive animation completes. Now
    // auto-set to "Empty" when Locomotion is included, IF a state named "Empty" is actually
    // found in the controller — otherwise left unchanged with a console warning.
    //
    // v31 FIX: Unity's default Animator Culling Mode can stop animation evaluation entirely
    // whenever a character's renderer isn't currently considered visible to any camera —
    // everything else (transform, dialogue, movement) keeps working regardless, which shows up
    // as "animation frozen, but interactable/still moving." Affects every NPC, independent of
    // Locomotion. Now set to Always Animate on every generated NPC automatically.

    /// <summary>
    /// Professional asset store pipeline wizard window that dynamically scans project assemblies.
    /// Perfectly synchronized with TownsPeople.Data to auto-seed configurations into new profiles.
    /// </summary>
    public class NPCCreatorWizardWindow : EditorWindow
    {
        // --- Core Generation Properties ---
        private string _npcName = "New Citizen";
        private GameObject _meshModel = null;
        private AnimationRigType _rigType = AnimationRigType.Humanoid;

        private enum NpcVariantType { Common_NPC, Vendor_NPC, QuestGiver_NPC, NonDialogue_NPC }
        private NpcVariantType _selectedVariant = NpcVariantType.Common_NPC;

        // --- Output Settings ---
        [Tooltip("Where generated NPC profile assets are saved. Created automatically (including subfolders) if it doesn't exist.")]
        private string _outputFolderPath = "Assets/NPC Creator/Generated Profiles";

        // v26: Spawn placement.
        [Tooltip("If enabled, the new NPC spawns at whatever point the Scene view camera is currently looking at, instead of world origin (0,0,0).")]
        private bool _spawnAtSceneCameraFocus = true;
        [Tooltip("Used only if the Scene view camera's look-ray doesn't hit any collider (e.g. aimed at open sky) — the NPC spawns this many world units in front of the camera instead.")]
        private float _spawnFallbackDistance = 5f;
        private const float SpawnRaycastMaxDistance = 1000f;

        // --- Editable UI & Prompt Parameters ---
        private string _promptTextString = "Talk [E]";
        private float _canvasHeightOffset = 2.2f;
        private float _dialogueFontSize = 10f;

        private Vector2 _canvasDimensions = new Vector2(150f, 35f);
        private Color _promptBgColor = new Color(0.1f, 0.1f, 0.1f, 0.85f);
        private Color _speechBgColor = new Color(0.15f, 0.15f, 0.15f, 0.90f);
        private Color _shopBgColor = new Color(0.15f, 0.15f, 0.15f, 0.95f);

        private bool _hasVendorAddon = false;
        private bool _hasQuestAddon = false;
        private bool _hasLocomotionAddon = false;

        [Tooltip("Adds LocomotionAgent (NavMesh-driven movement, Walk/Run speed tiers, turn anticipation) and LocomotionRootMotionRelay to this NPC, fully wired. Also defaults NPCAnimationBridge's Animation Layer Index to 1, matching Locomotion's recommended 2-layer Animator setup, and — if that setup is detected — sets Default Idle States to \"Empty\". If a Blend Tree state named exactly \"Locomotion\" exists in the shared Animator Controller, its per-pose playback rates are auto-synced too. Only shown if the Locomotion add-on is detected in the project.")]
        private bool _includeLocomotion = false;

        [Tooltip("If enabled, this NPC gets NPCRumorIndicator — an optional visual debug tool that spawns a small colored sphere above its head for each rumor it currently knows.")]
        private bool _includeRumorIndicator = false;

        [Tooltip("Optional faction this NPC belongs to, used by its NPCReputationOpinion for faction-aware effective reputation. Leave empty to use only general reputation.")]
        private string _factionId = "";

        [Tooltip("Which gendered voice line this NPC uses for rumor/response audio that provides both a Male and Female clip.")]
        private TownsPeople.Data.VoiceGender _voiceGender = TownsPeople.Data.VoiceGender.Male;

        [Tooltip("Shared library of generic Positive/Negative reactions, used by full NPCs as their rumor fallback and by Non-Dialogue NPCs as their only response source.")]
        private TownsPeople.Data.GeneralRumorResponseLibrary _responseLibrary;

        private Vector2 _scrollPosition;

        [Tooltip("Local position offset (within the NPC's worldspace UI canvas) where the speech bubble appears.")]
        private Vector3 _speechBubbleOffset = new Vector3(0f, 80f, 0f);

        [Tooltip("Local position offset (within the NPC's worldspace UI canvas) where the nameplate appears.")]
        private Vector3 _nameplateOffset = new Vector3(0f, 15f, 0f);

        [Header("[E] Prompt Visual Style")]
        private float _promptCornerRadius = 10f;
        private float _promptBorderThickness = 2f;
        private Color _promptBorderColor = new Color(0.80f, 0.66f, 0.32f, 1f);
        private Color _promptFillTop = new Color(0.16f, 0.16f, 0.16f, 0.9f);
        private Color _promptFillBottom = new Color(0.06f, 0.06f, 0.06f, 0.9f);

        [Tooltip("Full volume within this distance (world units) from the NPC. Below this, the player hears rumor/greeting/response audio at 100% regardless of exact distance.")]
        private float _npcAudioMinDistance = 2f;

        [Tooltip("Beyond this distance (world units), the NPC's audio is inaudible.")]
        private float _npcAudioMaxDistance = 15f;

        [Tooltip("How volume falls off between Min and Max Distance.")]
        private AudioRolloffMode _npcAudioRolloffMode = AudioRolloffMode.Logarithmic;

        private const string UiOutputFolder = "Assets/NPC Creator/Generated UI";

        private bool _generateButtonPressed;
        private Texture2D _btnNormalTex;
        private Texture2D _btnHoverTex;
        private Texture2D _btnActiveTex;
        private Texture2D _btnDisabledTex;
        private GUIStyle _generateButtonLabelStyle;

        [MenuItem("Tools/TownsPeople/NPC Creator")]
        public static void ShowWindow()
        {
            NPCCreatorWizardWindow window = GetWindow<NPCCreatorWizardWindow>("NPC Creator Wizard");
            window.minSize = new Vector2(480, 700);
            window.Show();
        }

        private void OnEnable()
        {
            _hasVendorAddon = Type.GetType("TownsPeople.GamePlay.VendorComponentAddon") != null;
            _hasQuestAddon = Type.GetType("TownsPeople.GamePlay.QuestComponentAddon") != null;
            _hasLocomotionAddon = Type.GetType("TownsPeople.GamePlay.LocomotionAgent") != null;
        }

        // --- Editor GUI Layout ---

        private void OnGUI()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            GUILayout.Label("NPC Creator Pipeline Wizard", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Automated multi-layer UI creation pipeline for Asset Store deployment.", EditorStyles.miniLabel);
            EditorGUILayout.Space();

            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("Core Identity Parameters", EditorStyles.boldLabel);
            _npcName = EditorGUILayout.TextField("NPC Display Name", _npcName);
            _meshModel = (GameObject)EditorGUILayout.ObjectField("3D Mesh Asset / Prefab", _meshModel, typeof(GameObject), false);
            _rigType = (AnimationRigType)EditorGUILayout.EnumPopup("Animation Rig Setup", _rigType);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();

            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("Spawn Placement", EditorStyles.boldLabel);
            _spawnAtSceneCameraFocus = EditorGUILayout.ToggleLeft(
                new GUIContent("Spawn At Scene View Camera Focus", "Spawns the new NPC wherever the Scene view camera is currently looking, instead of world origin (0,0,0)."),
                _spawnAtSceneCameraFocus);
            GUI.enabled = _spawnAtSceneCameraFocus;
            _spawnFallbackDistance = EditorGUILayout.FloatField(
                new GUIContent("Fallback Distance", "Used only if the camera's look-ray doesn't hit any collider — spawns this many units in front of the camera instead."),
                _spawnFallbackDistance);
            GUI.enabled = true;
            EditorGUILayout.HelpBox("Requires an active Scene view with colliders in it to land precisely. If disabled, or no Scene view is open, the NPC spawns at world origin as before.", MessageType.None);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();

            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("Output Settings", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            _outputFolderPath = EditorGUILayout.TextField("Profile Output Folder", _outputFolderPath);
            if (GUILayout.Button("Browse...", GUILayout.Width(70)))
            {
                string selected = EditorUtility.OpenFolderPanel("Select Output Folder", "Assets", "");
                if (!string.IsNullOrEmpty(selected))
                {
                    string relativePath = ConvertToProjectRelativePath(selected);
                    if (relativePath != null)
                    {
                        _outputFolderPath = relativePath;
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("Invalid Folder", "Please choose a folder inside this project's Assets directory.", "OK");
                    }
                }
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.HelpBox("This folder will be created automatically (including subfolders) if it doesn't already exist.", MessageType.None);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();

            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("Entity Role Archetype Selection", EditorStyles.boldLabel);
            _selectedVariant = (NpcVariantType)EditorGUILayout.EnumPopup("Target NPC Variant Role", _selectedVariant);

            if (_selectedVariant == NpcVariantType.Vendor_NPC && !_hasVendorAddon)
            {
                EditorGUILayout.HelpBox("⚠️ Vendor NPC selection locked! 'VendorComponentAddon.cs' was not detected.", MessageType.Warning);
                _selectedVariant = NpcVariantType.Common_NPC;
            }
            if (_selectedVariant == NpcVariantType.QuestGiver_NPC && !_hasQuestAddon)
            {
                EditorGUILayout.HelpBox("⚠️ Quest Giver NPC selection locked! 'QuestComponentAddon.cs' was not detected.", MessageType.Warning);
                _selectedVariant = NpcVariantType.Common_NPC;
            }
            if (_selectedVariant == NpcVariantType.NonDialogue_NPC)
            {
                EditorGUILayout.HelpBox("Skips the full rumor/gossip system entirely (no NPCGossipMemory, no rumor indicator, no witness reaction). Locomotion remains available if enabled below. Only ever greets the player with a reputation-driven Positive/Negative response from the General Response Library below.", MessageType.Info);
            }

            EditorGUILayout.BeginHorizontal();
            GUI.enabled = false;
            EditorGUILayout.Toggle("Vendor Pack Detected:", _hasVendorAddon);
            EditorGUILayout.Toggle("Quest Pack Detected:", _hasQuestAddon);
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();

            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("Visual Prompt & UI Canvas Formats", EditorStyles.boldLabel);

            float originalLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 220f;

            _promptTextString = EditorGUILayout.TextField("Interaction Prompt Text", _promptTextString);
            _canvasHeightOffset = EditorGUILayout.FloatField("Canvas Height Offset (3D)", _canvasHeightOffset);
            _dialogueFontSize = EditorGUILayout.FloatField("Speech Bubble Font Size", _dialogueFontSize);
            _speechBubbleOffset = EditorGUILayout.Vector3Field(
                new GUIContent("Speech Bubble Offset", "Local position where the speech bubble appears, relative to the prompt."),
                _speechBubbleOffset);
            _nameplateOffset = EditorGUILayout.Vector3Field(
                new GUIContent("Nameplate Offset", "Local position where the nameplate appears."),
                _nameplateOffset);

            _canvasDimensions = EditorGUILayout.Vector2Field("Master Canvas Dimensions", _canvasDimensions);

            EditorGUILayout.Space();
            _speechBgColor = EditorGUILayout.ColorField("Dialogue Speech Bubble BG Color", _speechBgColor);
            _shopBgColor = EditorGUILayout.ColorField("Merchant Menu Background Color", _shopBgColor);

            EditorGUIUtility.labelWidth = originalLabelWidth;
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();

            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("[E] Prompt Visual Style", EditorStyles.boldLabel);
            _promptCornerRadius = EditorGUILayout.Slider("Corner Radius", _promptCornerRadius, 0f, 24f);
            _promptBorderThickness = EditorGUILayout.Slider("Border Thickness", _promptBorderThickness, 0f, 8f);
            _promptBorderColor = EditorGUILayout.ColorField("Border Color", _promptBorderColor);
            _promptFillTop = EditorGUILayout.ColorField("Fill (Top)", _promptFillTop);
            _promptFillBottom = EditorGUILayout.ColorField("Fill (Bottom)", _promptFillBottom);
            EditorGUILayout.HelpBox("Shared across every generated NPC — regenerating any NPC after changing these values updates the look for all of them.", MessageType.None);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();

            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("NPC Audio Spatialization", EditorStyles.boldLabel);
            _npcAudioMinDistance = EditorGUILayout.FloatField(
                new GUIContent("Min Distance", "Full volume within this distance from the NPC."), _npcAudioMinDistance);
            _npcAudioMaxDistance = EditorGUILayout.FloatField(
                new GUIContent("Max Distance", "Beyond this distance, the NPC's audio is inaudible."), _npcAudioMaxDistance);
            _npcAudioRolloffMode = (AudioRolloffMode)EditorGUILayout.EnumPopup(
                new GUIContent("Rolloff Mode", "How volume falls off between Min and Max Distance."), _npcAudioRolloffMode);
            EditorGUILayout.HelpBox("Applies to newly generated NPCs only.", MessageType.None);
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("Debug & Visualization", EditorStyles.boldLabel);
            GUI.enabled = _selectedVariant != NpcVariantType.NonDialogue_NPC;
            _includeRumorIndicator = EditorGUILayout.ToggleLeft(
                new GUIContent("Include Rumor Indicator", "Spawns a small colored sphere above this NPC's head for each rumor it currently knows. Unavailable on Non-Dialogue NPCs."),
                _includeRumorIndicator);
            GUI.enabled = true;
            EditorGUILayout.HelpBox("NPCWitnessReaction is added automatically to every full NPC — defaulting to Present Rumor mode. Not added to Non-Dialogue NPCs.", MessageType.None);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();

            // v29: Locomotion — fully optional add-on. Toggle only shown/usable if the add-on
            // is actually present in the project (reflection-detected in OnEnable), same
            // pattern already used for Vendor/Quest.
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("Locomotion (Add-on)", EditorStyles.boldLabel);
            if (!_hasLocomotionAddon)
            {
                EditorGUILayout.HelpBox("Locomotion add-on not detected in this project — install it to enable NavMesh-driven wandering/route-walking for generated NPCs.", MessageType.Info);
                _includeLocomotion = false;
            }
            else
            {
                _includeLocomotion = EditorGUILayout.ToggleLeft(
                    new GUIContent("Include Locomotion", "Adds LocomotionAgent and LocomotionRootMotionRelay, fully wired. Also defaults this NPC's NPCAnimationBridge Animation Layer Index to 1 and (if found) Default Idle States to \"Empty\", to match Locomotion's recommended 2-layer Animator setup (Base Layer = Locomotion Blend Tree, Layer 1 = Reactions). Per-pose playback rates auto-sync if a Blend Tree state named exactly \"Locomotion\" exists — otherwise sync manually afterward via LocomotionAgent's own Inspector."),
                    _includeLocomotion);
                EditorGUILayout.HelpBox("Available for every NPC variant, including Non-Dialogue — Locomotion has no dependency on NPCGossipMemory/dialogue at all.", MessageType.None);
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();

            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("Responses", EditorStyles.boldLabel);
            _responseLibrary = (TownsPeople.Data.GeneralRumorResponseLibrary)EditorGUILayout.ObjectField(
                new GUIContent("General Response Library", "Required for Non-Dialogue NPCs; optional for full NPCs."),
                _responseLibrary, typeof(TownsPeople.Data.GeneralRumorResponseLibrary), false);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();

            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("Reputation", EditorStyles.boldLabel);
            _factionId = EditorGUILayout.TextField(
                new GUIContent("Faction ID (optional)", "Sets this NPC's NPCReputationOpinion faction."),
                _factionId);
            _voiceGender = (TownsPeople.Data.VoiceGender)EditorGUILayout.EnumPopup(
                new GUIContent("Voice Gender", "Which gendered voice line this NPC uses."),
                _voiceGender);
            EditorGUILayout.EndVertical();

            GUILayout.FlexibleSpace();

            bool canGenerate = _meshModel != null;
            if (!canGenerate)
            {
                EditorGUILayout.HelpBox("Assign a 3D Mesh Asset / Prefab above before generating.", MessageType.Warning);
            }

            DrawGenerateButton($"GENERATE COMPLETE {_selectedVariant.ToString().ToUpper()}", canGenerate);

            EditorGUILayout.EndScrollView();
        }

        // --- 3D Pressable Button Rendering ---

        private void EnsureButtonAssets()
        {
            if (_btnNormalTex != null) return;

            _btnNormalTex = MakeGradientTexture(new Color(0.35f, 0.72f, 0.35f, 1f), new Color(0.22f, 0.52f, 0.22f, 1f));
            _btnHoverTex = MakeGradientTexture(new Color(0.42f, 0.80f, 0.42f, 1f), new Color(0.28f, 0.60f, 0.28f, 1f));
            _btnActiveTex = MakeGradientTexture(new Color(0.20f, 0.45f, 0.20f, 1f), new Color(0.16f, 0.36f, 0.16f, 1f));
            _btnDisabledTex = MakeGradientTexture(new Color(0.45f, 0.45f, 0.45f, 1f), new Color(0.32f, 0.32f, 0.32f, 1f));

            _generateButtonLabelStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
        }

        private static Texture2D MakeGradientTexture(Color top, Color bottom)
        {
            const int height = 16;
            Texture2D tex = new Texture2D(1, height)
            {
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            for (int y = 0; y < height; y++)
            {
                float t = y / (float)(height - 1);
                tex.SetPixel(0, y, Color.Lerp(bottom, top, t));
            }
            tex.Apply();

            return tex;
        }

        private void DrawGenerateButton(string label, bool enabled)
        {
            EnsureButtonAssets();

            Rect baseRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(45), GUILayout.ExpandWidth(true));
            Event evt = Event.current;
            bool isMouseOver = baseRect.Contains(evt.mousePosition);

            if (!enabled)
            {
                _generateButtonPressed = false;
            }
            else
            {
                if (evt.type == EventType.MouseDown && evt.button == 0 && isMouseOver)
                {
                    _generateButtonPressed = true;
                    evt.Use();
                }
                else if (evt.type == EventType.MouseUp && evt.button == 0)
                {
                    bool shouldGenerate = _generateButtonPressed && isMouseOver;
                    _generateButtonPressed = false;
                    evt.Use();

                    if (shouldGenerate)
                    {
                        EditorApplication.delayCall += ExecuteEntityGeneration;
                    }
                }
            }

            bool isPressed = enabled && _generateButtonPressed;

            if (!isPressed)
            {
                Rect shadowRect = baseRect;
                shadowRect.y += 3f;
                EditorGUI.DrawRect(shadowRect, new Color(0f, 0f, 0f, 0.35f));
            }

            Rect drawRect = baseRect;
            if (isPressed)
            {
                drawRect.y += 2f;
            }

            Texture2D texToUse = !enabled ? _btnDisabledTex : isPressed ? _btnActiveTex : isMouseOver ? _btnHoverTex : _btnNormalTex;
            GUI.DrawTexture(drawRect, texToUse, ScaleMode.StretchToFill);
            GUI.Label(drawRect, label, _generateButtonLabelStyle);

            if (isMouseOver || isPressed)
            {
                Repaint();
            }
        }

        private Vector3 ComputeSpawnPosition()
        {
            if (!_spawnAtSceneCameraFocus)
            {
                return Vector3.zero;
            }

            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null || sceneView.camera == null)
            {
                Debug.LogWarning("<color=orange>[NPC Creator Wizard]</color> 'Spawn At Scene View Camera Focus' is enabled, but no active Scene view was found — spawning at world origin instead.");
                return Vector3.zero;
            }

            Camera sceneCamera = sceneView.camera;
            Ray lookRay = new Ray(sceneCamera.transform.position, sceneCamera.transform.forward);

            if (Physics.Raycast(lookRay, out RaycastHit hit, SpawnRaycastMaxDistance))
            {
                return hit.point;
            }

            return lookRay.GetPoint(_spawnFallbackDistance);
        }

        private string ConvertToProjectRelativePath(string absolutePath)
        {
            string projectDataPath = Application.dataPath.Replace("\\", "/");
            string normalizedAbsolute = absolutePath.Replace("\\", "/");

            if (!normalizedAbsolute.StartsWith(projectDataPath))
            {
                return null;
            }

            string relative = "Assets" + normalizedAbsolute.Substring(projectDataPath.Length);
            return relative;
        }

        private static void EnsureFolderExists(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath)) return;

            string[] parts = folderPath.Split('/');
            string currentPath = parts[0];

            for (int i = 1; i < parts.Length; i++)
            {
                string nextPath = $"{currentPath}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(nextPath))
                {
                    AssetDatabase.CreateFolder(currentPath, parts[i]);
                }
                currentPath = nextPath;
            }
        }

        private string GetUniqueNpcName(string baseName)
        {
            NPCGossipMemory[] existingNpcs = UnityEngine.Object.FindObjectsByType<NPCGossipMemory>(
                FindObjectsInactive.Include);

            HashSet<string> existingNames = new HashSet<string>(
                existingNpcs.Select(npc => npc.NpcName),
                StringComparer.OrdinalIgnoreCase);

            if (!existingNames.Contains(baseName))
            {
                return baseName;
            }

            int suffix = 1;
            string candidate = $"{baseName}_{suffix}";
            while (existingNames.Contains(candidate))
            {
                suffix++;
                candidate = $"{baseName}_{suffix}";
            }

            return candidate;
        }

        private static int EnsureLayerExists(string layerName)
        {
            int existingIndex = LayerMask.NameToLayer(layerName);
            if (existingIndex != -1) return existingIndex;

            SerializedObject tagManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layersProp = tagManager.FindProperty("layers");

            for (int i = 8; i < layersProp.arraySize; i++)
            {
                SerializedProperty layerSlot = layersProp.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(layerSlot.stringValue))
                {
                    layerSlot.stringValue = layerName;
                    tagManager.ApplyModifiedProperties();
                    Debug.Log($"<color=green>[NPC Creator Wizard]</color> Created new layer '{layerName}' at index {i}.");
                    return i;
                }
            }

            Debug.LogWarning($"<color=orange>[NPC Creator Wizard]</color> No free layer slots available — could not create '{layerName}' layer.");
            return -1;
        }

        private static void SetLayerRecursively(GameObject target, int layer)
        {
            target.layer = layer;
            foreach (Transform child in target.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }

        // --- Core Generation Pipeline ---

        private void ExecuteEntityGeneration()
        {
            if (_meshModel == null)
            {
                EditorUtility.DisplayDialog("Creation Aborted", "Please assign a 3D Mesh asset target prefab before compiling.", "OK");
                return;
            }

            string resolvedName = GetUniqueNpcName(_npcName);

            GameObject rootInstance = new GameObject($"TownsPeople_NPC_{resolvedName}");
            Undo.RegisterCreatedObjectUndo(rootInstance, "Create Modular NPC Root");

            rootInstance.transform.position = ComputeSpawnPosition();

            int npcLayer = EnsureLayerExists("NPC");
            if (npcLayer != -1)
            {
                SetLayerRecursively(rootInstance, npcLayer);
            }

            GameObject meshInstance = (GameObject)PrefabUtility.InstantiatePrefab(_meshModel, rootInstance.transform);
            meshInstance.name = "Character_Visual_Mesh";
            meshInstance.transform.localPosition = Vector3.zero;
            meshInstance.transform.localRotation = Quaternion.identity;

            Animator characterAnimator = meshInstance.GetComponent<Animator>();
            if (characterAnimator == null)
            {
                characterAnimator = meshInstance.AddComponent<Animator>();
            }

            // v31 FIX: Unity's default Animator Culling Mode can stop animation evaluation
            // entirely whenever the character's renderer isn't currently considered visible to
            // any camera — everything else (transform, dialogue, movement) keeps working, so
            // this shows up specifically as "animation frozen, but interactable/still moving."
            // Discovered and fixed manually on one test NPC earlier this session, but never
            // added here — meaning every NPC generated since then (Locomotion or not) shipped
            // with the bug latent. Always Animate trades a small per-NPC CPU cost for
            // guaranteed-correct animation regardless of camera visibility.
            characterAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            string customAnimatorName = "NPC Animator";
            string[] foundGuids = AssetDatabase.FindAssets($"{customAnimatorName} t:RuntimeAnimatorController");

            if (foundGuids.Length > 0)
            {
                string matchedPath = AssetDatabase.GUIDToAssetPath(foundGuids[0]);
                RuntimeAnimatorController controllerAsset = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(matchedPath);
                if (controllerAsset != null)
                {
                    characterAnimator.runtimeAnimatorController = controllerAsset;
                    characterAnimator.applyRootMotion = false;
                }
            }
            else
            {
                Debug.LogWarning($"<color=orange>[Animator Notice]</color> Controller '{customAnimatorName}' not found. Assigning default fallback.");
            }

            SphereCollider proxCollider = rootInstance.AddComponent<SphereCollider>();
            proxCollider.isTrigger = true;
            proxCollider.radius = 4.0f;

            NPCGossipMemory localMemory = null;

            if (_selectedVariant != NpcVariantType.NonDialogue_NPC)
            {
                localMemory = rootInstance.AddComponent<NPCGossipMemory>();
                localMemory.NpcName = resolvedName;

                SerializedObject serializedMemory = new SerializedObject(localMemory);
                serializedMemory.FindProperty("_voiceGender").enumValueIndex = (int)_voiceGender;
                if (_responseLibrary != null)
                {
                    serializedMemory.FindProperty("_responseLibrary").objectReferenceValue = _responseLibrary;
                }
                serializedMemory.ApplyModifiedProperties();
            }
            else
            {
                NPCGreetingResponder greetingResponder = rootInstance.AddComponent<NPCGreetingResponder>();

                SerializedObject serializedGreeting = new SerializedObject(greetingResponder);
                serializedGreeting.FindProperty("_voiceGender").enumValueIndex = (int)_voiceGender;
                if (_responseLibrary != null)
                {
                    serializedGreeting.FindProperty("_responseLibrary").objectReferenceValue = _responseLibrary;
                }
                serializedGreeting.ApplyModifiedProperties();
            }

            NPCProximityGossip proximityLogic = rootInstance.AddComponent<NPCProximityGossip>();

            AudioSource npcAudioSource = rootInstance.AddComponent<AudioSource>();
            npcAudioSource.spatialBlend = 1f;
            npcAudioSource.rolloffMode = _npcAudioRolloffMode;
            npcAudioSource.minDistance = _npcAudioMinDistance;
            npcAudioSource.maxDistance = _npcAudioMaxDistance;
            npcAudioSource.playOnAwake = false;

            NPCAnimationBridge animBridge = rootInstance.AddComponent<NPCAnimationBridge>();
            SerializedObject serializedAnimBridge = new SerializedObject(animBridge);
            serializedAnimBridge.FindProperty("_animator").objectReferenceValue = characterAnimator;

            // v29: Locomotion's recommended Animator setup splits reactive animations onto a
            // dedicated upper layer (index 1, Override blend, Empty default state) separate
            // from the Locomotion Blend Tree on the Base Layer — default this NPC's reactive-
            // animation layer to match when Locomotion is included.
            if (_includeLocomotion && _hasLocomotionAddon)
            {
                serializedAnimBridge.FindProperty("_animationLayerIndex").intValue = 1;

                // v30 FIX: Animation Layer Index alone isn't enough — Default Idle States
                // previously kept its component-level default ("Idle_Neutral", a real clip).
                // On the Reactions layer (Override blending), reverting to a real clip
                // PERMANENTLY masks the Base Layer's Locomotion Blend Tree the first time any
                // reactive animation completes. The Reactions layer's own Empty default state
                // is what should be reverted to instead. Only applied if an "Empty" state is
                // actually found in the controller — otherwise left alone with a warning.
                AnimatorController controllerForIdleCheck = characterAnimator.runtimeAnimatorController as AnimatorController;
                bool foundEmptyState = controllerForIdleCheck != null && FindNamedState(controllerForIdleCheck, "Empty") != null;

                if (foundEmptyState)
                {
                    SerializedProperty idleStatesProp = serializedAnimBridge.FindProperty("_defaultIdleStates");
                    idleStatesProp.ClearArray();
                    idleStatesProp.InsertArrayElementAtIndex(0);
                    idleStatesProp.GetArrayElementAtIndex(0).stringValue = "Empty";
                }
                else
                {
                    Debug.LogWarning("<color=orange>[NPC Creator Wizard]</color> Locomotion was included, but no state named 'Empty' was found on the Reactions layer — Default Idle States was left unchanged. Reactive animations may freeze the Locomotion Blend Tree once triggered. See this project's Locomotion setup notes for creating the Reactions layer's Empty default state.");
                }
            }

            serializedAnimBridge.ApplyModifiedProperties();

            rootInstance.AddComponent<NpcAddonRegistry>();

            NPCReputationOpinion reputationOpinion = rootInstance.AddComponent<NPCReputationOpinion>();
            if (!string.IsNullOrEmpty(_factionId))
            {
                SerializedObject serializedOpinion = new SerializedObject(reputationOpinion);
                serializedOpinion.FindProperty("_factionId").stringValue = _factionId;
                serializedOpinion.ApplyModifiedProperties();
            }

            if (_includeRumorIndicator && _selectedVariant != NpcVariantType.NonDialogue_NPC)
            {
                rootInstance.AddComponent<NPCRumorIndicator>();
            }

            if (_selectedVariant != NpcVariantType.NonDialogue_NPC)
            {
                NPCWitnessReaction witnessReaction = rootInstance.AddComponent<NPCWitnessReaction>();
                SerializedObject serializedWitnessReaction = new SerializedObject(witnessReaction);
                serializedWitnessReaction.FindProperty("_animator").objectReferenceValue = characterAnimator;

                AnimatorController generatedControllerForWitness = characterAnimator.runtimeAnimatorController as AnimatorController;
                if (generatedControllerForWitness != null)
                {
                    List<string> allStates = NPCWitnessReaction.CollectAllStateNames(generatedControllerForWitness);
                    SerializedProperty statesProp = serializedWitnessReaction.FindProperty("_reactionAnimationStates");
                    statesProp.ClearArray();
                    for (int i = 0; i < allStates.Count; i++)
                    {
                        statesProp.InsertArrayElementAtIndex(i);
                        statesProp.GetArrayElementAtIndex(i).stringValue = allStates[i];
                    }
                }

                serializedWitnessReaction.ApplyModifiedProperties();
            }

            // v29: Locomotion — deliberately available for EVERY variant, including
            // Non-Dialogue, since Locomotion has zero dependency on NPCGossipMemory at all.
            // Every Locomotion type is resolved via Type.GetType() rather than a direct
            // reference, so this whole block is a complete no-op (and this file still compiles
            // cleanly) in a project that doesn't have the Locomotion add-on installed.
            if (_includeLocomotion && _hasLocomotionAddon)
            {
                Type locomotionAgentType = Type.GetType("TownsPeople.GamePlay.LocomotionAgent");
                Type locomotionRelayType = Type.GetType("TownsPeople.GamePlay.LocomotionRootMotionRelay");

                if (locomotionAgentType != null)
                {
                    Component locomotionAgentComponent = rootInstance.AddComponent(locomotionAgentType);
                    SerializedObject serializedLocomotion = new SerializedObject(locomotionAgentComponent);

                    SerializedProperty locomotionAnimatorProp = serializedLocomotion.FindProperty("_animator");
                    if (locomotionAnimatorProp != null)
                    {
                        locomotionAnimatorProp.objectReferenceValue = characterAnimator;
                    }

                    // Auto-sync per-pose playback rates from a Blend Tree state named exactly
                    // "Locomotion" (this project's established naming convention). Never
                    // guesses at an unrelated tree if that convention isn't followed — simply
                    // skipped, leaving the list empty for manual sync afterward.
                    AnimatorController generatedController = characterAnimator.runtimeAnimatorController as AnimatorController;
                    if (generatedController != null)
                    {
                        BlendTree locomotionTree = FindNamedBlendTree(generatedController, "Locomotion");
                        if (locomotionTree != null)
                        {
                            SerializedProperty posesProp = serializedLocomotion.FindProperty("_posePlaybackRates");
                            if (posesProp != null)
                            {
                                posesProp.ClearArray();

                                List<ChildMotion> sortedChildren = locomotionTree.children.OrderBy(c => c.threshold).ToList();
                                for (int i = 0; i < sortedChildren.Count; i++)
                                {
                                    posesProp.InsertArrayElementAtIndex(i);
                                    SerializedProperty entryProp = posesProp.GetArrayElementAtIndex(i);
                                    string motionName = sortedChildren[i].motion != null ? sortedChildren[i].motion.name : "(empty)";
                                    entryProp.FindPropertyRelative("MotionName").stringValue = motionName;
                                    entryProp.FindPropertyRelative("Threshold").floatValue = sortedChildren[i].threshold;
                                    entryProp.FindPropertyRelative("Multiplier").floatValue = 1f;
                                }
                            }
                        }
                    }

                    serializedLocomotion.ApplyModifiedProperties();
                }

                if (locomotionRelayType != null)
                {
                    // Lives on the mesh child, not the NPC root — OnAnimatorMove() only fires
                    // on the same GameObject as the Animator component.
                    meshInstance.AddComponent(locomotionRelayType);
                }
            }

            // Step 4: Automated Master Worldspace UI Canvas
            GameObject canvasObj = new GameObject("NPC_Worldspace_UI_Canvas");
            canvasObj.transform.SetParent(rootInstance.transform);
            canvasObj.transform.localPosition = new Vector3(0, _canvasHeightOffset, 0);
            canvasObj.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            Canvas uiCanvas = canvasObj.AddComponent<Canvas>();
            uiCanvas.renderMode = RenderMode.WorldSpace;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();

            RectTransform canvasRect = canvasObj.GetComponent<RectTransform>();
            canvasRect.sizeDelta = _canvasDimensions;
            canvasRect.localScale = new Vector3(0.01f, 0.01f, 0.01f);

            canvasObj.AddComponent<Billboard>();

            GameObject nameplateObj = new GameObject("NPC_Nameplate");
            nameplateObj.transform.SetParent(canvasObj.transform, false);

            nameplateObj.AddComponent<CanvasGroup>();
            CanvasGroupFader nameplateFader = nameplateObj.AddComponent<CanvasGroupFader>();

            TextMeshProUGUI nameplateText = nameplateObj.AddComponent<TextMeshProUGUI>();
            nameplateText.alignment = TextAlignmentOptions.Center;
            nameplateText.fontSize = 12f;
            nameplateText.fontStyle = FontStyles.Bold;
            nameplateText.text = resolvedName;
            nameplateText.outlineWidth = 0.2f;
            nameplateText.outlineColor = Color.black;

            RectTransform nameplateRect = nameplateObj.GetComponent<RectTransform>();
            nameplateRect.sizeDelta = new Vector2(200f, 40f);

            NPCNameplate nameplateComponent = rootInstance.AddComponent<NPCNameplate>();
            SerializedObject serializedNameplate = new SerializedObject(nameplateComponent);
            serializedNameplate.FindProperty("_fader").objectReferenceValue = nameplateFader;
            serializedNameplate.FindProperty("_positionOffset").vector3Value = _nameplateOffset;
            serializedNameplate.ApplyModifiedProperties();

            GameObject promptBackground = new GameObject("Graphic_Placeholder_Background");
            promptBackground.transform.SetParent(canvasObj.transform, false);

            CanvasGroup promptCanvasGroup = promptBackground.AddComponent<CanvasGroup>();
            CanvasGroupFader promptFader = promptBackground.AddComponent<CanvasGroupFader>();

            Image promptBgImage = promptBackground.AddComponent<Image>();
            EnsureFolderExists(UiOutputFolder);
            Sprite promptSprite = ProceduralUISprites.CreateRoundedRectSprite(
                $"{UiOutputFolder}/PromptBackground.png", 64, _promptCornerRadius, _promptBorderThickness, _promptBorderColor, _promptFillTop, _promptFillBottom);
            promptBgImage.sprite = promptSprite;
            promptBgImage.type = Image.Type.Sliced;
            promptBgImage.color = Color.white;

            RectTransform promptBgRect = promptBackground.GetComponent<RectTransform>();
            promptBgRect.anchorMin = new Vector2(0f, 0f);
            promptBgRect.anchorMax = new Vector2(1f, 1f);
            promptBgRect.offsetMin = new Vector2(0f, -69f);
            promptBgRect.offsetMax = new Vector2(0f, -80f);
            promptBgRect.anchoredPosition3D = new Vector3(promptBgRect.anchoredPosition.x, promptBgRect.anchoredPosition.y, -25f);

            GameObject promptTextObj = new GameObject("Interaction_Prompt_Text");
            promptTextObj.transform.SetParent(promptBackground.transform, false);
            TextMeshProUGUI promptTmp = promptTextObj.AddComponent<TextMeshProUGUI>();
            promptTmp.alignment = TextAlignmentOptions.Center;
            promptTmp.fontSize = 10f;
            promptTmp.fontStyle = FontStyles.Bold;
            promptTmp.text = _promptTextString;

            RectTransform promptTextRect = promptTextObj.GetComponent<RectTransform>();
            promptTextRect.anchorMin = Vector2.zero;
            promptTextRect.anchorMax = Vector2.one;
            promptTextRect.sizeDelta = Vector2.zero;

            GameObject speechBubbleNode = new GameObject("NPC_Dialogue_Speech_Bubble");
            speechBubbleNode.transform.SetParent(canvasObj.transform, false);

            CanvasGroup speechCanvasGroup = speechBubbleNode.AddComponent<CanvasGroup>();
            NPCSpeechBubble speechBubble = speechBubbleNode.AddComponent<NPCSpeechBubble>();

            SerializedObject serializedSpeechBubble = new SerializedObject(speechBubble);
            serializedSpeechBubble.FindProperty("_positionOffset").vector3Value = _speechBubbleOffset;
            serializedSpeechBubble.ApplyModifiedProperties();

            GameObject speechBackground = new GameObject("Speech_Graphic_Plate_Background");
            speechBackground.transform.SetParent(speechBubbleNode.transform, false);
            Image speechBgImage = speechBackground.AddComponent<Image>();
            speechBgImage.color = _speechBgColor;

            RectTransform speechBgRect = speechBackground.GetComponent<RectTransform>();
            speechBgRect.sizeDelta = new Vector2(200f, 60f);

            GameObject dialogueTextObj = new GameObject("Dialogue_Speech_Text");
            dialogueTextObj.transform.SetParent(speechBackground.transform, false);
            TextMeshProUGUI dialogueTmp = dialogueTextObj.AddComponent<TextMeshProUGUI>();
            dialogueTmp.alignment = TextAlignmentOptions.Center;
            dialogueTmp.fontSize = _dialogueFontSize;
            dialogueTmp.text = "...";

            RectTransform dialogueTextRect = dialogueTextObj.GetComponent<RectTransform>();
            dialogueTextRect.anchorMin = Vector2.zero;
            dialogueTextRect.anchorMax = Vector2.one;
            dialogueTextRect.sizeDelta = Vector2.zero;

            SerializedObject serializedLogic = new SerializedObject(proximityLogic);
            serializedLogic.FindProperty("speechBubble").objectReferenceValue = speechBubble;
            serializedLogic.FindProperty("interactionPromptFader").objectReferenceValue = promptFader;
            serializedLogic.ApplyModifiedProperties();

            GameObject shopCanvasObj = new GameObject("NPC_Merchant_Market_Canvas");
            shopCanvasObj.transform.SetParent(rootInstance.transform);
            Canvas screenCanvas = shopCanvasObj.AddComponent<Canvas>();
            screenCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            shopCanvasObj.AddComponent<CanvasScaler>();
            shopCanvasObj.AddComponent<GraphicRaycaster>();
            shopCanvasObj.AddComponent<CanvasGroup>();
            NPCShopWindowUI shopUI = shopCanvasObj.AddComponent<NPCShopWindowUI>();

            GameObject marketFrame = new GameObject("Market_Frame_Background");
            marketFrame.transform.SetParent(shopCanvasObj.transform, false);
            Image marketImage = marketFrame.AddComponent<Image>();
            marketImage.color = _shopBgColor;
            RectTransform frameRect = marketFrame.GetComponent<RectTransform>();
            frameRect.anchorMin = new Vector2(0.2f, 0.2f);
            frameRect.anchorMax = new Vector2(0.8f, 0.8f);
            frameRect.sizeDelta = Vector2.zero;

            GameObject shopTitleObj = new GameObject("Shop_Title_Text");
            shopTitleObj.transform.SetParent(marketFrame.transform, false);
            TextMeshProUGUI titleTmp = shopTitleObj.AddComponent<TextMeshProUGUI>();
            titleTmp.text = "=== MARKETPLACE ===";
            titleTmp.alignment = TextAlignmentOptions.Top;
            titleTmp.fontSize = 32;
            RectTransform titleRect = shopTitleObj.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 0.8f);
            titleRect.anchorMax = new Vector2(1, 1);
            titleRect.sizeDelta = Vector2.zero;

            shopCanvasObj.SetActive(false);

            if (_selectedVariant == NpcVariantType.Vendor_NPC)
            {
                Type vendorType = Type.GetType("TownsPeople.GamePlay.VendorComponentAddon");
                if (vendorType != null) rootInstance.AddComponent(vendorType);
            }
            else if (_selectedVariant == NpcVariantType.QuestGiver_NPC)
            {
                Type questType = Type.GetType("TownsPeople.GamePlay.QuestComponentAddon");
                if (questType != null) rootInstance.AddComponent(questType);
            }

            NPCArchetypeConfiguration dataConfig = ScriptableObject.CreateInstance<NPCArchetypeConfiguration>();
            dataConfig.DefaultName = resolvedName;
            dataConfig.RigStyle = _rigType;
            dataConfig.InteractionPromptText = _promptTextString;
            dataConfig.UiVerticalOffsetHeight = _canvasHeightOffset;

            ScriptedResponsePacket placeholderLine = new ScriptedResponsePacket();
            placeholderLine.RequiredState = EmotionalState.Neutral;
            placeholderLine.ResponseText = "Hello traveller! What brings you to these parts?";
            placeholderLine.VoiceLineAudio = null;

            dataConfig.ScriptedDialogues = new System.Collections.Generic.List<ScriptedResponsePacket>();
            dataConfig.ScriptedDialogues.Add(placeholderLine);

            EnsureFolderExists(_outputFolderPath);

            string safeName = string.IsNullOrWhiteSpace(resolvedName) ? "Unnamed" : resolvedName.Replace(" ", "_");
            string uniquePath = $"{_outputFolderPath}/NPC_Profile_{safeName}_{System.DateTime.Now.Ticks}.asset";
            AssetDatabase.CreateAsset(dataConfig, uniquePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            SerializedObject serializedProximity = new SerializedObject(proximityLogic);
            serializedProximity.FindProperty("archetypeConfig").objectReferenceValue = dataConfig;
            serializedProximity.ApplyModifiedProperties();

            Selection.activeGameObject = rootInstance;
            EditorGUIUtility.PingObject(dataConfig);

            string nameNote = resolvedName != _npcName
                ? $"\n\nNote: '{_npcName}' was already in use — this NPC was named '{resolvedName}' instead."
                : "";

            EditorUtility.DisplayDialog("Success!", $"Compiled an Asset-Ready {_selectedVariant.ToString()} prefab.\n\nProfile saved to:\n{uniquePath}{nameNote}", "Perfect");
        }

        /// <summary>
        /// Recursively searches every layer of the given controller for a state whose name
        /// matches (case-insensitive) and whose Motion is a BlendTree. Used to auto-populate
        /// LocomotionAgent's per-pose playback rates at generation time.
        /// </summary>
        private static BlendTree FindNamedBlendTree(AnimatorController controller, string stateName)
        {
            foreach (AnimatorControllerLayer layer in controller.layers)
            {
                BlendTree found = FindNamedBlendTreeRecursive(layer.stateMachine, stateName);
                if (found != null) return found;
            }
            return null;
        }

        private static BlendTree FindNamedBlendTreeRecursive(AnimatorStateMachine stateMachine, string stateName)
        {
            foreach (ChildAnimatorState childState in stateMachine.states)
            {
                if (string.Equals(childState.state.name, stateName, StringComparison.OrdinalIgnoreCase)
                    && childState.state.motion is BlendTree blendTree)
                {
                    return blendTree;
                }
            }
            foreach (ChildAnimatorStateMachine childMachine in stateMachine.stateMachines)
            {
                BlendTree found = FindNamedBlendTreeRecursive(childMachine.stateMachine, stateName);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>
        /// Recursively searches every layer for a state matching the given name (case-
        /// insensitive), regardless of what its Motion is — used to confirm a plain "Empty"
        /// state actually exists before pointing Default Idle States at it.
        /// </summary>
        private static AnimatorState FindNamedState(AnimatorController controller, string stateName)
        {
            foreach (AnimatorControllerLayer layer in controller.layers)
            {
                AnimatorState found = FindNamedStateRecursive(layer.stateMachine, stateName);
                if (found != null) return found;
            }
            return null;
        }

        private static AnimatorState FindNamedStateRecursive(AnimatorStateMachine stateMachine, string stateName)
        {
            foreach (ChildAnimatorState childState in stateMachine.states)
            {
                if (string.Equals(childState.state.name, stateName, StringComparison.OrdinalIgnoreCase))
                {
                    return childState.state;
                }
            }
            foreach (ChildAnimatorStateMachine childMachine in stateMachine.stateMachines)
            {
                AnimatorState found = FindNamedStateRecursive(childMachine.stateMachine, stateName);
                if (found != null) return found;
            }
            return null;
        }
    }
}