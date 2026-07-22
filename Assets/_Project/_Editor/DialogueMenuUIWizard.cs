#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using UnityEngine.Video;
using TMPro;
using TownsPeople.UI;
using TownsPeople.GamePlay;

namespace TownsPeople.CustomEditor
{
    /// <summary>
    /// One-click generator for the shared Dialogue Menu UI. Builds the whole hierarchy — name
    /// header, a flexible middle region (housing either the scrollable list or the carousel
    /// slot), and a Leave button pinned to the bottom — and wires DialogueMenuUI automatically.
    ///
    /// Also supports "Use Existing Prefab" mode: a developer can drop in their own custom UI
    /// prefab and have UIElementAutoWirer scan it for matching elements by name + component
    /// type, wiring DialogueMenuUI automatically instead of generating UI from scratch.
    /// </summary>
    // v18: Added "Use Existing Prefab" mode.
    // v19: Generate New no longer uses a VerticalLayoutGroup on the panel root or the carousel slot.
    // v20: Larger, readable info text; Custom UI Prefab / GameObject label on its own line.
    // v21: FIX — buttonSprite's CreateRoundedRectSprite call was missing _borderColor (CS7036).
    // v22: FIX — option button template cleanup now uses UIElementAutoWirer.SafeDestroyImmediate
    // instead of a raw DestroyImmediate, preventing MissingReferenceException /
    // SerializedObjectNotCreatableException in the Inspector if that template (or, more likely,
    // the extracted element in Use Existing Prefab mode — see UIElementAutoWirer v2) happened to
    // be selected when this ran.
    public class DialogueMenuUIWizard : EditorWindow
    {
        private const string OutputFolder = "Assets/NPC Creator/Generated UI";

        private const float PanelPadding = 16f;
        private const float PanelSpacing = 10f;
        private const float NameHeight = 32f;
        private const float LeaveHeight = 36f;
        private const float CarouselButtonHeight = 60f;
        private const float CarouselIndexHeight = 20f;
        private const float CarouselSpacing = 8f;

        private enum WizardMode { GenerateNew, UseExistingPrefab }
        private WizardMode _mode = WizardMode.GenerateNew;
        private GameObject _sourcePrefab;

        [Header("Visual Style")]
        private float _cornerRadius = 14f;
        private float _borderThickness = 3f;
        private Color _borderColor = new Color(0.80f, 0.66f, 0.32f, 1f);
        private Color _panelFillTop = new Color(0.17f, 0.17f, 0.21f, 0.97f);
        private Color _panelFillBottom = new Color(0.08f, 0.08f, 0.11f, 0.97f);
        private Color _buttonFillTop = new Color(0.32f, 0.32f, 0.36f, 1f);
        private Color _buttonFillBottom = new Color(0.19f, 0.19f, 0.23f, 1f);

        [MenuItem("Tools/TownsPeople/Generate Dialogue Menu UI")]
        public static void ShowWindow()
        {
            DialogueMenuUIWizard window = GetWindow<DialogueMenuUIWizard>("Dialogue Menu UI Wizard");
            window.minSize = new Vector2(380, 180);
            window.Show();
        }

