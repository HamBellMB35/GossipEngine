#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Project.Data;

namespace Project.EditorTools
{
    /// <summary>
    /// Editor-only drawer for [AnimatorStateName]. Must live in an Editor folder/assembly
    /// since it depends on UnityEditor.Animations, which isn't available at runtime.
    /// </summary>
    [CustomPropertyDrawer(typeof(AnimatorStateNameAttribute))]
    public class AnimatorStateNameDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var attr = (AnimatorStateNameAttribute)attribute;

            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            AnimatorController controller = ResolveController(property, attr.ControllerFieldName);

            if (controller == null)
            {
                // No controller assigned yet — fall back to a plain text field so the
                // user is never blocked, with a hint about why there's no dropdown.
                EditorGUI.BeginChangeCheck();
                string typedValue = EditorGUI.TextField(position, label, property.stringValue);
                if (EditorGUI.EndChangeCheck()) property.stringValue = typedValue;
                return;
            }

            List<string> stateNames = GetAllStateNames(controller);

            if (stateNames.Count == 0)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            int currentIndex = stateNames.IndexOf(property.stringValue);
            bool notFound = currentIndex < 0;

            List<string> displayOptions = new List<string>(stateNames);
            int placeholderCount = 0;

            if (notFound && !string.IsNullOrEmpty(property.stringValue))
            {
                displayOptions.Insert(0, $"{property.stringValue}  (not found in controller)");
                currentIndex = 0;
                placeholderCount = 1;
            }
            else if (notFound)
            {
                displayOptions.Insert(0, "(none selected)");
                currentIndex = 0;
                placeholderCount = 1;
            }
            else
            {
                currentIndex += placeholderCount; // no offset needed, kept for clarity
            }

            EditorGUI.BeginChangeCheck();
            int selected = EditorGUI.Popup(position, label.text, currentIndex, displayOptions.ToArray());
            if (EditorGUI.EndChangeCheck() && selected >= placeholderCount)
            {
                property.stringValue = stateNames[selected - placeholderCount];
            }
        }

        private AnimatorController ResolveController(SerializedProperty property, string controllerFieldName)
        {
            SerializedProperty siblingProp = property.serializedObject.FindProperty(controllerFieldName);
            if (siblingProp == null || siblingProp.objectReferenceValue == null) return null;

            switch (siblingProp.objectReferenceValue)
            {
                case AnimatorController ac:
                    return ac;
                case Animator animatorComponent:
                    return animatorComponent.runtimeAnimatorController as AnimatorController;
                case RuntimeAnimatorController rac:
                    return rac as AnimatorController;
                default:
                    return null;
            }
        }

        private List<string> GetAllStateNames(AnimatorController controller)
        {
            var names = new List<string>();
            foreach (AnimatorControllerLayer layer in controller.layers)
            {
                CollectStateNames(layer.stateMachine, names);
            }
            return names.Distinct().OrderBy(n => n).ToList();
        }

        private void CollectStateNames(AnimatorStateMachine stateMachine, List<string> names)
        {
            foreach (ChildAnimatorState childState in stateMachine.states)
            {
                names.Add(childState.state.name);
            }
            foreach (ChildAnimatorStateMachine childMachine in stateMachine.stateMachines)
            {
                CollectStateNames(childMachine.stateMachine, names);
            }
        }
    }
}
#endif