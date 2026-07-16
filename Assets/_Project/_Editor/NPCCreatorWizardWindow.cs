using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;
using System.Linq;
using Project.Data;
using Project.GamePlay;
using Project.UI;

namespace Project.CustomEditor
{
    // NOTE: This wizard automates the creation of modular NPC prefabs.
    // The CanvasGroup hierarchy has been permanently patched, and UI font sizing is now fully exposed for Asset Store distribution.
    //
    // v4: Generated NPCArchetypeConfiguration assets no longer dump into Assets/ root.
    // Added a configurable Output Folder field (with Browse button) that's auto-created,
    // including nested subfolders, if it doesn't already exist. Also added UX polish: the
    // Generate button is now disabled with an explanatory HelpBox instead of only failing
    // after the click, and the generated asset is pinged in the Project window.

    /// <summary>
    /// Professional asset store pipeline wizard window that dynamically scans project assemblies.
    /// Perfectly synchronized with Project.Data to auto-seed configurations into new profiles.
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

        // --- Editable UI & Prompt Parameters ---
        private string _promptTextString = "Talk [E]";
        private float _canvasHeightOffset = 2.2f;
        private float _dialogueFontSize = 10f; // Exposed font size variable, defaulting to 10

        // Sizing Parameters Locked Permanently at 150x35
        private Vector2 _canvasDimensions = new Vector2(150f, 35f);
        private Color _promptBgColor = new Color(0.1f, 0.1f, 0.1f, 0.85f);
        private Color _speechBgColor = new Color(0.15f, 0.15f, 0.15f, 0.90f);
        private Color _shopBgColor = new Color(0.15f, 0.15f, 0.15f, 0.95f);

        private bool _hasVendorAddon = false;
        private bool _hasQuestAddon = false;

        [Tooltip("If enabled, this NPC gets NPCRumorIndicator — an optional visual debug tool that spawns a small colored sphere above its head for each rumor it currently knows.")]
        private bool _includeRumorIndicator = false;

        [Tooltip("Optional faction this NPC belongs to, used by its NPCReputationOpinion for faction-aware effective reputation. Leave empty to use only general reputation.")]
        private string _factionId = "";

        [Tooltip("Which gendered voice line this NPC uses for rumor/response audio that provides both a Male and Female clip.")]
        private Project.Data.VoiceGender _voiceGender = Project.Data.VoiceGender.Male;

        [Tooltip("Shared library of generic Positive/Negative reactions, used by full NPCs as their rumor fallback and by Non-Dialogue NPCs as their only response source.")]
        private Project.Data.GeneralRumorResponseLibrary _responseLibrary;

        // v18: Added for the scroll view wrapping OnGUI's content.
        private Vector2 _scrollPosition;

        // v19: 3D pressable button rendering — see DrawGenerateButton().
        private bool _generateButtonPressed;
        private Texture2D _btnNormalTex;
        private Texture2D _btnHoverTex;
        private Texture2D _btnActiveTex;
        private Texture2D _btnDisabledTex;
        private GUIStyle _generateButtonLabelStyle;

        [MenuItem("Tools/NPC Creator/Launch Wizard Window")]
        public static void ShowWindow()
        {
            NPCCreatorWizardWindow window = GetWindow<NPCCreatorWizardWindow>("NPC Creator Wizard");
            window.minSize = new Vector2(480, 700);
            window.Show();
        }

        private void OnEnable()
        {
            _hasVendorAddon = Type.GetType("Project.GamePlay.VendorComponentAddon") != null;
            _hasQuestAddon = Type.GetType("Project.GamePlay.QuestComponentAddon") != null;
        }

        // --- Editor GUI Layout ---

        private void OnGUI()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            GUILayout.Label("NPC Creator Pipeline Wizard", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Automated multi-layer UI creation pipeline for Asset Store deployment.", EditorStyles.miniLabel);
            EditorGUILayout.Space();

            // Core Identity Parameters
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("Core Identity Parameters", EditorStyles.boldLabel);
            _npcName = EditorGUILayout.TextField("NPC Display Name", _npcName);
            _meshModel = (GameObject)EditorGUILayout.ObjectField("3D Mesh Asset / Prefab", _meshModel, typeof(GameObject), false);
            _rigType = (AnimationRigType)EditorGUILayout.EnumPopup("Animation Rig Setup", _rigType);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();

            // v4: Output Settings — where generated profile assets get saved.
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

            // Conditional Archetype Targeting
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
                EditorGUILayout.HelpBox("Skips the full rumor/gossip system entirely (no NPCGossipMemory, no rumor indicator). Only ever greets the player with a reputation-driven Positive/Negative response from the General Response Library below. Lighter weight — intended for background/ambient NPCs.", MessageType.Info);
            }