        private void OnGUI()
        {
            GUILayout.Label("Dialogue Menu UI Generator", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Builds the shared dialogue menu panel (NPC name header, options area, Leave button) and wires DialogueMenuUI automatically. Only one is needed for the whole game. Toggle List vs Carousel display mode afterward on the generated DialogueMenuUI component. Every generated element's position/size is freely editable afterward in the Inspector or Scene view — nothing re-locks it into place.",
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space();

            _mode = (WizardMode)GUILayout.Toolbar((int)_mode, new[] { "Generate New", "Use Existing Prefab" });
            EditorGUILayout.Space();

            if (_mode == WizardMode.UseExistingPrefab)
            {
                DrawLargeInfoBox(
                    "Drop in your own UI prefab and the wizard wires DialogueMenuUI automatically. Name " +
                    "elements with these keywords:\n\n" +
                    "\u2022 NPC name text: \"Name\"   \u2022 Leave button: \"Leave\"\n" +
                    "\u2022 List root: \"List\"   \u2022 Options content: \"Content\"\n" +
                    "\u2022 Option button template: \"Option\" (not \"Carousel\") \u2014 extracted as its own prefab\n" +
                    "\u2022 Carousel root/group/button/label: contain \"Carousel\"   \u2022 Carousel index text: \"Index\"\n" +
                    "\u2022 Rumor popup fader/text: \"Popup\"   \u2022 Popup close button: \"Close\"\n" +
                    "\u2022 Portrait image: \"Portrait\" (image vs. video RawImage auto-distinguished by type)\n" +
                    "\u2022 Video player: on the popup object   \u2022 Click AudioSource: \"Click\"");

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Custom UI Prefab / GameObject", EditorStyles.boldLabel);
                _sourcePrefab = (GameObject)EditorGUILayout.ObjectField(_sourcePrefab, typeof(GameObject), true);
            }
            else
            {
                EditorGUILayout.BeginVertical("box");
                GUILayout.Label("Visual Style", EditorStyles.boldLabel);
                _cornerRadius = EditorGUILayout.Slider("Corner Radius", _cornerRadius, 0f, 32f);
                _borderThickness = EditorGUILayout.Slider("Border Thickness", _borderThickness, 0f, 10f);
                _borderColor = EditorGUILayout.ColorField("Border Color", _borderColor);
                EditorGUILayout.Space();
                _panelFillTop = EditorGUILayout.ColorField("Panel Fill (Top)", _panelFillTop);
                _panelFillBottom = EditorGUILayout.ColorField("Panel Fill (Bottom)", _panelFillBottom);
                EditorGUILayout.Space();
                _buttonFillTop = EditorGUILayout.ColorField("Button Fill (Top)", _buttonFillTop);
                _buttonFillBottom = EditorGUILayout.ColorField("Button Fill (Bottom)", _buttonFillBottom);
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space();

            GUI.backgroundColor = Color.green;
            string buttonLabel = _mode == WizardMode.GenerateNew ? "GENERATE DIALOGUE MENU UI" : "WIRE CUSTOM DIALOGUE MENU UI";
            if (GUILayout.Button(buttonLabel, GUILayout.Height(40)))
            {
                if (_mode == WizardMode.GenerateNew) GenerateDialogueMenuUI();
                else GenerateFromExistingPrefab();
            }
            GUI.backgroundColor = Color.white;
        }

        private static void DrawLargeInfoBox(string message)
        {
            EditorGUILayout.BeginVertical("box");
            GUIStyle labelStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 13,
                wordWrap = true
            };
            EditorGUILayout.LabelField(message, labelStyle);
            EditorGUILayout.EndVertical();
        }

        private void GenerateDialogueMenuUI()
        {
            EnsureFolderExists(OutputFolder);
            Canvas canvas = FindOrCreateCanvas();

            Sprite panelSprite = ProceduralUISprites.CreateRoundedRectSprite(
                $"{OutputFolder}/PanelBackground.png", 128, _cornerRadius, _borderThickness, _borderColor, _panelFillTop, _panelFillBottom);

            Sprite buttonSprite = ProceduralUISprites.CreateRoundedRectSprite(
                $"{OutputFolder}/ButtonBackground.png", 64, _cornerRadius * 0.6f, _borderThickness, _borderColor, _buttonFillTop, _buttonFillBottom);

            // --- Panel root ---
            GameObject panelObj = new GameObject("DialogueMenuPanel", typeof(RectTransform));
            panelObj.transform.SetParent(canvas.transform, false);

            RectTransform panelRect = panelObj.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(420f, 480f);
            panelRect.anchoredPosition = Vector2.zero;

            Image panelBg = panelObj.AddComponent<Image>();
            panelBg.sprite = panelSprite;
            panelBg.type = Image.Type.Sliced;
            panelBg.color = Color.white;

            panelObj.AddComponent<CanvasGroup>();
            CanvasGroupFader panelFader = panelObj.AddComponent<CanvasGroupFader>();
            DialogueMenuUI menuUI = panelObj.AddComponent<DialogueMenuUI>();

            AudioSource clickAudioSource = panelObj.AddComponent<AudioSource>();
            clickAudioSource.playOnAwake = false;
            clickAudioSource.spatialBlend = 0f;

            // --- NPC name header ---
            GameObject nameObj = new GameObject("NpcNameText", typeof(RectTransform));
            nameObj.transform.SetParent(panelObj.transform, false);
            RectTransform nameRect = nameObj.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0f, 1f);
            nameRect.anchorMax = new Vector2(1f, 1f);
            nameRect.pivot = new Vector2(0.5f, 1f);
            nameRect.anchoredPosition = new Vector2(0f, -PanelPadding);
            nameRect.sizeDelta = new Vector2(-PanelPadding * 2f, NameHeight);

            TextMeshProUGUI nameText = nameObj.AddComponent<TextMeshProUGUI>();
            nameText.text = "NPC Name";
            nameText.fontSize = 22;
            nameText.fontStyle = FontStyles.Bold;
            nameText.alignment = TextAlignmentOptions.Center;

            // --- Flexible middle region ---
            GameObject middleContainer = new GameObject("MiddleContainer", typeof(RectTransform));
            middleContainer.transform.SetParent(panelObj.transform, false);
            RectTransform middleRect = middleContainer.GetComponent<RectTransform>();
            middleRect.anchorMin = Vector2.zero;
            middleRect.anchorMax = Vector2.one;
            float middleTopOffset = PanelPadding + NameHeight + PanelSpacing;
            float middleBottomOffset = PanelPadding + LeaveHeight + PanelSpacing;
            middleRect.offsetMin = new Vector2(PanelPadding, middleBottomOffset);
            middleRect.offsetMax = new Vector2(-PanelPadding, -middleTopOffset);

            // ===== List mode UI =====
            GameObject scrollObj = new GameObject("OptionsScrollView", typeof(RectTransform));
            scrollObj.transform.SetParent(middleContainer.transform, false);
            RectTransform scrollFullRect = scrollObj.GetComponent<RectTransform>();
            scrollFullRect.anchorMin = Vector2.zero;
            scrollFullRect.anchorMax = Vector2.one;
            scrollFullRect.offsetMin = Vector2.zero;
            scrollFullRect.offsetMax = Vector2.zero;

            ScrollRect scrollRect = scrollObj.AddComponent<ScrollRect>();
            Image scrollBg = scrollObj.AddComponent<Image>();
            scrollBg.color = new Color(0f, 0f, 0f, 0.15f);
            Mask scrollMask = scrollObj.AddComponent<Mask>();
            scrollMask.showMaskGraphic = true;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.viewport = scrollFullRect;

            GameObject contentObj = new GameObject("Content", typeof(RectTransform));
            contentObj.transform.SetParent(scrollObj.transform, false);
            RectTransform contentRect = contentObj.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);

