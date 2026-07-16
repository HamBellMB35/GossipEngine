#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using Project.UI;
using Project.GamePlay;

namespace Project.CustomEditor
{
    /// <summary>
    /// One-click generator for the shared Dialogue Menu UI (name header, scrollable option
    /// list, Leave button). Builds the whole hierarchy and wires DialogueMenuUI automatically —
    /// no manual ScrollRect/Button construction required.
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
                "Builds the shared dialogue menu panel (NPC name header, scrollable option list, Leave button) and wires DialogueMenuUI automatically. Only one is needed for the whole game — it's reused and repopulated for whichever NPC the player talks to.",
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

            VerticalLayoutGroup panelLayout = panelObj.AddComponent<VerticalLayoutGroup>();
            panelLayout.padding = new RectOffset(16, 16, 16, 16);
            panelLayout.spacing = 10f;
            // v2: FIX — must be true for LayoutElement's preferredHeight/flexibleHeight to have
            // any effect at all. With this false (as it was), flexibleHeight on the scroll view
            // was silently ignored, which is what let empty space collect below the Leave button.
            panelLayout.childControlHeight = true;
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

            // --- Scrollable options list ---
            GameObject scrollObj = new GameObject("OptionsScrollView", typeof(RectTransform));
            scrollObj.transform.SetParent(panelObj.transform, false);
            ScrollRect scrollRect = scrollObj.AddComponent<ScrollRect>();
            Image scrollBg = scrollObj.AddComponent<Image>();
            scrollBg.color = new Color(0f, 0f, 0f, 0.15f);
            Mask scrollMask = scrollObj.AddComponent<Mask>();
            scrollMask.showMaskGraphic = true;
            LayoutElement scrollLayoutElement = scrollObj.AddComponent<LayoutElement>();
            // v2: FIX — was a fixed preferredHeight, which left leftover panel space below the
            // Leave button instead of the button sitting flush at the bottom. minHeight keeps
            // it from collapsing; flexibleHeight makes it absorb all remaining space, pushing
            // the Leave button (added after it) to the true bottom of the panel.
            scrollLayoutElement.minHeight = 200f;
            scrollLayoutElement.flexibleHeight = 1f;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.viewport = scrollObj.GetComponent<RectTransform>();

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

            // --- Option button template (built once, saved as a reusable prefab asset) ---
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

            // --- Wire DialogueMenuUI ---
            SerializedObject serializedMenu = new SerializedObject(menuUI);
            serializedMenu.FindProperty("_panelFader").objectReferenceValue = panelFader;
            serializedMenu.FindProperty("_npcNameText").objectReferenceValue = nameText;
            serializedMenu.FindProperty("_optionsContainer").objectReferenceValue = contentRect;
            serializedMenu.FindProperty("_optionButtonPrefab").objectReferenceValue = savedButtonPrefab.GetComponent<Button>();
            serializedMenu.FindProperty("_leaveButton").objectReferenceValue = leaveButton;
            serializedMenu.ApplyModifiedProperties();

            Selection.activeGameObject = panelObj;
            EditorGUIUtility.PingObject(savedButtonPrefab);

            EditorUtility.DisplayDialog(
                "Success!",
                $"Dialogue Menu UI generated and wired automatically.\n\nOption button prefab saved to:\n{buttonPrefabPath}",
                "Great");
        }

        /// <summary>
        /// Finds a genuine standalone HUD canvas, skipping any canvas that belongs to an NPC's
        /// own hierarchy (e.g. the Vendor shop canvas, which is also Screen Space - Overlay).
        /// </summary>
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