            EditorGUILayout.BeginHorizontal();
            GUI.enabled = false;
            EditorGUILayout.Toggle("Vendor Pack Detected:", _hasVendorAddon);
            EditorGUILayout.Toggle("Quest Pack Detected:", _hasQuestAddon);
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();

            // Visual Prompt & Text Configurations
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("Visual Prompt & UI Canvas Formats", EditorStyles.boldLabel);

            float originalLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 220f;

            _promptTextString = EditorGUILayout.TextField("Interaction Prompt Text", _promptTextString);
            _canvasHeightOffset = EditorGUILayout.FloatField("Canvas Height Offset (3D)", _canvasHeightOffset);
            _dialogueFontSize = EditorGUILayout.FloatField("Speech Bubble Font Size", _dialogueFontSize);

            // v5: Unlocked — Master Canvas Dimensions is now fully editable instead of
            // GUI.enabled-locked to a fixed 150x35 value.
            _canvasDimensions = EditorGUILayout.Vector2Field("Master Canvas Dimensions", _canvasDimensions);

            EditorGUILayout.Space();
            _promptBgColor = EditorGUILayout.ColorField("Interaction Prompt BG Color", _promptBgColor);
            _speechBgColor = EditorGUILayout.ColorField("Dialogue Speech Bubble BG Color", _speechBgColor);
            _shopBgColor = EditorGUILayout.ColorField("Merchant Menu Background Color", _shopBgColor);

            EditorGUIUtility.labelWidth = originalLabelWidth;
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();

            // Debug/visualization add-on toggle.
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("Debug & Visualization", EditorStyles.boldLabel);
            GUI.enabled = _selectedVariant != NpcVariantType.NonDialogue_NPC;
            _includeRumorIndicator = EditorGUILayout.ToggleLeft(
                new GUIContent("Include Rumor Indicator", "Spawns a small colored sphere above this NPC's head for each rumor it currently knows. Purely cosmetic/debug — safe to leave off. Unavailable on Non-Dialogue NPCs (no rumor memory to track)."),
                _includeRumorIndicator);
            GUI.enabled = true;
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();

            // v16: NonDialogue_NPC is now a variant option (selected above) rather than a
            // separate toggle. This section just holds the shared response library field.
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("Responses", EditorStyles.boldLabel);
            _responseLibrary = (Project.Data.GeneralRumorResponseLibrary)EditorGUILayout.ObjectField(
                new GUIContent("General Response Library", "Shared Positive/Negative response pools. Required for Non-Dialogue NPCs (their only response source); optional for full NPCs (used as their rumor fallback)."),
                _responseLibrary, typeof(Project.Data.GeneralRumorResponseLibrary), false);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();

            // v11: Reputation settings — every generated NPC gets NPCReputationOpinion by
            // default (it's core infrastructure, not an optional add-on), so this just lets
            // you set its faction at creation time instead of hunting it down afterward.
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("Reputation", EditorStyles.boldLabel);
            _factionId = EditorGUILayout.TextField(
                new GUIContent("Faction ID (optional)", "Sets this NPC's NPCReputationOpinion faction. Leave empty to use only general reputation."),
                _factionId);
            _voiceGender = (Project.Data.VoiceGender)EditorGUILayout.EnumPopup(
                new GUIContent("Voice Gender", "Which gendered voice line this NPC uses for rumor/response audio that provides both a Male and Female clip."),
                _voiceGender);
            EditorGUILayout.EndVertical();

            GUILayout.FlexibleSpace();

            // v4: Generate button is now disabled (with an explanation) instead of only
            // failing after the click — buyers see why they can't proceed immediately.
            bool canGenerate = _meshModel != null;
            if (!canGenerate)
            {
                EditorGUILayout.HelpBox("Assign a 3D Mesh Asset / Prefab above before generating.", MessageType.Warning);
            }