            VerticalLayoutGroup contentLayout = contentObj.AddComponent<VerticalLayoutGroup>();
            contentLayout.spacing = 6f;
            contentLayout.childControlHeight = false;
            contentLayout.childControlWidth = true;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;

            ContentSizeFitter contentFitter = contentObj.AddComponent<ContentSizeFitter>();
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.content = contentRect;

            // Option button template — built once, saved as a reusable prefab asset.
            GameObject buttonTemplate = new GameObject("DialogueOptionButton", typeof(RectTransform));
            RectTransform buttonRect = buttonTemplate.GetComponent<RectTransform>();
            buttonRect.sizeDelta = new Vector2(0f, 40f);
            Image buttonBg = buttonTemplate.AddComponent<Image>();
            buttonBg.sprite = buttonSprite;
            buttonBg.type = Image.Type.Sliced;
            buttonBg.color = Color.white;
            buttonTemplate.AddComponent<Button>();
            buttonTemplate.AddComponent<AnimatedButtonFeedback>();

            GameObject buttonLabelObj = new GameObject("Label", typeof(RectTransform));
            buttonLabelObj.transform.SetParent(buttonTemplate.transform, false);
            TextMeshProUGUI buttonLabel = buttonLabelObj.AddComponent<TextMeshProUGUI>();
            buttonLabel.text = "Option";
            buttonLabel.alignment = TextAlignmentOptions.Center;
            buttonLabel.fontSize = 16;
            RectTransform buttonLabelRect = buttonLabelObj.GetComponent<RectTransform>();
            buttonLabelRect.anchorMin = Vector2.zero;
            buttonLabelRect.anchorMax = Vector2.one;
            buttonLabelRect.offsetMin = Vector2.zero;
            buttonLabelRect.offsetMax = Vector2.zero;

