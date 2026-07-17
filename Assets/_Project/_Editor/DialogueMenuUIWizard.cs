#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using UnityEngine.Video;
using TMPro;
using Project.UI;
using Project.GamePlay;

namespace Project.CustomEditor
{
    /// <summary>
    /// One-click generator for the shared Dialogue Menu UI. Builds the whole hierarchy — name
    /// header, a flexible middle region (housing either the scrollable list or the carousel
    /// slot), and a Leave button pinned to the bottom — and wires DialogueMenuUI automatically.
    /// </summary>
    public class DialogueMenuUIWizard : EditorWindow
    {
        private const string OutputFolder = "Assets/NPC Creator/Generated UI";

        [MenuItem("Tools/NPC Creator/Generate Dialogue Menu UI")]
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
                "Builds the shared dialogue menu panel (NPC name header, options area, Leave button) and wires DialogueMenuUI automatically. Only one is needed for the whole game. Toggle List vs Carousel display mode afterward on the generated DialogueMenuUI component.",
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space();

            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("GENERATE DIALOGUE MENU UI", GUILayout.Height(40)))
            {
                GenerateDialogueMenuUI();
            }
            GUI.backgroundColor = Color.white;
        }

        private void GenerateDialogueMenuUI()
        {
            EnsureFolderExists(OutputFolder);
            Canvas canvas = FindOrCreateCanvas();

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
            panelBg.color = new Color(0.1f, 0.1f, 0.1f, 0.95f);

            panelObj.AddComponent<CanvasGroup>();
            CanvasGroupFader panelFader = panelObj.AddComponent<CanvasGroupFader>();
            DialogueMenuUI menuUI = panelObj.AddComponent<DialogueMenuUI>();

            // v13: Click sound source — 2D (non-spatial), since this is UI feedback, not a
            // world-positioned sound.
            AudioSource clickAudioSource = panelObj.AddComponent<AudioSource>();
            clickAudioSource.playOnAwake = false;
            clickAudioSource.spatialBlend = 0f;

            VerticalLayoutGroup panelLayout = panelObj.AddComponent<VerticalLayoutGroup>();
            panelLayout.padding = new RectOffset(16, 16, 16, 16);
            panelLayout.spacing = 10f;
            panelLayout.childControlHeight = true; // Required for preferredHeight/flexibleHeight below to have any effect.
            panelLayout.childControlWidth = true;
            panelLayout.childForceExpandWidth = true;
            panelLayout.childForceExpandHeight = false;

            // --- NPC name header ---
            GameObject nameObj = new GameObject("NpcNameText", typeof(RectTransform));
            nameObj.transform.SetParent(panelObj.transform, false);
            TextMeshProUGUI nameText = nameObj.AddComponent<TextMeshProUGUI>();
            nameText.text = "NPC Name";
            nameText.fontSize = 22;
            nameText.fontStyle = FontStyles.Bold;
            nameText.alignment = TextAlignmentOptions.Center;
            LayoutElement nameLayoutElement = nameObj.AddComponent<LayoutElement>();
            nameLayoutElement.preferredHeight = 32f;

            // --- Flexible middle region: houses EITHER the list OR the carousel slot, both
            // full-stretched inside it. flexibleHeight makes this absorb all leftover space,
            // which is what actually centers the options area and pins Leave to the bottom
            // regardless of panel size. ---
            GameObject middleContainer = new GameObject("MiddleContainer", typeof(RectTransform));
            middleContainer.transform.SetParent(panelObj.transform, false);
            LayoutElement middleLayoutElement = middleContainer.AddComponent<LayoutElement>();
            middleLayoutElement.minHeight = 200f;
            middleLayoutElement.flexibleHeight = 1f;

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
            buttonBg.color = new Color(0.25f, 0.25f, 0.25f, 1f);
            buttonTemplate.AddComponent<Button>();

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
            Object.DestroyImmediate(buttonTemplate);

            // ===== Carousel mode UI =====
            GameObject carouselObj = new GameObject("CarouselSlot", typeof(RectTransform));
            carouselObj.transform.SetParent(middleContainer.transform, false);
            RectTransform carouselFullRect = carouselObj.GetComponent<RectTransform>();
            carouselFullRect.anchorMin = Vector2.zero;
            carouselFullRect.anchorMax = Vector2.one;
            carouselFullRect.offsetMin = Vector2.zero;
            carouselFullRect.offsetMax = Vector2.zero;

            VerticalLayoutGroup carouselLayout = carouselObj.AddComponent<VerticalLayoutGroup>();
            carouselLayout.childAlignment = TextAnchor.MiddleCenter;
            carouselLayout.spacing = 8f;
            carouselLayout.childControlHeight = false;
            carouselLayout.childControlWidth = true;
            carouselLayout.childForceExpandWidth = true;
            carouselLayout.childForceExpandHeight = false;

            GameObject carouselButtonObj = new GameObject("CarouselOptionButton", typeof(RectTransform));
            carouselButtonObj.transform.SetParent(carouselObj.transform, false);
            RectTransform carouselButtonRect = carouselButtonObj.GetComponent<RectTransform>();
            carouselButtonRect.sizeDelta = new Vector2(0f, 60f);
            Image carouselButtonBg = carouselButtonObj.AddComponent<Image>();
            carouselButtonBg.color = new Color(0.25f, 0.25f, 0.25f, 1f);
            CanvasGroup carouselCanvasGroup = carouselButtonObj.AddComponent<CanvasGroup>();
            Button carouselButton = carouselButtonObj.AddComponent<Button>();
            LayoutElement carouselButtonLayoutElement = carouselButtonObj.AddComponent<LayoutElement>();
            carouselButtonLayoutElement.preferredHeight = 60f;

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
            TextMeshProUGUI carouselIndexText = carouselIndexObj.AddComponent<TextMeshProUGUI>();
            carouselIndexText.text = "1 / 1";
            carouselIndexText.alignment = TextAlignmentOptions.Center;
            carouselIndexText.fontSize = 14;
            carouselIndexText.color = new Color(1f, 1f, 1f, 0.6f);
            LayoutElement carouselIndexLayoutElement = carouselIndexObj.AddComponent<LayoutElement>();
            carouselIndexLayoutElement.preferredHeight = 20f;

            carouselObj.SetActive(false); // List mode is the default — DialogueMenuUI.Awake() also enforces this based on _useCarouselMode.

            // --- Leave button ---
            GameObject leaveObj = new GameObject("LeaveButton", typeof(RectTransform));
            leaveObj.transform.SetParent(panelObj.transform, false);
            Image leaveBg = leaveObj.AddComponent<Image>();
            leaveBg.color = new Color(0.4f, 0.15f, 0.15f, 1f);
            Button leaveButton = leaveObj.AddComponent<Button>();
            LayoutElement leaveLayoutElement = leaveObj.AddComponent<LayoutElement>();
            leaveLayoutElement.preferredHeight = 36f;

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

            // --- Rumor popup — a sibling of the main panel (same Canvas), so it visually
            // overlays it. Sized/positioned to sit centered on top, with a portrait square on
            // the left and an [X] close button.
            GameObject popupObj = new GameObject("RumorPopupPanel", typeof(RectTransform));
            popupObj.transform.SetParent(canvas.transform, false);
            RectTransform popupRect = popupObj.GetComponent<RectTransform>();
            popupRect.anchorMin = new Vector2(0.5f, 0.5f);
            popupRect.anchorMax = new Vector2(0.5f, 0.5f);
            popupRect.pivot = new Vector2(0.5f, 0.5f);
            popupRect.sizeDelta = new Vector2(440f, 220f); // v13: Widened to make room for the portrait square.
            popupRect.anchoredPosition = Vector2.zero;

            Image popupBg = popupObj.AddComponent<Image>();
            popupBg.color = new Color(0.12f, 0.12f, 0.16f, 0.97f);
            popupObj.AddComponent<CanvasGroup>();
            CanvasGroupFader popupFader = popupObj.AddComponent<CanvasGroupFader>();
            VideoPlayer popupVideoPlayer = popupObj.AddComponent<VideoPlayer>();
            popupVideoPlayer.playOnAwake = false;

            // v13: Portrait square (left side, fixed size regardless of popup size).
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
            portraitVideoObj.SetActive(false); // Hidden unless the current NPC has a Portrait Video assigned.

            GameObject popupTextObj = new GameObject("RumorPopupText", typeof(RectTransform));
            popupTextObj.transform.SetParent(popupObj.transform, false);
            TextMeshProUGUI popupText = popupTextObj.AddComponent<TextMeshProUGUI>();
            popupText.alignment = TextAlignmentOptions.Center;
            popupText.fontSize = 18;
            popupText.text = "...";
            RectTransform popupTextRect = popupTextObj.GetComponent<RectTransform>();
            // v13: Shifted right to make room for the portrait square.
            popupTextRect.anchorMin = new Vector2(0.40f, 0.08f);
            popupTextRect.anchorMax = new Vector2(0.94f, 0.92f);
            popupTextRect.offsetMin = Vector2.zero;
            popupTextRect.offsetMax = Vector2.zero;

            GameObject popupCloseObj = new GameObject("CloseButton", typeof(RectTransform));
            popupCloseObj.transform.SetParent(popupObj.transform, false);
            Image popupCloseBg = popupCloseObj.AddComponent<Image>();
            popupCloseBg.color = new Color(0.5f, 0.15f, 0.15f, 1f);
            Button popupCloseButton = popupCloseObj.AddComponent<Button>();
            RectTransform popupCloseRect = popupCloseObj.GetComponent<RectTransform>();
            popupCloseRect.anchorMin = new Vector2(1f, 1f);
            popupCloseRect.anchorMax = new Vector2(1f, 1f);
            popupCloseRect.pivot = new Vector2(1f, 1f);
            popupCloseRect.sizeDelta = new Vector2(28f, 28f);
            popupCloseRect.anchoredPosition = new Vector2(-6f, -6f);

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
                $"Dialogue Menu UI generated and wired automatically (List mode by default — toggle 'Use Carousel Mode' on DialogueMenuUI to switch).\n\nOption button prefab saved to:\n{buttonPrefabPath}",
                "Great");
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