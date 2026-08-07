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
    /// Role-Specific Add-ons, Locomotion) sections.
    ///
    /// FIX: every Locomotion-specific type reference (LocomotionAgent, LocomotionRoute,
    /// LocomotionRootMotionRelay) now goes through Type.GetType() reflection rather than a
    /// direct compile-time reference — Locomotion is a separately-sold, optional add-on, and
    /// this file (part of the CORE asset) must compile and function correctly whether or not a
    /// given buyer has installed it, same as the existing Vendor/Quest pattern in
    /// BuildCategoryMaps(). Previously this file would have failed to compile entirely for any
    /// buyer without the Locomotion add-on present.
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
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
        }

        private void OnDisable()
        {
            ClearEditorCache();
            EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
        }

        private void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            ClearEditorCache();
            Repaint();
        }

        private void OnSelectionChange()
        {
            AutoDetectTargetFromSelection();
            Repaint();
        }

        /// <summary>
        /// Builds the Core/Add-on category → component type map. Role-Specific Add-ons and
        /// Locomotion are both resolved via reflection since they're optional packs that may
        /// not exist in every project — missing ones are simply skipped.
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

            // FIX: previously referenced typeof(LocomotionAgent)/typeof(LocomotionRootMotionRelay)
            // directly — a hard compile-time dependency that would break this ENTIRE file (and
            // therefore the whole NPC Control Panel) for any buyer who owns the base asset but
            // NOT the separately-sold Locomotion add-on. Converted to the same reflection
            // pattern already used for Vendor/Quest above.
            List<Type> locomotionTypes = new List<Type>();
            Type locomotionAgentType = Type.GetType("TownsPeople.GamePlay.LocomotionAgent");
            Type locomotionRelayType = Type.GetType("TownsPeople.GamePlay.LocomotionRootMotionRelay");
            // v9: NPCFlockingBehavior — same reflection-safe pattern, since it's also part of
            // the separately-sold Locomotion add-on.
            Type locomotionFlockingType = Type.GetType("TownsPeople.GamePlay.NPCFlockingBehavior");
            if (locomotionAgentType != null) locomotionTypes.Add(locomotionAgentType);
            if (locomotionRelayType != null) locomotionTypes.Add(locomotionRelayType);
            if (locomotionFlockingType != null) locomotionTypes.Add(locomotionFlockingType);

            _addonCategories = new (string, Type[])[]
            {
                ("Optional Behavior Overrides", new[] { typeof(NPCWitnessReaction), typeof(NPCRumorIndicator) }),
                ("Role-Specific Add-ons", roleSpecificTypes.ToArray()),
                ("Locomotion", locomotionTypes.ToArray()),
            };
        }

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

            bool isLocomotionCategory = _topSection == TopSection.AddOns && categories[selectedIndex].Label == "Locomotion";
            if (isLocomotionCategory)
            {
                DrawLocomotionRouteButton();
            }

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

        /// <summary>
        /// FIX: rewritten to use reflection (Type.GetType + Component/SerializedObject) instead
        /// of the direct LocomotionAgent/LocomotionRoute type references this method previously
        /// had — same compile-safety reasoning as BuildCategoryMaps() above. If the Locomotion
        /// add-on isn't installed, this silently does nothing rather than failing to compile.
        /// </summary>
        private void DrawLocomotionRouteButton()
        {
            Type locomotionAgentType = Type.GetType("TownsPeople.GamePlay.LocomotionAgent");
            if (locomotionAgentType == null) return;

            Component agentComponent = _targetNpc.GetComponentInChildren(locomotionAgentType);

            GUIStyle cardStyle = TownsPeopleEditorTheme.CreateCardStyle(TownsPeopleEditorTheme.Panel);
            EditorGUILayout.BeginVertical(cardStyle);

            if (agentComponent == null)
            {
                EditorGUILayout.HelpBox("This NPC has no LocomotionAgent yet.", MessageType.None);
            }
            else
            {
                SerializedObject serializedAgent = new SerializedObject(agentComponent);
                SerializedProperty routeProp = serializedAgent.FindProperty("_assignedRoute");
                UnityEngine.Object routeObj = routeProp != null ? routeProp.objectReferenceValue : null;
                Component routeComponent = routeObj as Component;
                GameObject routeGameObject = routeComponent != null ? routeComponent.gameObject : null;

                if (routeGameObject == null)
                {
                    EditorGUILayout.HelpBox("No Locomotion Route assigned to this NPC yet — assign one on LocomotionAgent below.", MessageType.None);
                }
                else
                {
                    EditorGUILayout.LabelField("Assigned Route", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(routeGameObject.name, EditorStyles.miniLabel);
                    EditorGUILayout.Space(4);

                    if (GUILayout.Button("Show Locomotion Route", GUILayout.Height(28)))
                    {
                        Selection.activeGameObject = routeGameObject;
                        EditorGUIUtility.PingObject(routeGameObject);
                        SceneView.lastActiveSceneView?.FrameSelected();
                    }
                }
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(6);
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