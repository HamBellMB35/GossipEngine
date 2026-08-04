#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using TownsPeople.Data;

namespace TownsPeople.EditorTools
{
    /// <summary>
    /// Editor-only drawer for [AnimatorParameterName]. Same structure as
    /// AnimatorStateNameDrawer, filtered to a specific AnimatorControllerParameterType.
    /// </summary>
    [CustomPropertyDrawer(typeof(AnimatorParameterNameAttribute))]
    public class AnimatorParameterNameDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var attr = (AnimatorParameterNameAttribute)attribute;

            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            AnimatorController controller = ResolveController(property, attr.ControllerFieldName);

            if (controller == null)
            {
                EditorGUI.BeginChangeCheck();
                string typedValue = EditorGUI.TextField(position, label, property.stringValue);
                if (EditorGUI.EndChangeCheck()) property.stringValue = typedValue;
                return;
            }

            List<string> parameterNames = controller.parameters
                .Where(p => p.type == attr.ParameterType)
                .Select(p => p.name)
                .Distinct()
                .OrderBy(n => n)
                .ToList();

            if (parameterNames.Count == 0)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            int currentIndex = parameterNames.IndexOf(property.stringValue);
            bool notFound = currentIndex < 0;

            List<string> displayOptions = new List<string>(parameterNames);
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

            EditorGUI.BeginChangeCheck();
            int selected = EditorGUI.Popup(position, label.text, currentIndex, displayOptions.ToArray());
            if (EditorGUI.EndChangeCheck() && selected >= placeholderCount)
            {
                property.stringValue = parameterNames[selected - placeholderCount];
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
    }
}
#endif