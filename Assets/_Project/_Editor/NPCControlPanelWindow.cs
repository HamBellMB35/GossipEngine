#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using TownsPeople.EditorTools;
using TownsPeople.GamePlay;
using TownsPeople.UI;
using UnityEditor;
using UnityEngine;

namespace TownsPeople.CustomEditor
{
    /// <summary>
    /// A categorized, single-window alternative to scrolling through every component's
    /// separate Inspector block on an NPC. Select any NPC (root or a child of one) and this
    /// window auto-targets it, grouping its components into CORE (Identity & State,
    /// Perception, Presentation, Infrastructure) and ADD-ONS (Optional Behavior Overrides,
    /// Role-Specific Add-ons) sections — click a category button to see only that section's
    /// components. Every field already available in the normal Inspector remains available
    /// here (each card renders via Editor.CreateEditor(component).OnInspectorGUI(), the same
    /// mechanism Unity's own Inspector uses) — this changes how it's organized, never what's
    /// exposed, and automatically picks up any future fields added to these components without
    /// needing this window updated.
    ///
    /// Visual style (rounded cards/buttons, editable colors) comes from
    /// TownsPeopleEditorTheme — kept separate so the same look can be shared across other
    /// wizards later for one consistent tool identity.
    /// </summary>
    public class NPCControlPanelWindow : EditorWindow
    {
        private enum TopSection { Core, AddOns }

        private GameObject _targetNpc;
        private TopSection _topSection = TopSection.Core;
        private int _selectedCoreCategory;
        private int _selectedAddonCategory;
        private bool _showTheme;
        private Vector2 _scrollPosition;

        private readonly Dictionary<Component, Editor> _cachedEditors = new Dictionary<Component, Editor>();

        private (string Label, Type[] Types)[] _coreCategories;
        private (string Label, Type[] Types)[] _addonCategories;

        [MenuItem("Tools/TownsPeople/NPC Control Panel")]
        public static void ShowWindow()
        {
            NPCControlPanelWindow window = GetWindow<NPCControlPanelWindow>("NPC Control Panel");
            window.minSize = new Vector2(420, 500);
            window.Show();
        }

        private void OnEnable()
        {
            BuildCategoryMaps();
            AutoDetectTargetFromSelection();
        }

        private void OnDisable()
        {
            ClearEditorCache();
        }

        private void OnSelectionChange()
        {
            AutoDetectTargetFromSelection();
            Repaint();
        }

        /// <summary>
        /// Builds the Core/Add-on category → component type map. Role-Specific Add-ons are
        /// resolved via reflection since VendorComponentAddon/QuestComponentAddon are optional
        /// packs that may not exist in every project — missing ones are simply skipped, same
        /// pattern used by NPCCreatorWizardWindow.
        /// </summary>
        private void BuildCategoryMaps()
        {
            _coreCategories = new (string, Type[])[]
            {
                ("Identity & State", new[] { typeof(NPCGossipMemory), typeof(NPCGreetingResponder), typeof(NPCReputationOpinion) }),
                ("Perception", new[] { typeof(SphereCollider), typeof(NPCProximityGossip) }),
                ("Presentation", new[] { typeof(NPCAnimationBridge), typeof(Animator), typeof(AudioSource), typeof(NPCNameplate), typeof(NPCSpeechBubble) }),
                ("Infrastructure", new[] { typeof(NpcAddonRegistry) }),
            };

            List<Type> roleSpecificTypes = new List<Type>();
            Type vendorType = Type.GetType("TownsPeople.GamePlay.VendorComponentAddon");
            Type questType = Type.GetType("TownsPeople.GamePlay.QuestComponentAddon");
            if (vendorType != null) roleSpecificTypes.Add(vendorType);
            if (questType != null) roleSpecificTypes.Add(questType);

            _addonCategories = new (string, Type[])[]
            {
                ("Optional Behavior Overrides", new[] { typeof(NPCWitnessReaction), typeof(NPCRumorIndicator) }),
                ("Role-Specific Add-ons", roleSpecificTypes.ToArray()),
            };
        }

        /// <summary>
        /// If the current Selection is (or is a child of) an NPC — detected by the presence of
        /// NPCGossipMemory or NPCGreetingResponder anywhere in its hierarchy — auto-targets
        /// this window at that NPC's root. Leaves the target unchanged if the selection doesn't
        /// look like an NPC, so selecting something unrelated (e.g. a UI element while tuning
        /// this NPC) doesn't lose your place.
        /// </summary>
        private void AutoDetectTargetFromSelection()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null) return;

            NPCGossipMemory memory = selected.GetComponentInParent<NPCGossipMemory>();
            if (memory != null)
            {
                SetTarget(memory.gameObject);
                return;
            }

