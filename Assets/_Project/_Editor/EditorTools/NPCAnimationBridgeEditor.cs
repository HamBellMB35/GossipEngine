#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using TownsPeople.GamePlay;

namespace TownsPeople.EditorTools
{
    /// <summary>
    /// Custom Inspector for NPCAnimationBridge. Every field draws normally EXCEPT Default Idle
    /// States, which gets a dedicated section below: a proper reorderable list of real Animator
    /// state dropdowns (Add/Remove buttons included), instead of relying on List&lt;string&gt;'s
    /// automatic per-element PropertyDrawer behavior — guaranteed-correct dropdown rendering
    /// rather than a hopeful attribute side-effect, matching the polish level already used for
    /// LocomotionRoute's waypoint list elsewhere in this project.
    ///
    /// FIX: TownsPeople.CustomEditor's own last namespace segment shadows UnityEditor's
    /// CustomEditor attribute and Editor base class (same collision already fixed on
    /// PlayerDeedBroadcasterEditor and LocomotionRouteEditor) — both are fully qualified below
    /// instead of relying on the `using UnityEditor;` import for these two specifically.
    /// </summary>
    [UnityEditor.CustomEditor(typeof(NPCAnimationBridge))]
    public class NPCAnimationBridgeEditor : UnityEditor.Editor
    {
        private SerializedProperty _defaultIdleStatesProp;

        private void OnEnable()
        {
            _defaultIdleStatesProp = serializedObject.FindProperty("_defaultIdleStates");

            // v2: Auto-fills the Animator field the instant an EXISTING NPC (one that already
            // has this component, added before Reset()'s auto-fill existed) is selected/viewed —
            // no manual drag, no need to right-click "Reset" on every NPC individually. Uses the
            // exact same resolution order as NPCAnimationBridge's own Reset()/Awake().
            SerializedProperty animatorProp = serializedObject.FindProperty("_animator");
            if (animatorProp.objectReferenceValue == null)
            {
                NPCAnimationBridge bridge = (NPCAnimationBridge)target;
                Animator autoResolved = bridge.GetComponent<Animator>();
                if (autoResolved == null)
                {
                    autoResolved = bridge.GetComponentInChildren<Animator>();
                }

                if (autoResolved != null)
                {
                    animatorProp.objectReferenceValue = autoResolved;
                    serializedObject.ApplyModifiedProperties();
                }
            }
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // Draw every field EXCEPT _defaultIdleStates using Unity's normal default
            // rendering — this keeps every other field (Animator, Speed/Turn parameters,
            // tooltips, headers, etc.) working exactly as authored, with zero duplication.
            SerializedProperty iterator = serializedObject.GetIterator();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (iterator.propertyPath == "_defaultIdleStates") continue;
                EditorGUILayout.PropertyField(iterator, true);
            }

            EditorGUILayout.Space();
            DrawDefaultIdleStatesList();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawDefaultIdleStatesList()
        {
            EditorGUILayout.LabelField("Default Idle States", EditorStyles.boldLabel);

            AnimatorController controller = AnimatorStateNameDrawer.ResolveControllerStatic(_defaultIdleStatesProp, "_animator");
            List<string> stateNames = controller != null
                ? AnimatorStateNameDrawer.GetAllStateNames(controller)
                : new List<string>();

            if (controller == null)
            {
                EditorGUILayout.HelpBox("Assign an Animator above to populate this list from its real states.", MessageType.Info);
            }
            else if (stateNames.Count == 0)
            {
                EditorGUILayout.HelpBox("The assigned Animator Controller has no states yet.", MessageType.Info);
            }

            for (int i = 0; i < _defaultIdleStatesProp.arraySize; i++)
            {
                SerializedProperty element = _defaultIdleStatesProp.GetArrayElementAtIndex(i);

                EditorGUILayout.BeginHorizontal();

                if (stateNames.Count > 0)
                {
                    int currentIndex = stateNames.IndexOf(element.stringValue);
                    List<string> displayOptions = new List<string>(stateNames);
                    int placeholderCount = 0;

                    if (currentIndex < 0)
                    {
                        displayOptions.Insert(0, string.IsNullOrEmpty(element.stringValue)
                            ? "(none selected)"
                            : $"{element.stringValue}  (not found in controller)");
                        currentIndex = 0;
                        placeholderCount = 1;
                    }

                    EditorGUI.BeginChangeCheck();
                    int selected = EditorGUILayout.Popup(currentIndex, displayOptions.ToArray());
                    if (EditorGUI.EndChangeCheck() && selected >= placeholderCount)
                    {
                        element.stringValue = stateNames[selected - placeholderCount];
                    }
                }
                else
                {
                    // No controller/states available yet — fall back to a plain text field so
                    // the user is never blocked, matching AnimatorStateNameDrawer's own fallback.
                    element.stringValue = EditorGUILayout.TextField(element.stringValue);
                }

                if (GUILayout.Button("-", GUILayout.Width(24)))
                {
                    _defaultIdleStatesProp.DeleteArrayElementAtIndex(i);
                    EditorGUILayout.EndHorizontal();
                    break; // Array size just changed — stop this loop iteration cleanly.
                }

                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("+ Add Idle State"))
            {
                int newIndex = _defaultIdleStatesProp.arraySize;
                _defaultIdleStatesProp.InsertArrayElementAtIndex(newIndex);
                _defaultIdleStatesProp.GetArrayElementAtIndex(newIndex).stringValue =
                    stateNames.Count > 0 ? stateNames[0] : "";
            }
        }
    }
}
#endif