            // v19: Custom 3D pressable button — drop-shadow at rest, gradient background,
            // hover highlight, and visibly shifts down while held (shadow disappears),
            // instead of the flat default IMGUI button.
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

        /// <summary>
        /// Draws a button with a drop-shadow (raised look at rest), gradient fill, hover
        /// highlight, and a visible downward shift + shadow removal while the mouse is held on
        /// it — a real "3D pressable" feel rather than a flat default IMGUI button.
        /// </summary>
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
                    if (_generateButtonPressed && isMouseOver)
                    {
                        ExecuteEntityGeneration();
                    }
                    _generateButtonPressed = false;
                    evt.Use();
                }
            }

            bool isPressed = enabled && _generateButtonPressed;

            // Drop-shadow: visible at rest (gives the raised/3D look), gone while pressed
            // (reinforces the "sunk in" feel alongside the position shift below).
            if (!isPressed)
            {
                Rect shadowRect = baseRect;
                shadowRect.y += 3f;
                EditorGUI.DrawRect(shadowRect, new Color(0f, 0f, 0f, 0.35f));
            }

            Rect drawRect = baseRect;
            if (isPressed)
            {
                drawRect.y += 2f; // The actual "moves down when pressed" effect.
            }

            Texture2D texToUse = !enabled ? _btnDisabledTex : isPressed ? _btnActiveTex : isMouseOver ? _btnHoverTex : _btnNormalTex;
            GUI.DrawTexture(drawRect, texToUse, ScaleMode.StretchToFill);
            GUI.Label(drawRect, label, _generateButtonLabelStyle);

            if (isMouseOver || isPressed)
            {
                Repaint(); // Keep hover/press visuals responsive to mouse movement.
            }
        }

        /// <summary>
        /// Converts an absolute OS path (from OpenFolderPanel) into a project-relative
        /// "Assets/..." path. Returns null if the folder isn't inside this project.
        /// </summary>
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

        /// <summary>
        /// Creates every missing folder along the given path (e.g. "Assets/NPC Creator/Generated Profiles"
        /// creates both "NPC Creator" and "Generated Profiles" if neither exists yet).
        /// </summary>
        private static void EnsureFolderExists(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath)) return;

            string[] parts = folderPath.Split('/');
            string currentPath = parts[0]; // Expected to be "Assets"

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

        /// <summary>
        /// v6: Returns a name guaranteed not to collide with any NPC currently in the open
        /// scene (checked by NPCGossipMemory.NpcName, case-insensitive). If "Vanessa" already
        /// exists, returns "Vanessa_1"; if that also exists, "Vanessa_2", and so on.
        /// </summary>
        private string GetUniqueNpcName(string baseName)
        {
            // v7: FindObjectsOfType<T>(bool) is deprecated. FindObjectsByType requires an
            // explicit sort mode — we don't need results sorted by InstanceID here, so
            // FindObjectsSortMode.None is both correct and faster.
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

        /// <summary>
        /// v9: Returns the index of the given layer, creating it as a new user layer
        /// (in the first free slot, 8-31) if it doesn't already exist. This directly edits
        /// ProjectSettings/TagManager.asset, the same file the Tags & Layers window edits.
        /// </summary>
        private static int EnsureLayerExists(string layerName)
        {
            int existingIndex = LayerMask.NameToLayer(layerName);
            if (existingIndex != -1) return existingIndex;

            SerializedObject tagManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layersProp = tagManager.FindProperty("layers");

            // User layers start at index 8 (0-7 are Unity's built-in layers).
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

            Debug.LogWarning($"<color=orange>[NPC Creator Wizard]</color> No free layer slots available (8-31 all in use) — could not create '{layerName}' layer. NPC will be left on its current layer.");
            return -1;
        }

        /// <summary>
        /// Sets a layer on a GameObject and every one of its children, recursively.
        /// </summary>
        private static void SetLayerRecursively(GameObject target, int layer)
        {
            target.layer = layer;
            foreach (Transform child in target.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }

        // --- Core Generation Pipeline ---

        /// <summary>
        /// Executes the deterministic compilation of the complete NPC prefab hierarchy.
        /// </summary>
        private void ExecuteEntityGeneration()
        {
            if (_meshModel == null)
            {
                EditorUtility.DisplayDialog("Creation Aborted", "Please assign a 3D Mesh asset target prefab before compiling.", "OK");
                return;
            }

            // Step 1: Core Node Layout
            // v6: Resolve a collision-free name before anything else, so it's used
            // consistently for the GameObject, NpcName, and the saved profile asset.
            string resolvedName = GetUniqueNpcName(_npcName);

            GameObject rootInstance = new GameObject($"NPC_AssetStore_{resolvedName}");
            Undo.RegisterCreatedObjectUndo(rootInstance, "Create Modular NPC Root");

            // v9: Every generated NPC defaults to the "NPC" layer (created automatically if
            // it doesn't exist yet), applied to the whole hierarchy. This gives
            // PlayerDeedBroadcaster's Npc Layer Mask something real to filter witness
            // detection on, instead of leaving it at "Everything".
            int npcLayer = EnsureLayerExists("NPC");
            if (npcLayer != -1)
            {
                SetLayerRecursively(rootInstance, npcLayer);
            }

            // Step 2: Visuals & Animator Automation
            GameObject meshInstance = (GameObject)PrefabUtility.InstantiatePrefab(_meshModel, rootInstance.transform);
            meshInstance.name = "Character_Visual_Mesh";
            meshInstance.transform.localPosition = Vector3.zero;
            meshInstance.transform.localRotation = Quaternion.identity;

            Animator characterAnimator = meshInstance.GetComponent<Animator>();
            if (characterAnimator == null)
            {
                characterAnimator = meshInstance.AddComponent<Animator>();
            }

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

            // Step 3: Mechanical Baseline Systems
            SphereCollider proxCollider = rootInstance.AddComponent<SphereCollider>();
            proxCollider.isTrigger = true;
            proxCollider.radius = 4.0f;

            // v15: Full NPCs get NPCGossipMemory (rumor tracking + presentation); Non-Dialogue
            // NPCs get the lighter NPCGreetingResponder instead, with no rumor memory at all.
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
            rootInstance.AddComponent<AudioSource>();

            // v5: Every generated NPC now gets an NPCAnimationBridge automatically — previously
            // this had to be added by hand, meaning tone-driven animation and the exit-revert
            // fix silently didn't work until someone noticed and added it manually.
            NPCAnimationBridge animBridge = rootInstance.AddComponent<NPCAnimationBridge>();
            SerializedObject serializedAnimBridge = new SerializedObject(animBridge);
            serializedAnimBridge.FindProperty("_animator").objectReferenceValue = characterAnimator;
            serializedAnimBridge.ApplyModifiedProperties();

            // Every generated NPC gets an NpcAddonRegistry so add-ons (Vendor, Quest Giver, etc.)
            // are discoverable at runtime via TryGetAddon<T>() instead of raw GetComponent calls.
            rootInstance.AddComponent<NpcAddonRegistry>();

            // v11: Every generated NPC now gets NPCReputationOpinion by default — this is core
            // Reputation System infrastructure (roadmap Section 4), not an "Advanced Add-on"
            // like Vendor/Quest/Locomotion, so it shouldn't require opting in. Everything that
            // reads it already null-checks for its absence, so this is purely additive.
            NPCReputationOpinion reputationOpinion = rootInstance.AddComponent<NPCReputationOpinion>();
            if (!string.IsNullOrEmpty(_factionId))
            {
                SerializedObject serializedOpinion = new SerializedObject(reputationOpinion);
                serializedOpinion.FindProperty("_factionId").stringValue = _factionId;
                serializedOpinion.ApplyModifiedProperties();
            }

            // Only added for full NPCs — Non-Dialogue NPCs have no NPCGossipMemory for this to
            // require, and the toggle is disabled in the GUI for them anyway.
            if (_includeRumorIndicator && _selectedVariant != NpcVariantType.NonDialogue_NPC)
            {
                rootInstance.AddComponent<NPCRumorIndicator>();
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

            // v13: Added — without this, the canvas only reads correctly from whatever angle
            // it happened to be facing when generated. Billboard keeps both the [E] prompt and
            // the speech bubble (its children) facing the camera continuously.
            canvasObj.AddComponent<Billboard>();

            // ELEMENT 1: INTERACTION PROMPT BOX (Permanently Fixed Hierarchy)
            GameObject promptBackground = new GameObject("Graphic_Placeholder_Background");
            promptBackground.transform.SetParent(canvasObj.transform, false);

            CanvasGroup promptCanvasGroup = promptBackground.AddComponent<CanvasGroup>();
            // v12: Added — the [E] prompt now fades via CanvasGroupFader instead of snapping.
            CanvasGroupFader promptFader = promptBackground.AddComponent<CanvasGroupFader>();

            Image promptBgImage = promptBackground.AddComponent<Image>();
            promptBgImage.color = _promptBgColor;

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

            // ELEMENT 2: SEPARATE SPEECH BUBBLE LAYER
            GameObject speechBubbleNode = new GameObject("NPC_Dialogue_Speech_Bubble");
            speechBubbleNode.transform.SetParent(canvasObj.transform, false);
            speechBubbleNode.transform.localPosition = new Vector3(0f, 80f, 0f);

            CanvasGroup speechCanvasGroup = speechBubbleNode.AddComponent<CanvasGroup>();
            NPCSpeechBubble speechBubble = speechBubbleNode.AddComponent<NPCSpeechBubble>();

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

            // NOTE: Font size is now dynamically controlled by the Wizard's exposed variable
            dialogueTmp.fontSize = _dialogueFontSize;

            dialogueTmp.text = "...";

            RectTransform dialogueTextRect = dialogueTextObj.GetComponent<RectTransform>();
            dialogueTextRect.anchorMin = Vector2.zero;
            dialogueTextRect.anchorMax = Vector2.one;
            dialogueTextRect.sizeDelta = Vector2.zero;

            // Auto-wire proximity logic dependencies securely
            SerializedObject serializedLogic = new SerializedObject(proximityLogic);
            serializedLogic.FindProperty("speechBubble").objectReferenceValue = speechBubble;
            serializedLogic.FindProperty("interactionPromptFader").objectReferenceValue = promptFader;
            serializedLogic.ApplyModifiedProperties();

            // Step 5: Screenspace Shopping Canvas Automation
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

            // Conditional Reflection Addons
            if (_selectedVariant == NpcVariantType.Vendor_NPC)
            {
                Type vendorType = Type.GetType("Project.GamePlay.VendorComponentAddon");
                if (vendorType != null) rootInstance.AddComponent(vendorType);
            }
            else if (_selectedVariant == NpcVariantType.QuestGiver_NPC)
            {
                Type questType = Type.GetType("Project.GamePlay.QuestComponentAddon");
                if (questType != null) rootInstance.AddComponent(questType);
            }

            // Step 6: Data Compilation & Explicit Seeding
            NPCArchetypeConfiguration dataConfig = ScriptableObject.CreateInstance<NPCArchetypeConfiguration>();
            dataConfig.DefaultName = resolvedName;
            dataConfig.RigStyle = _rigType;
            // BrainStyle uses NPCArchetypeConfiguration's own default (FixedScripted).
            dataConfig.InteractionPromptText = _promptTextString;
            dataConfig.UiVerticalOffsetHeight = _canvasHeightOffset;

            // Seed fallback string for FixedScripted branch
            ScriptedResponsePacket placeholderLine = new ScriptedResponsePacket();
            placeholderLine.RequiredState = EmotionalState.Neutral;
            placeholderLine.ResponseText = "Hello traveller! What brings you to these parts?";
            placeholderLine.VoiceLineAudio = null;

            dataConfig.ScriptedDialogues = new System.Collections.Generic.List<ScriptedResponsePacket>();
            dataConfig.ScriptedDialogues.Add(placeholderLine);

            // v4: Ensure the configured output folder (and any missing parent folders) exists
            // before saving, instead of dumping the asset into Assets/ root.
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
            // Ping the generated profile asset in the Project window so it's easy to find.
            EditorGUIUtility.PingObject(dataConfig);

            string nameNote = resolvedName != _npcName
                ? $"\n\nNote: '{_npcName}' was already in use — this NPC was named '{resolvedName}' instead."
                : "";

            EditorUtility.DisplayDialog("Success!", $"Compiled an Asset-Ready {_selectedVariant.ToString()} prefab.\n\nProfile saved to:\n{uniquePath}{nameNote}", "Perfect");
        }
    }
}