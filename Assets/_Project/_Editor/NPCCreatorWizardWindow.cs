using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using System;
using Project.Data;
using Project.GamePlay;
using Project.UI;

namespace Project.CustomEditor
{
    // NOTE: This wizard automates the creation of modular NPC prefabs.
    // The CanvasGroup hierarchy has been permanently patched, and UI font sizing is now fully exposed for Asset Store distribution.

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
        private ConversationalBrainType _brainType = ConversationalBrainType.GenerativeAI;

        private enum NpcVariantType { CommonNPC, VendorNPC, QuestGiverNPC }
        private NpcVariantType _selectedVariant = NpcVariantType.CommonNPC;

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

        [MenuItem("Tools/NPC Creator/Launch Wizard Window")]
        public static void ShowWindow()
        {
            NPCCreatorWizardWindow window = GetWindow<NPCCreatorWizardWindow>("NPC Creator Wizard");
            window.minSize = new Vector2(480, 660);
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
            GUILayout.Label("NPC Creator Pipeline Wizard", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Automated multi-layer UI creation pipeline for Asset Store deployment.", EditorStyles.miniLabel);
            EditorGUILayout.Space();

            // Core Identity Parameters
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("Core Identity Parameters", EditorStyles.boldLabel);
            _npcName = EditorGUILayout.TextField("NPC Display Name", _npcName);
            _meshModel = (GameObject)EditorGUILayout.ObjectField("3D Mesh Asset / Prefab", _meshModel, typeof(GameObject), false);
            _rigType = (AnimationRigType)EditorGUILayout.EnumPopup("Animation Rig Setup", _rigType);
            _brainType = (ConversationalBrainType)EditorGUILayout.EnumPopup("Conversation Engine", _brainType);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();

            // Conditional Archetype Targeting
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("Entity Role Archetype Selection", EditorStyles.boldLabel);
            _selectedVariant = (NpcVariantType)EditorGUILayout.EnumPopup("Target NPC Variant Role", _selectedVariant);

            if (_selectedVariant == NpcVariantType.VendorNPC && !_hasVendorAddon)
            {
                EditorGUILayout.HelpBox("⚠️ Vendor NPC selection locked! 'VendorComponentAddon.cs' was not detected.", MessageType.Warning);
                _selectedVariant = NpcVariantType.CommonNPC;
            }
            if (_selectedVariant == NpcVariantType.QuestGiverNPC && !_hasQuestAddon)
            {
                EditorGUILayout.HelpBox("⚠️ Quest Giver NPC selection locked! 'QuestComponentAddon.cs' was not detected.", MessageType.Warning);
                _selectedVariant = NpcVariantType.CommonNPC;
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

            GUI.enabled = false;
            _canvasDimensions = EditorGUILayout.Vector2Field("Master Canvas Dimensions (Locked)", _canvasDimensions);
            GUI.enabled = true;

            EditorGUILayout.Space();
            _promptBgColor = EditorGUILayout.ColorField("Interaction Prompt BG Color", _promptBgColor);
            _speechBgColor = EditorGUILayout.ColorField("Dialogue Speech Bubble BG Color", _speechBgColor);
            _shopBgColor = EditorGUILayout.ColorField("Merchant Menu Background Color", _shopBgColor);

            EditorGUIUtility.labelWidth = originalLabelWidth;
            EditorGUILayout.EndVertical();

            GUILayout.FlexibleSpace();

            GUI.backgroundColor = Color.green;
            if (GUILayout.Button($"GENERATE COMPLETE {_selectedVariant.ToString().ToUpper()}", GUILayout.Height(45)))
            {
                ExecuteEntityGeneration();
            }
            GUI.backgroundColor = Color.white;
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
            GameObject rootInstance = new GameObject($"NPC_AssetStore_{_npcName}");
            Undo.RegisterCreatedObjectUndo(rootInstance, "Create Modular NPC Root");

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

            NPCGossipMemory localMemory = rootInstance.AddComponent<NPCGossipMemory>();
            localMemory.NpcName = _npcName;

            NPCProximityGossip proximityLogic = rootInstance.AddComponent<NPCProximityGossip>();
            rootInstance.AddComponent<AudioSource>();

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

            // ELEMENT 1: INTERACTION PROMPT BOX (Permanently Fixed Hierarchy)
            GameObject promptBackground = new GameObject("Graphic_Placeholder_Background");
            promptBackground.transform.SetParent(canvasObj.transform, false);

            CanvasGroup promptCanvasGroup = promptBackground.AddComponent<CanvasGroup>();

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
            serializedLogic.FindProperty("interactionPromptCanvasGroup").objectReferenceValue = promptCanvasGroup;
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
            if (_selectedVariant == NpcVariantType.VendorNPC)
            {
                Type vendorType = Type.GetType("Project.GamePlay.VendorComponentAddon");
                if (vendorType != null) rootInstance.AddComponent(vendorType);
            }
            else if (_selectedVariant == NpcVariantType.QuestGiverNPC)
            {
                Type questType = Type.GetType("Project.GamePlay.QuestComponentAddon");
                if (questType != null) rootInstance.AddComponent(questType);
            }

            // Step 6: Data Compilation & Explicit Seeding
            NPCArchetypeConfiguration dataConfig = ScriptableObject.CreateInstance<NPCArchetypeConfiguration>();
            dataConfig.DefaultName = _npcName;
            dataConfig.RigStyle = _rigType;
            dataConfig.BrainStyle = _brainType;
            dataConfig.InteractionPromptText = _promptTextString;
            dataConfig.UiVerticalOffsetHeight = _canvasHeightOffset;

            // Seed fallback string for FixedScripted branch
            ScriptedResponsePacket placeholderLine = new ScriptedResponsePacket();
            placeholderLine.RequiredState = EmotionalState.Neutral;
            placeholderLine.ResponseText = "Hello traveller! What brings you to these parts?";
            placeholderLine.VoiceLineAudio = null;

            dataConfig.ScriptedDialogues = new System.Collections.Generic.List<ScriptedResponsePacket>();
            dataConfig.ScriptedDialogues.Add(placeholderLine);

            string uniquePath = $"Assets/New_NPC_Profile_{_npcName}_{System.DateTime.Now.Ticks}.asset";
            AssetDatabase.CreateAsset(dataConfig, uniquePath);
            AssetDatabase.SaveAssets();

            SerializedObject serializedProximity = new SerializedObject(proximityLogic);
            serializedProximity.FindProperty("archetypeConfig").objectReferenceValue = dataConfig;
            serializedProximity.ApplyModifiedProperties();

            Selection.activeGameObject = rootInstance;
            EditorUtility.DisplayDialog("Success!", $"Compiled an Asset-Ready {_selectedVariant.ToString()} prefab.", "Perfect");
        }
    }
}