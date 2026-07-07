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
    /// Configurable generator for the Reputation Bar UI. Builds the Canvas (if one doesn't
    /// already exist), the General bar, the Faction bar prefab, and the container that
    /// dynamically-created faction bars get parented under — then wires ReputationBarUI's
    /// fields automatically.
    ///
    /// v2: Converted from a single-click MenuItem into a configurable window (Row Width/Height
    /// editable before generation, defaulting to 215x20). Each generated bar's RectTransform
    /// remains freely resizable afterward in the Inspector — this also fixes a bug where both
    /// layout groups had childControlWidth enabled, which silently force-stretched every row
    /// to fill its parent regardless of whatever size was configured.
    /// </summary>
    public class ReputationUIWizard : EditorWindow
    {
        private const string OutputFolder = "Assets/NPC Creator/Generated UI";

        private float _rowWidth = 215f;
        private float _rowHeight = 20f;

        [MenuItem("Tools/NPC Creator/Generate Reputation Bar UI")]
        public static void ShowWindow()
        {
            ReputationUIWizard window = GetWindow<ReputationUIWizard>("Reputation UI Wizard");
            window.minSize = new Vector2(360, 240);
            window.Show();
        }

        private void OnGUI()
        {
            GUILayout.Label("Reputation Bar UI Generator", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Builds the Canvas (if needed), General bar, Faction bar prefab, and container automatically.", EditorStyles.miniLabel);
            EditorGUILayout.Space();

            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("Bar Dimensions", EditorStyles.boldLabel);
            _rowWidth = EditorGUILayout.FloatField("Row Width", _rowWidth);
            _rowHeight = EditorGUILayout.FloatField("Row Height", _rowHeight);
            EditorGUILayout.HelpBox("Applies to both the General bar and the Faction bar prefab (label and value fill the row automatically, so they scale with it). Each generated bar's RectTransform stays freely resizable afterward in the Inspector.", MessageType.None);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();
            GUILayout.FlexibleSpace();

            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("GENERATE REPUTATION BAR UI", GUILayout.Height(40)))
            {
                GenerateReputationBarUI();
            }
            GUI.backgroundColor = Color.white;
        }

        private void GenerateReputationBarUI()
        {
            EnsureFolderExists(OutputFolder);

            Canvas targetCanvas = FindOrCreateCanvas();

            GameObject rootObj = new GameObject("ReputationBarUI", typeof(RectTransform));
            rootObj.transform.SetParent(targetCanvas.transform, false);

            RectTransform rootRect = rootObj.GetComponent<RectTransform>();
            // v4: Anchored to the TOP-RIGHT corner instead of top-left.
            rootRect.anchorMin = new Vector2(1f, 1f);
            rootRect.anchorMax = new Vector2(1f, 1f);
            rootRect.pivot = new Vector2(1f, 1f);
            rootRect.anchoredPosition = new Vector2(-20f, -20f);
            rootRect.sizeDelta = new Vector2(_rowWidth + 20f, 220f);

            VerticalLayoutGroup rootLayout = rootObj.AddComponent<VerticalLayoutGroup>();
            rootLayout.spacing = 6f;
            rootLayout.childControlHeight = false;
            // v2: FIX — was true, which silently force-stretched every row's width to match
            // the container regardless of the row's own RectTransform size, making the
            // configured/edited size have no visible effect.
            rootLayout.childControlWidth = false;
            rootLayout.childForceExpandHeight = false;
            rootLayout.childForceExpandWidth = false;

            ReputationBarUI barUI = rootObj.AddComponent<ReputationBarUI>();

            ReputationBarRow generalRow = CreateBarRow(rootObj.transform, "GeneralReputationBar", "General");

            GameObject containerObj = new GameObject("FactionRowContainer", typeof(RectTransform));
            containerObj.transform.SetParent(rootObj.transform, false);
            VerticalLayoutGroup containerLayout = containerObj.AddComponent<VerticalLayoutGroup>();
            containerLayout.spacing = 4f;
            containerLayout.childControlHeight = false;
            // v2: Same fix as above.
            containerLayout.childControlWidth = false;
            containerLayout.childForceExpandHeight = false;
            containerLayout.childForceExpandWidth = false;

            ReputationBarRow factionTemplate = CreateBarRow(null, "FactionReputationBarRow", "Faction");
            string prefabPath = $"{OutputFolder}/FactionReputationBarRow.prefab";
            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(factionTemplate.gameObject, prefabPath);
            Object.DestroyImmediate(factionTemplate.gameObject);

            SerializedObject serializedBarUI = new SerializedObject(barUI);
            serializedBarUI.FindProperty("_generalRow").objectReferenceValue = generalRow;
            serializedBarUI.FindProperty("_factionRowPrefab").objectReferenceValue = savedPrefab.GetComponent<ReputationBarRow>();
            serializedBarUI.FindProperty("_factionRowContainer").objectReferenceValue = containerObj.transform;
            serializedBarUI.ApplyModifiedProperties();

            Selection.activeGameObject = rootObj;
            EditorGUIUtility.PingObject(savedPrefab);

            EditorUtility.DisplayDialog(
                "Success!",
                $"Reputation Bar UI generated and wired automatically ({_rowWidth}x{_rowHeight} bars).\n\nFaction row prefab saved to:\n{prefabPath}",
                "Great");
        }

        private ReputationBarRow CreateBarRow(Transform parent, string objectName, string defaultLabel)
        {
            GameObject rowObj = new GameObject(objectName, typeof(RectTransform));
            if (parent != null)
            {
                rowObj.transform.SetParent(parent, false);
            }

            // v2: Uses the configured Row Width/Height instead of a hardcoded value. This
            // RectTransform remains a normal, freely editable field afterward — nothing locks
            // it once generated.
            RectTransform rowRect = rowObj.GetComponent<RectTransform>();
            rowRect.sizeDelta = new Vector2(_rowWidth, _rowHeight);

            Image fillImage = rowObj.AddComponent<Image>();
            fillImage.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImage.color = new Color(0.2f, 0.6f, 1f, 1f);

            GameObject labelObj = new GameObject("Label", typeof(RectTransform));
            labelObj.transform.SetParent(rowObj.transform, false);
            TextMeshProUGUI labelText = labelObj.AddComponent<TextMeshProUGUI>();
            labelText.text = defaultLabel;
            labelText.alignment = TextAlignmentOptions.MidlineRight;
            labelText.fontSize = 14;
            RectTransform labelRect = labelObj.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(0.5f, 1f);
            // v6: Left = -130, Right = 100 as requested. In the Inspector, "Left" maps to
            // offsetMin.x and "Right" maps to -offsetMax.x.
            labelRect.offsetMin = new Vector2(-130f, 0f);
            labelRect.offsetMax = new Vector2(-100f, 0f);

            GameObject valueObj = new GameObject("Value", typeof(RectTransform));
            valueObj.transform.SetParent(rowObj.transform, false);
            TextMeshProUGUI valueText = valueObj.AddComponent<TextMeshProUGUI>();
            valueText.text = "+0";
            valueText.alignment = TextAlignmentOptions.Midline;
            valueText.fontSize = 14;
            valueText.fontStyle = FontStyles.Bold;
            RectTransform valueRect = valueObj.GetComponent<RectTransform>();
            valueRect.anchorMin = Vector2.zero;
            valueRect.anchorMax = Vector2.one;
            valueRect.offsetMin = Vector2.zero;
            valueRect.offsetMax = Vector2.zero;

            ReputationBarRow row = rowObj.AddComponent<ReputationBarRow>();
            SerializedObject serializedRow = new SerializedObject(row);
            serializedRow.FindProperty("_fillImage").objectReferenceValue = fillImage;
            serializedRow.FindProperty("_labelText").objectReferenceValue = labelText;
            serializedRow.FindProperty("_valueText").objectReferenceValue = valueText;
            serializedRow.ApplyModifiedProperties();

            return row;
        }

        private static Canvas FindOrCreateCanvas()
        {
            // v5: REAL FIX — render mode alone isn't enough to identify "the" HUD canvas,
            // because every NPC's Vendor shop canvas (NPC_Merchant_Market_Canvas) is ALSO a
            // Screen Space - Overlay canvas. Now explicitly skips any canvas that belongs to
            // an NPC's own hierarchy, regardless of render mode or naming.
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

        /// <summary>
        /// Walks up from the given transform looking for NPCGossipMemory or
        /// NPCProximityGossip anywhere in the parent chain — either one indicates this
        /// transform is part of an NPC's own hierarchy (e.g. its Vendor shop canvas), not a
        /// standalone HUD canvas.
        /// </summary>
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