            string buttonPrefabPath = $"{OutputFolder}/DialogueOptionButton.prefab";
            GameObject savedButtonPrefab = PrefabUtility.SaveAsPrefabAsset(buttonTemplate, buttonPrefabPath);

            // v22 FIX: was a raw Object.DestroyImmediate — now goes through the safe helper.
            UIElementAutoWirer.SafeDestroyImmediate(buttonTemplate);

            // ===== Carousel mode UI =====
            GameObject carouselObj = new GameObject("CarouselSlot", typeof(RectTransform));
            carouselObj.transform.SetParent(middleContainer.transform, false);
            RectTransform carouselFullRect = carouselObj.GetComponent<RectTransform>();
            carouselFullRect.anchorMin = Vector2.zero;
            carouselFullRect.anchorMax = Vector2.one;
            carouselFullRect.offsetMin = Vector2.zero;
            carouselFullRect.offsetMax = Vector2.zero;

            float carouselBlockHeight = CarouselButtonHeight + CarouselSpacing + CarouselIndexHeight;

            GameObject carouselButtonObj = new GameObject("CarouselOptionButton", typeof(RectTransform));
            carouselButtonObj.transform.SetParent(carouselObj.transform, false);
            RectTransform carouselButtonRect = carouselButtonObj.GetComponent<RectTransform>();
            carouselButtonRect.anchorMin = new Vector2(0f, 0.5f);
            carouselButtonRect.anchorMax = new Vector2(1f, 0.5f);
            carouselButtonRect.pivot = new Vector2(0.5f, 0.5f);
            carouselButtonRect.sizeDelta = new Vector2(0f, CarouselButtonHeight);
            carouselButtonRect.anchoredPosition = new Vector2(0f, carouselBlockHeight / 2f - CarouselButtonHeight / 2f);

            Image carouselButtonBg = carouselButtonObj.AddComponent<Image>();
            carouselButtonBg.sprite = buttonSprite;
            carouselButtonBg.type = Image.Type.Sliced;
            carouselButtonBg.color = Color.white;
            CanvasGroup carouselCanvasGroup = carouselButtonObj.AddComponent<CanvasGroup>();
            Button carouselButton = carouselButtonObj.AddComponent<Button>();
            carouselButtonObj.AddComponent<AnimatedButtonFeedback>();

            GameObject carouselLabelObj = new GameObject("Label", typeof(RectTransform));
            carouselLabelObj.transform.SetParent(carouselButtonObj.transform, false);
            TextMeshProUGUI carouselLabel = carouselLabelObj.AddComponent<TextMeshProUGUI>();
            carouselLabel.text = "Option";
            carouselLabel.alignment = TextAlignmentOptions.Center;
            carouselLabel.fontSize = 18;
            RectTransform carouselLabelRect = carouselLabelObj.GetComponent<RectTransform>();
            carouselLabelRect.anchorMin = Vector2.zero;
            carouselLabelRect.anchorMax = Vector2.one;
            carouselLabelRect.offsetMin = Vector2.zero;
            carouselLabelRect.offsetMax = Vector2.zero;

            GameObject carouselIndexObj = new GameObject("CarouselIndexText", typeof(RectTransform));
            carouselIndexObj.transform.SetParent(carouselObj.transform, false);
            RectTransform carouselIndexRect = carouselIndexObj.GetComponent<RectTransform>();
            carouselIndexRect.anchorMin = new Vector2(0f, 0.5f);
            carouselIndexRect.anchorMax = new Vector2(1f, 0.5f);
            carouselIndexRect.pivot = new Vector2(0.5f, 0.5f);
            carouselIndexRect.sizeDelta = new Vector2(0f, CarouselIndexHeight);
            carouselIndexRect.anchoredPosition = new Vector2(0f, carouselBlockHeight / 2f - CarouselButtonHeight - CarouselSpacing - CarouselIndexHeight / 2f);

