#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using TownsPeople.GamePlay;

namespace TownsPeople.EditorTools
{
    /// <summary>
    /// Custom Inspector for NPCFlockingBehavior. Every field draws normally EXCEPT Waiting
    /// Animation States, which gets a dedicated section below: a proper reorderable list of real
    /// Animator state dropdowns (Add/Remove buttons included) — same pattern and reasoning as
    /// NPCAnimationBridgeEditor's Default Idle States section.
    ///
    /// FIX: TownsPeople.CustomEditor's own last namespace segment shadows UnityEditor's
    /// CustomEditor attribute and Editor base class (same collision already fixed on
    /// PlayerDeedBroadcasterEditor, LocomotionRouteEditor, and NPCAnimationBridgeEditor) — both
    /// are fully qualified below instead of relying on the `using UnityEditor;` import for
    /// these two specifically.
    /// </summary>
    [UnityEditor.CustomEditor(typeof(NPCFlockingBehavior))]
    public class NPCFlockingBehaviorEditor : UnityEditor.Editor
    {
        private SerializedProperty _waitingAnimationStatesProp;

        private void OnEnable()
        {
            _waitingAnimationStatesProp = serializedObject.FindProperty("_waitingAnimationStates");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            SerializedProperty iterator = serializedObject.GetIterator();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (iterator.propertyPath == "_waitingAnimationStates") continue;
                EditorGUILayout.PropertyField(iterator, true);
            }

            EditorGUILayout.Space();
            DrawWaitingAnimationStatesList();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawWaitingAnimationStatesList()
        {
            EditorGUILayout.LabelField("Waiting Animation States", EditorStyles.boldLabel);

            AnimatorController controller = ResolveControllerFromSiblingBridge();
            List<string> stateNames = controller != null
                ? AnimatorStateNameDrawer.GetAllStateNames(controller)
                : new List<string>();

            if (controller == null)
            {
                EditorGUILayout.HelpBox("No NPCAnimationBridge with an assigned Animator found on this GameObject — add one to populate this list from its real states.", MessageType.Info);
            }
            else if (stateNames.Count == 0)
            {
                EditorGUILayout.HelpBox("The assigned Animator Controller has no states yet.", MessageType.Info);
            }

            for (int i = 0; i < _waitingAnimationStatesProp.arraySize; i++)
            {
                SerializedProperty element = _waitingAnimationStatesProp.GetArrayElementAtIndex(i);

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
                    // the user is never blocked, matching NPCAnimationBridgeEditor's own fallback.
                    element.stringValue = EditorGUILayout.TextField(element.stringValue);
                }

                if (GUILayout.Button("-", GUILayout.Width(24)))
                {
                    _waitingAnimationStatesProp.DeleteArrayElementAtIndex(i);
                    EditorGUILayout.EndHorizontal();
                    break; // Array size just changed — stop this loop iteration cleanly.
                }

                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("+ Add Waiting Animation"))
            {
                int newIndex = _waitingAnimationStatesProp.arraySize;
                _waitingAnimationStatesProp.InsertArrayElementAtIndex(newIndex);
                _waitingAnimationStatesProp.GetArrayElementAtIndex(newIndex).stringValue =
                    stateNames.Count > 0 ? stateNames[0] : "";
            }
        }

        /// <summary>
        /// v6: NPCFlockingBehavior has no Animator field of its own — the controller comes from
        /// the NPCAnimationBridge on the same GameObject, if present.
        /// </summary>
        private AnimatorController ResolveControllerFromSiblingBridge()
        {
            NPCFlockingBehavior behavior = (NPCFlockingBehavior)target;
            NPCAnimationBridge bridge = behavior.GetComponent<NPCAnimationBridge>();
            if (bridge == null || bridge.Animator == null) return null;

            return bridge.Animator.runtimeAnimatorController as AnimatorController;
        }
    }
}
#endif