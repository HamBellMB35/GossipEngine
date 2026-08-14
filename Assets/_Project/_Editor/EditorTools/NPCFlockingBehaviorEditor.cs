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
    /// Animation States and Recovery Animation States, which each get a dedicated section
    /// below: a proper reorderable list of real Animator state dropdowns (Add/Remove buttons
    /// included) — same pattern and reasoning as NPCAnimationBridgeEditor's Default Idle States
    /// section.
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
        // v3: Second list, same treatment — Recovery Animation States.
        private SerializedProperty _recoveryAnimationStatesProp;

        private void OnEnable()
        {
            _waitingAnimationStatesProp = serializedObject.FindProperty("_waitingAnimationStates");
            _recoveryAnimationStatesProp = serializedObject.FindProperty("_recoveryAnimationStates");
        }

        public override void OnInspectorGUI()
        {
            // v2 FIX: target can be a destroyed-but-not-yet-fully-null UnityEngine.Object if the
            // NPC (or just this component) was deleted while NPCControlPanelWindow still had it
            // selected/drawn — Unity's overloaded == on UnityEngine.Object correctly detects
            // this case, unlike a plain reference null check. Bail out before touching anything
            // on it (GetComponent on a destroyed object throws MissingReferenceException).
            if (target == null) return;

            serializedObject.Update();

            SerializedProperty iterator = serializedObject.GetIterator();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                // v3: Both list fields are drawn in their own dedicated sections below instead
                // of via the default per-property rendering.
                if (iterator.propertyPath == "_waitingAnimationStates") continue;
                if (iterator.propertyPath == "_recoveryAnimationStates") continue;
                EditorGUILayout.PropertyField(iterator, true);
            }

            EditorGUILayout.Space();
            DrawAnimationStateList(_waitingAnimationStatesProp, "Waiting Animation States", "+ Add Waiting Animation");

            EditorGUILayout.Space();
            DrawAnimationStateList(_recoveryAnimationStatesProp, "Recovery Animation States", "+ Add Recovery Animation");

            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// v3: Generalized from the original DrawWaitingAnimationStatesList() — now takes the
        /// target SerializedProperty and its display labels, so both Waiting and Recovery
        /// Animation States share one implementation instead of duplicating this whole block.
        /// </summary>
        private void DrawAnimationStateList(SerializedProperty listProp, string headerLabel, string addButtonLabel)
        {
            EditorGUILayout.LabelField(headerLabel, EditorStyles.boldLabel);

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

            for (int i = 0; i < listProp.arraySize; i++)
            {
                SerializedProperty element = listProp.GetArrayElementAtIndex(i);

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
                    listProp.DeleteArrayElementAtIndex(i);
                    EditorGUILayout.EndHorizontal();
                    break; // Array size just changed — stop this loop iteration cleanly.
                }

                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button(addButtonLabel))
            {
                int newIndex = listProp.arraySize;
                listProp.InsertArrayElementAtIndex(newIndex);
                listProp.GetArrayElementAtIndex(newIndex).stringValue =
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