            TextMeshProUGUI carouselIndexText = carouselIndexObj.AddComponent<TextMeshProUGUI>();
            carouselIndexText.text = "1 / 1";
            carouselIndexText.alignment = TextAlignmentOptions.Center;
            carouselIndexText.fontSize = 14;
            carouselIndexText.color = new Color(1f, 1f, 1f, 0.6f);

            carouselObj.SetActive(false);

            // --- Leave button ---
            GameObject leaveObj = new GameObject("LeaveButton", typeof(RectTransform));
            leaveObj.transform.SetParent(panelObj.transform, false);
            RectTransform leaveRect = leaveObj.GetComponent<RectTransform>();
            leaveRect.anchorMin = new Vector2(0f, 0f);
            leaveRect.anchorMax = new Vector2(1f, 0f);
            leaveRect.pivot = new Vector2(0.5f, 0f);
            leaveRect.anchoredPosition = new Vector2(0f, PanelPadding);
            leaveRect.sizeDelta = new Vector2(-PanelPadding * 2f, LeaveHeight);

            Image leaveBg = leaveObj.AddComponent<Image>();
            leaveBg.sprite = buttonSprite;
            leaveBg.type = Image.Type.Sliced;
            Button leaveButton = leaveObj.AddComponent<Button>();

            AnimatedButtonFeedback leaveFeedback = leaveObj.AddComponent<AnimatedButtonFeedback>();
            SerializedObject serializedLeaveFeedback = new SerializedObject(leaveFeedback);
            serializedLeaveFeedback.FindProperty("_normalColor").colorValue = new Color(1.3f, 0.75f, 0.75f, 1f);
            serializedLeaveFeedback.FindProperty("_hoverColor").colorValue = new Color(1.5f, 0.85f, 0.85f, 1f);
            serializedLeaveFeedback.FindProperty("_pressedColor").colorValue = new Color(1.1f, 0.6f, 0.6f, 1f);
            serializedLeaveFeedback.ApplyModifiedProperties();

            GameObject leaveLabelObj = new GameObject("Label", typeof(RectTransform));
            leaveLabelObj.transform.SetParent(leaveObj.transform, false);
            TextMeshProUGUI leaveLabel = leaveLabelObj.AddComponent<TextMeshProUGUI>();
            leaveLabel.text = "Leave";
            leaveLabel.alignment = TextAlignmentOptions.Center;
            leaveLabel.fontSize = 16;
            RectTransform leaveLabelRect = leaveLabelObj.GetComponent<RectTransform>();
            leaveLabelRect.anchorMin = Vector2.zero;
            leaveLabelRect.anchorMax = Vector2.one;
            leaveLabelRect.offsetMin = Vector2.zero;
            leaveLabelRect.offsetMax = Vector2.zero;

            // --- Rumor popup ---
            GameObject popupObj = new GameObject("RumorPopupPanel", typeof(RectTransform));
            popupObj.transform.SetParent(canvas.transform, false);
            RectTransform popupRect = popupObj.GetComponent<RectTransform>();
            popupRect.anchorMin = new Vector2(0.5f, 0.5f);
            popupRect.anchorMax = new Vector2(0.5f, 0.5f);
            popupRect.pivot = new Vector2(0.5f, 0.5f);
            popupRect.sizeDelta = new Vector2(440f, 220f);
            popupRect.anchoredPosition = Vector2.zero;

            Image popupBg = popupObj.AddComponent<Image>();
            popupBg.sprite = panelSprite;
            popupBg.type = Image.Type.Sliced;
            popupBg.color = Color.white;
            popupObj.AddComponent<CanvasGroup>();
            CanvasGroupFader popupFader = popupObj.AddComponent<CanvasGroupFader>();
            VideoPlayer popupVideoPlayer = popupObj.AddComponent<VideoPlayer>();
            popupVideoPlayer.playOnAwake = false;

