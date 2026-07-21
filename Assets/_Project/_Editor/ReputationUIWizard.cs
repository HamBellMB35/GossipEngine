#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using TownsPeople.UI;
using TownsPeople.GamePlay;

namespace TownsPeople.CustomEditor
{
    /// <summary>
    /// Configurable generator for the Reputation Bar UI. Builds the Canvas (if one doesn't
    /// already exist), the General bar, the Faction bar prefab, and the container that
    /// dynamically-created faction bars get parented under — then wires ReputationBarUI's
    /// fields automatically.
    ///
    /// Also supports "Use Existing Prefab" mode: a developer can drop in their own custom UI
    /// prefab and have UIElementAutoWirer scan it for matching elements by name + component
    /// type, wiring ReputationBarUI automatically instead of generating UI from scratch.
    /// </summary>
    // v8: Bar backgrounds now generated through ProceduralUISprites (rounded corners, border,
    // vertical gradient) instead of the flat built-in UISprite — matching the visual language
    // of the Dialogue Menu / [E] prompt work. Both the General row and the Faction row prefab
    // share the SAME generated sprite asset (one CreateRoundedRectSprite call, reused for
    // both), guaranteeing they're visually identical by construction rather than just similar.
    // v9: Added "Use Existing Prefab" mode — see UIElementAutoWirer.
    public class ReputationUIWizard : EditorWindow
    {
        private const string OutputFolder = "Assets/NPC Creator/Generated UI";

        private enum WizardMode { GenerateNew, UseExistingPrefab }
        private WizardMode _mode = WizardMode.GenerateNew;
        private GameObject _sourcePrefab;

        private static readonly string[] GeneralRowHints = { "general" };
        private static readonly string[] FactionRowHints = { "faction" };

        private float _rowWidth = 215f;
        private float _rowHeight = 20f;

        [Header("Visual Style")]
        private float _cornerRadius = 8f;
        private float _borderThickness = 2f;
        private Color _borderColor = new Color(0.80f, 0.66f, 0.32f, 1f);
        private Color _fillTop = new Color(0.22f, 0.55f, 0.85f, 1f);
        private Color _fillBottom = new Color(0.12f, 0.32f, 0.55f, 1f);

        [MenuItem("Tools/TownsPeople/Generate Reputation Bar UI")]
        public static void ShowWindow()
        {
            ReputationUIWizard window = GetWindow<ReputationUIWizard>("Reputation UI Wizard");
            window.minSize = new Vector2(360, 340);
            window.Show();
        }