            NPCGreetingResponder responder = selected.GetComponentInParent<NPCGreetingResponder>();
            if (responder != null)
            {
                SetTarget(responder.gameObject);
            }
        }

        private void SetTarget(GameObject npc)
        {
            if (_targetNpc == npc) return;
            _targetNpc = npc;
            ClearEditorCache();
        }

        private void ClearEditorCache()
        {
            foreach (Editor editor in _cachedEditors.Values)
            {
                if (editor != null) DestroyImmediate(editor);
            }
            _cachedEditors.Clear();
        }

        private void OnGUI()
        {
            DrawHeader();

            if (_targetNpc == null)
            {
                EditorGUILayout.HelpBox("Select an NPC in the Hierarchy (or any of its children) to inspect it here.", MessageType.Info);
                DrawThemeSection();
                return;
            }

            DrawTopSectionToggle();
            EditorGUILayout.Space(4);
            DrawCategoryButtons();
            EditorGUILayout.Space(6);

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            DrawSelectedCategoryContent();
            EditorGUILayout.EndScrollView();

            DrawThemeSection();
        }

        private void DrawHeader()
        {
            GUIStyle headerStyle = TownsPeopleEditorTheme.CreateCardStyle(TownsPeopleEditorTheme.Background);
            EditorGUILayout.BeginVertical(headerStyle);

            GUILayout.Label("NPC Control Panel", EditorStyles.boldLabel);

            GameObject newTarget = (GameObject)EditorGUILayout.ObjectField("Target NPC", _targetNpc, typeof(GameObject), true);
            if (newTarget != _targetNpc)
            {
                SetTarget(newTarget);
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(6);
        }

        private void DrawTopSectionToggle()
        {
            EditorGUILayout.BeginHorizontal();

            if (DrawToggleButton("CORE", _topSection == TopSection.Core))
            {
                _topSection = TopSection.Core;
            }
            if (DrawToggleButton("ADD-ONS", _topSection == TopSection.AddOns))
            {
                _topSection = TopSection.AddOns;
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawCategoryButtons()
        {
            (string Label, Type[] Types)[] categories = _topSection == TopSection.Core ? _coreCategories : _addonCategories;
            int selectedIndex = _topSection == TopSection.Core ? _selectedCoreCategory : _selectedAddonCategory;

            EditorGUILayout.BeginHorizontal();
            for (int i = 0; i < categories.Length; i++)
            {
                if (DrawToggleButton(categories[i].Label, selectedIndex == i))
                {
                    if (_topSection == TopSection.Core) _selectedCoreCategory = i;
                    else _selectedAddonCategory = i;
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>A single rounded, theme-colored toggle button. Returns true the frame it's clicked.</summary>
        private bool DrawToggleButton(string label, bool selected)
        {
            GUIStyle style = TownsPeopleEditorTheme.CreateCategoryButtonStyle(selected);
            return GUILayout.Button(label, style, GUILayout.ExpandWidth(true));
        }

        private void DrawSelectedCategoryContent()
        {
            (string Label, Type[] Types)[] categories = _topSection == TopSection.Core ? _coreCategories : _addonCategories;
            int selectedIndex = _topSection == TopSection.Core ? _selectedCoreCategory : _selectedAddonCategory;

            if (selectedIndex < 0 || selectedIndex >= categories.Length) return;

            Type[] types = categories[selectedIndex].Types;
            List<Component> found = new List<Component>();

            foreach (Type type in types)
            {
                if (type == null) continue;
                found.AddRange(_targetNpc.GetComponentsInChildren(type, true));
            }

            if (found.Count == 0)
            {
                EditorGUILayout.HelpBox($"'{_targetNpc.name}' has no components in this category.", MessageType.None);
                return;
            }

            foreach (Component component in found)
            {
                DrawComponentCard(component);
            }
        }

        private void DrawComponentCard(Component component)
        {
            if (component == null) return;

            if (!_cachedEditors.TryGetValue(component, out Editor editor) || editor == null)
            {
                editor = Editor.CreateEditor(component);
                _cachedEditors[component] = editor;
            }

            GUIStyle cardStyle = TownsPeopleEditorTheme.CreateCardStyle(TownsPeopleEditorTheme.Panel);
            EditorGUILayout.BeginVertical(cardStyle);

            GUILayout.Label(component.GetType().Name, EditorStyles.boldLabel);
            EditorGUILayout.Space(2);

            editor.OnInspectorGUI();

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(6);
        }

        private void DrawThemeSection()
        {
            EditorGUILayout.Space(8);
            _showTheme = EditorGUILayout.Foldout(_showTheme, "Tool Theme", true);
            if (!_showTheme) return;

            GUIStyle themeCard = TownsPeopleEditorTheme.CreateCardStyle(TownsPeopleEditorTheme.Background);
            EditorGUILayout.BeginVertical(themeCard);

            TownsPeopleEditorTheme.Accent = EditorGUILayout.ColorField("Accent (selected category)", TownsPeopleEditorTheme.Accent);
            TownsPeopleEditorTheme.Panel = EditorGUILayout.ColorField("Panel (card background)", TownsPeopleEditorTheme.Panel);
            TownsPeopleEditorTheme.PanelSelected = EditorGUILayout.ColorField("Panel (selected button)", TownsPeopleEditorTheme.PanelSelected);
            TownsPeopleEditorTheme.Background = EditorGUILayout.ColorField("Window Background", TownsPeopleEditorTheme.Background);
            TownsPeopleEditorTheme.Border = EditorGUILayout.ColorField("Border", TownsPeopleEditorTheme.Border);
            TownsPeopleEditorTheme.CornerRadius = EditorGUILayout.Slider("Corner Radius", TownsPeopleEditorTheme.CornerRadius, 0f, 16f);

            EditorGUILayout.Space(4);
            if (GUILayout.Button("Reset to Default Theme"))
            {
                TownsPeopleEditorTheme.ResetToDefaults();
            }

            EditorGUILayout.EndVertical();
        }
    }
}
#endif