            GameObject portraitObj = new GameObject("PortraitImage", typeof(RectTransform));
            portraitObj.transform.SetParent(popupObj.transform, false);
            Image portraitImage = portraitObj.AddComponent<Image>();
            portraitImage.color = new Color(1f, 1f, 1f, 1f);
            portraitImage.preserveAspect = true;
            RectTransform portraitRect = portraitObj.GetComponent<RectTransform>();
            portraitRect.anchorMin = new Vector2(0f, 0.5f);
            portraitRect.anchorMax = new Vector2(0f, 0.5f);
            portraitRect.pivot = new Vector2(0f, 0.5f);
            portraitRect.sizeDelta = new Vector2(140f, 140f);
            portraitRect.anchoredPosition = new Vector2(15f, 0f);

            GameObject portraitVideoObj = new GameObject("PortraitVideoImage", typeof(RectTransform));
            portraitVideoObj.transform.SetParent(portraitObj.transform, false);
            RawImage portraitVideoImage = portraitVideoObj.AddComponent<RawImage>();
            RectTransform portraitVideoRect = portraitVideoObj.GetComponent<RectTransform>();
            portraitVideoRect.anchorMin = Vector2.zero;
            portraitVideoRect.anchorMax = Vector2.one;
            portraitVideoRect.offsetMin = Vector2.zero;
            portraitVideoRect.offsetMax = Vector2.zero;
            portraitVideoObj.SetActive(false);

            GameObject popupTextObj = new GameObject("RumorPopupText", typeof(RectTransform));
            popupTextObj.transform.SetParent(popupObj.transform, false);
            TextMeshProUGUI popupText = popupTextObj.AddComponent<TextMeshProUGUI>();
            popupText.alignment = TextAlignmentOptions.Center;
            popupText.fontSize = 18;
            popupText.text = "...";
            RectTransform popupTextRect = popupTextObj.GetComponent<RectTransform>();
            popupTextRect.anchorMin = new Vector2(0.40f, 0.08f);
            popupTextRect.anchorMax = new Vector2(0.94f, 0.92f);
            popupTextRect.offsetMin = Vector2.zero;
            popupTextRect.offsetMax = Vector2.zero;

            GameObject popupCloseObj = new GameObject("CloseButton", typeof(RectTransform));
            popupCloseObj.transform.SetParent(popupObj.transform, false);
            Image popupCloseBg = popupCloseObj.AddComponent<Image>();
            popupCloseBg.sprite = buttonSprite;
            popupCloseBg.type = Image.Type.Sliced;
            Button popupCloseButton = popupCloseObj.AddComponent<Button>();
            RectTransform popupCloseRect = popupCloseObj.GetComponent<RectTransform>();
            popupCloseRect.anchorMin = new Vector2(1f, 1f);
            popupCloseRect.anchorMax = new Vector2(1f, 1f);
            popupCloseRect.pivot = new Vector2(1f, 1f);
            popupCloseRect.sizeDelta = new Vector2(28f, 28f);
            popupCloseRect.anchoredPosition = new Vector2(-6f, -6f);

            AnimatedButtonFeedback popupCloseFeedback = popupCloseObj.AddComponent<AnimatedButtonFeedback>();
            SerializedObject serializedPopupCloseFeedback = new SerializedObject(popupCloseFeedback);
            serializedPopupCloseFeedback.FindProperty("_normalColor").colorValue = new Color(1.3f, 0.75f, 0.75f, 1f);
            serializedPopupCloseFeedback.FindProperty("_hoverColor").colorValue = new Color(1.5f, 0.85f, 0.85f, 1f);
            serializedPopupCloseFeedback.FindProperty("_pressedColor").colorValue = new Color(1.1f, 0.6f, 0.6f, 1f);
            serializedPopupCloseFeedback.ApplyModifiedProperties();

            GameObject popupCloseLabelObj = new GameObject("Label", typeof(RectTransform));
            popupCloseLabelObj.transform.SetParent(popupCloseObj.transform, false);
            TextMeshProUGUI popupCloseLabel = popupCloseLabelObj.AddComponent<TextMeshProUGUI>();
            popupCloseLabel.text = "X";
            popupCloseLabel.alignment = TextAlignmentOptions.Center;
            popupCloseLabel.fontSize = 16;
            popupCloseLabel.fontStyle = FontStyles.Bold;
            RectTransform popupCloseLabelRect = popupCloseLabelObj.GetComponent<RectTransform>();
            popupCloseLabelRect.anchorMin = Vector2.zero;
            popupCloseLabelRect.anchorMax = Vector2.one;
            popupCloseLabelRect.offsetMin = Vector2.zero;
            popupCloseLabelRect.offsetMax = Vector2.zero;