        private void OnGUI()
        {
            GUILayout.Label("Reputation Bar UI Generator", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Builds the Canvas (if needed), General bar, Faction bar prefab, and container automatically.", EditorStyles.miniLabel);
            EditorGUILayout.Space();

            _mode = (WizardMode)GUILayout.Toolbar((int)_mode, new[] { "Generate New", "Use Existing Prefab" });
            EditorGUILayout.Space();

            if (_mode == WizardMode.UseExistingPrefab)
            {
                EditorGUILayout.HelpBox(
                    "Drop in your own UI prefab (or a scene GameObject) and the wizard scans it for a " +
                    "matching Image/Text setup, wiring ReputationBarUI automatically. Name your elements " +
                    "with these keywords for the best match rate:\n" +
                    "\u2022 General bar row: contains \"General\"\n" +
                    "\u2022 Faction bar template row: contains \"Faction\" (extracted as its own prefab)\n" +
                    "\u2022 Faction rows container: contains \"Container\"\n" +
                    "\u2022 Each row's fill Image: contains \"Fill\" or \"Bar\"\n" +
                    "\u2022 Each row's label/value text: contain \"Label\" / \"Value\" respectively",
                    MessageType.Info);
                _sourcePrefab = (GameObject)EditorGUILayout.ObjectField("Custom UI Prefab / GameObject", _sourcePrefab, typeof(GameObject), true);
            }
            else
            {
                EditorGUILayout.BeginVertical("box");
                GUILayout.Label("Bar Dimensions", EditorStyles.boldLabel);
                _rowWidth = EditorGUILayout.FloatField("Row Width", _rowWidth);
                _rowHeight = EditorGUILayout.FloatField("Row Height", _rowHeight);
                EditorGUILayout.HelpBox("Applies to both the General bar and the Faction bar prefab (label and value fill the row automatically, so they scale with it). Each generated bar's RectTransform stays freely resizable afterward in the Inspector.", MessageType.None);
                EditorGUILayout.EndVertical();

                EditorGUILayout.Space();

                EditorGUILayout.BeginVertical("box");
                GUILayout.Label("Visual Style", EditorStyles.boldLabel);
                _cornerRadius = EditorGUILayout.Slider("Corner Radius", _cornerRadius, 0f, 16f);
                _borderThickness = EditorGUILayout.Slider("Border Thickness", _borderThickness, 0f, 6f);
                _borderColor = EditorGUILayout.ColorField("Border Color", _borderColor);
                _fillTop = EditorGUILayout.ColorField("Fill (Top)", _fillTop);
                _fillBottom = EditorGUILayout.ColorField("Fill (Bottom)", _fillBottom);
                EditorGUILayout.HelpBox("General and Faction bars share the same generated sprite — they're guaranteed to look identical.", MessageType.None);
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space();
            GUILayout.FlexibleSpace();

            GUI.backgroundColor = Color.green;
            string buttonLabel = _mode == WizardMode.GenerateNew ? "GENERATE REPUTATION BAR UI" : "WIRE CUSTOM REPUTATION BAR UI";
            if (GUILayout.Button(buttonLabel, GUILayout.Height(40)))
            {
                if (_mode == WizardMode.GenerateNew) GenerateReputationBarUI();
                else GenerateFromExistingPrefab();
            }
            GUI.backgroundColor = Color.white;
        }

        private void GenerateReputationBarUI()
        {
            EnsureFolderExists(OutputFolder);

            Canvas targetCanvas = FindOrCreateCanvas();

            // One shared sprite, used for both bars below — the actual mechanism that
            // guarantees General and Faction bars are visually identical, not just similarly coded.
            Sprite barSprite = ProceduralUISprites.CreateRoundedRectSprite(
                $"{OutputFolder}/ReputationBarBackground.png", 64, _cornerRadius, _borderThickness, _borderColor, _fillTop, _fillBottom);

            GameObject rootObj = new GameObject("ReputationBarUI", typeof(RectTransform));
            rootObj.transform.SetParent(targetCanvas.transform, false);

            RectTransform rootRect = rootObj.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(1f, 1f);
            rootRect.anchorMax = new Vector2(1f, 1f);
            rootRect.pivot = new Vector2(1f, 1f);
            rootRect.anchoredPosition = new Vector2(-20f, -20f);
            rootRect.sizeDelta = new Vector2(_rowWidth + 20f, 220f);

            VerticalLayoutGroup rootLayout = rootObj.AddComponent<VerticalLayoutGroup>();
            rootLayout.spacing = 6f;
            rootLayout.childControlHeight = false;
            rootLayout.childControlWidth = false;
            rootLayout.childForceExpandHeight = false;
            rootLayout.childForceExpandWidth = false;

            ReputationBarUI barUI = rootObj.AddComponent<ReputationBarUI>();

            ReputationBarRow generalRow = CreateBarRow(rootObj.transform, "GeneralReputationBar", "General", barSprite);

            GameObject containerObj = new GameObject("FactionRowContainer", typeof(RectTransform));
            containerObj.transform.SetParent(rootObj.transform, false);
            VerticalLayoutGroup containerLayout = containerObj.AddComponent<VerticalLayoutGroup>();
            containerLayout.spacing = 4f;
            containerLayout.childControlHeight = false;
            containerLayout.childControlWidth = false;
            containerLayout.childForceExpandHeight = false;
            containerLayout.childForceExpandWidth = false;

            ReputationBarRow factionTemplate = CreateBarRow(null, "FactionReputationBarRow", "Faction", barSprite);
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

        /// <summary>
        /// v9: Builds ReputationBarUI on a developer-supplied custom prefab/GameObject instead
        /// of generating one from scratch. Scans for a "General"-named row and a "Faction"-named
        /// row (adding ReputationBarRow to each if not already present), wires each row's own
        /// Fill Image / Label / Value text, extracts the faction row as its own prefab asset
        /// (same role as the Faction Bar Prefab in Generate New mode), and wires the top-level
        /// ReputationBarUI fields. Reports exactly what it could and couldn't find rather than
        /// failing silently on a naming mismatch.
        /// </summary>
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
                Canvas targetCanvas = FindOrCreateCanvas();
                rootInstance.transform.SetParent(targetCanvas.transform, false);
                Undo.RegisterCreatedObjectUndo(rootInstance, "Instantiate Custom Reputation Bar UI");
            }

            ReputationBarUI barUI = rootInstance.GetComponent<ReputationBarUI>();
            if (barUI == null) barUI = rootInstance.AddComponent<ReputationBarUI>();

            Transform generalRowTransform = EnsureRowComponent(rootInstance.transform, GeneralRowHints);
            Transform factionRowTransform = EnsureRowComponent(rootInstance.transform, FactionRowHints);

            var topFields = new List<UIElementAutoWirer.FieldTarget>
            {
                new UIElementAutoWirer.FieldTarget("_generalRow", typeof(ReputationBarRow), GeneralRowHints),
                new UIElementAutoWirer.FieldTarget("_factionRowContainer", typeof(Transform), new[] { "container" }),
            };
            UIElementAutoWirer.Result topResult = UIElementAutoWirer.AutoWire(rootInstance, barUI, topFields, OutputFolder);

            int wiredRowFields = 0, totalRowFields = 0;

            // Wire each row's own Fill Image / Label / Value BEFORE extracting the faction row
            // as a prefab, since extraction removes it from the live hierarchy.
            if (generalRowTransform != null)
            {
                ReputationBarRow generalRow = generalRowTransform.GetComponent<ReputationBarRow>();
                var rowFields = BuildRowFieldTargets();
                var rowResult = UIElementAutoWirer.AutoWire(generalRowTransform.gameObject, generalRow, rowFields, OutputFolder);
                wiredRowFields += rowResult.Wired.Count;
                totalRowFields += rowFields.Count;
            }

            if (factionRowTransform != null)
            {
                ReputationBarRow factionRow = factionRowTransform.GetComponent<ReputationBarRow>();
                var rowFields = BuildRowFieldTargets();
                var rowResult = UIElementAutoWirer.AutoWire(factionRowTransform.gameObject, factionRow, rowFields, OutputFolder);
                wiredRowFields += rowResult.Wired.Count;
                totalRowFields += rowFields.Count;

                var extractField = new List<UIElementAutoWirer.FieldTarget>
                {
                    new UIElementAutoWirer.FieldTarget("_factionRowPrefab", typeof(ReputationBarRow), FactionRowHints, extractAsPrefab: true)
                };
                var extractResult = UIElementAutoWirer.AutoWire(rootInstance, barUI, extractField, OutputFolder);
                topResult.Wired.AddRange(extractResult.Wired);
                topResult.Missing.AddRange(extractResult.Missing);
            }
            else
            {
                topResult.Missing.Add("_factionRowPrefab (no child matched \"Faction\")");
            }

            Selection.activeGameObject = rootInstance;
            int totalFields = topFields.Count + 1 + totalRowFields;
            int wiredFields = topResult.Wired.Count + wiredRowFields;
            string summary = $"Wired: {wiredFields}/{totalFields}";
            if (topResult.Missing.Count > 0)
            {
                summary += $"\n\nCould not find a match for:\n\u2022 {string.Join("\n\u2022 ", topResult.Missing.Distinct())}\n\nAssign these manually on ReputationBarUI in the Inspector.";
            }
            EditorUtility.DisplayDialog("Custom Prefab Wired", summary, "Great");
        }

        private List<UIElementAutoWirer.FieldTarget> BuildRowFieldTargets()
        {
            return new List<UIElementAutoWirer.FieldTarget>
            {
                new UIElementAutoWirer.FieldTarget("_fillImage", typeof(Image), new[] { "fill" }),
                new UIElementAutoWirer.FieldTarget("_labelText", typeof(TextMeshProUGUI), new[] { "label" }),
                new UIElementAutoWirer.FieldTarget("_valueText", typeof(TextMeshProUGUI), new[] { "value" }),
            };
        }

        /// <summary>
        /// Finds a child whose name matches the given hints and ensures it carries a
        /// ReputationBarRow component (adding one if missing) so the top-level type+name scan
        /// can match it.
        /// </summary>
        private static Transform EnsureRowComponent(Transform root, string[] nameHints)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t == root) continue;
                string nameLower = t.name.ToLowerInvariant();
                if (nameHints.All(h => nameLower.Contains(h.ToLowerInvariant())))
                {
                    if (t.GetComponent<ReputationBarRow>() == null) t.gameObject.AddComponent<ReputationBarRow>();
                    return t;
                }
            }
            return null;
        }

        private ReputationBarRow CreateBarRow(Transform parent, string objectName, string defaultLabel, Sprite barSprite)
        {
            GameObject rowObj = new GameObject(objectName, typeof(RectTransform));
            if (parent != null)
            {
                rowObj.transform.SetParent(parent, false);
            }

            RectTransform rowRect = rowObj.GetComponent<RectTransform>();
            rowRect.sizeDelta = new Vector2(_rowWidth, _rowHeight);

            Image fillImage = rowObj.AddComponent<Image>();
            fillImage.sprite = barSprite;
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImage.color = Color.white; // Gradient/border baked into the sprite — tint stays neutral.

            GameObject labelObj = new GameObject("Label", typeof(RectTransform));
            labelObj.transform.SetParent(rowObj.transform, false);
            TextMeshProUGUI labelText = labelObj.AddComponent<TextMeshProUGUI>();
            labelText.text = defaultLabel;
            labelText.alignment = TextAlignmentOptions.MidlineRight;
            labelText.fontSize = 14;
            RectTransform labelRect = labelObj.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(0.5f, 1f);
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