            // --- Wire DialogueMenuUI ---
            SerializedObject serializedMenu = new SerializedObject(menuUI);
            serializedMenu.FindProperty("_panelFader").objectReferenceValue = panelFader;
            serializedMenu.FindProperty("_npcNameText").objectReferenceValue = nameText;
            serializedMenu.FindProperty("_leaveButton").objectReferenceValue = leaveButton;
            serializedMenu.FindProperty("_listModeRoot").objectReferenceValue = scrollObj;
            serializedMenu.FindProperty("_optionsContainer").objectReferenceValue = contentRect;
            serializedMenu.FindProperty("_optionButtonPrefab").objectReferenceValue = savedButtonPrefab.GetComponent<Button>();
            serializedMenu.FindProperty("_carouselModeRoot").objectReferenceValue = carouselObj;
            serializedMenu.FindProperty("_carouselOptionGroup").objectReferenceValue = carouselCanvasGroup;
            serializedMenu.FindProperty("_carouselOptionButton").objectReferenceValue = carouselButton;
            serializedMenu.FindProperty("_carouselOptionLabel").objectReferenceValue = carouselLabel;
            serializedMenu.FindProperty("_carouselIndexText").objectReferenceValue = carouselIndexText;
            serializedMenu.FindProperty("_panelAnchoredPosition").vector2Value = Vector2.zero;
            serializedMenu.FindProperty("_panelSize").vector2Value = new Vector2(420f, 480f);
            serializedMenu.FindProperty("_rumorPopupFader").objectReferenceValue = popupFader;
            serializedMenu.FindProperty("_rumorPopupText").objectReferenceValue = popupText;
            serializedMenu.FindProperty("_rumorPopupCloseButton").objectReferenceValue = popupCloseButton;
            serializedMenu.FindProperty("_normalOptionColor").colorValue = Color.white;
            serializedMenu.FindProperty("_usedOptionColor").colorValue = new Color(0.5f, 0.5f, 0.5f, 1f);
            serializedMenu.FindProperty("_popupPortraitImage").objectReferenceValue = portraitImage;
            serializedMenu.FindProperty("_popupPortraitVideoImage").objectReferenceValue = portraitVideoImage;
            serializedMenu.FindProperty("_popupVideoPlayer").objectReferenceValue = popupVideoPlayer;
            serializedMenu.FindProperty("_clickAudioSource").objectReferenceValue = clickAudioSource;
            serializedMenu.ApplyModifiedProperties();

            Selection.activeGameObject = panelObj;
            EditorGUIUtility.PingObject(savedButtonPrefab);

            EditorUtility.DisplayDialog(
                "Success!",
                $"Dialogue Menu UI generated and wired automatically (List mode by default — toggle 'Use Carousel Mode' on DialogueMenuUI to switch).\n\nEvery panel element's position and size is freely editable afterward — drag in Scene view or edit the RectTransform in the Inspector.\n\nOption button prefab saved to:\n{buttonPrefabPath}",
                "Great");
        }

        private void GenerateFromExistingPrefab()
        {
            if (_sourcePrefab == null)
            {
                EditorUtility.DisplayDialog("No Prefab Assigned", "Assign a custom UI prefab or scene GameObject first.", "OK");
                return;
            }

            bool isAsset = PrefabUtility.GetPrefabAssetType(_sourcePrefab) != PrefabAssetType.NotAPrefab && !_sourcePrefab.scene.IsValid();
            GameObject rootInstance = isAsset ? (GameObject)PrefabUtility.InstantiatePrefab(_sourcePrefab) : _sourcePrefab;

            if (isAsset)
            {
                Canvas canvas = FindOrCreateCanvas();
                rootInstance.transform.SetParent(canvas.transform, false);
                Undo.RegisterCreatedObjectUndo(rootInstance, "Instantiate Custom Dialogue Menu UI");
            }

            DialogueMenuUI menuUI = rootInstance.GetComponent<DialogueMenuUI>();
            if (menuUI == null) menuUI = rootInstance.AddComponent<DialogueMenuUI>();
            if (rootInstance.GetComponent<CanvasGroup>() == null) rootInstance.AddComponent<CanvasGroup>();
            CanvasGroupFader rootFader = rootInstance.GetComponent<CanvasGroupFader>();
            if (rootFader == null) rootFader = rootInstance.AddComponent<CanvasGroupFader>();

            SerializedObject serializedMenu = new SerializedObject(menuUI);
            serializedMenu.FindProperty("_panelFader").objectReferenceValue = rootFader;
            serializedMenu.ApplyModifiedProperties();

            var fields = new List<UIElementAutoWirer.FieldTarget>
            {
                new UIElementAutoWirer.FieldTarget("_npcNameText", typeof(TextMeshProUGUI), new[] { "name" }),
                new UIElementAutoWirer.FieldTarget("_leaveButton", typeof(Button), new[] { "leave" }),
                new UIElementAutoWirer.FieldTarget("_listModeRoot", typeof(GameObject), new[] { "list" }),
                new UIElementAutoWirer.FieldTarget("_optionsContainer", typeof(RectTransform), new[] { "content" }),
                new UIElementAutoWirer.FieldTarget("_optionButtonPrefab", typeof(Button), new[] { "option" }, extractAsPrefab: true) { ExcludeHints = new[] { "carousel" } },
                new UIElementAutoWirer.FieldTarget("_carouselModeRoot", typeof(GameObject), new[] { "carousel" }),
                new UIElementAutoWirer.FieldTarget("_carouselOptionGroup", typeof(CanvasGroup), new[] { "carousel" }),
                new UIElementAutoWirer.FieldTarget("_carouselOptionButton", typeof(Button), new[] { "carousel", "option" }),
                new UIElementAutoWirer.FieldTarget("_carouselOptionLabel", typeof(TextMeshProUGUI), new[] { "carousel", "label" }),
                new UIElementAutoWirer.FieldTarget("_carouselIndexText", typeof(TextMeshProUGUI), new[] { "index" }),
                new UIElementAutoWirer.FieldTarget("_rumorPopupFader", typeof(CanvasGroupFader), new[] { "popup" }),
                new UIElementAutoWirer.FieldTarget("_rumorPopupText", typeof(TextMeshProUGUI), new[] { "popup" }) { ExcludeHints = new[] { "close" } },
                new UIElementAutoWirer.FieldTarget("_rumorPopupCloseButton", typeof(Button), new[] { "close" }),
                new UIElementAutoWirer.FieldTarget("_popupPortraitImage", typeof(Image), new[] { "portrait" }) { ExcludeHints = new[] { "video" } },
                new UIElementAutoWirer.FieldTarget("_popupPortraitVideoImage", typeof(RawImage), new[] { "portrait" }),
                new UIElementAutoWirer.FieldTarget("_popupVideoPlayer", typeof(VideoPlayer), new[] { "popup" }),
                new UIElementAutoWirer.FieldTarget("_clickAudioSource", typeof(AudioSource), new[] { "click" }),
            };

            UIElementAutoWirer.Result result = UIElementAutoWirer.AutoWire(rootInstance, menuUI, fields, OutputFolder);

            Selection.activeGameObject = rootInstance;
            EditorUtility.DisplayDialog("Custom Prefab Wired", UIElementAutoWirer.BuildSummaryMessage(result, fields.Count + 1), "Great");
        }

        private static Canvas FindOrCreateCanvas()
        {
            Canvas[] allCanvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include);
            foreach (Canvas candidate in allCanvases)
            {
                if (candidate.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                if (IsPartOfNpcHierarchy(candidate.transform)) continue;

                return candidate;
            }

            GameObject canvasObj = new GameObject("Canvas", typeof(RectTransform));
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();

            return canvas;
        }

        private static bool IsPartOfNpcHierarchy(Transform start)
        {
            Transform current = start;
            while (current != null)
            {
                if (current.GetComponent<NPCGossipMemory>() != null || current.GetComponent<NPCProximityGossip>() != null)
                {
                    return true;
                }
                current = current.parent;
            }

            return false;
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
    }
}
#endif