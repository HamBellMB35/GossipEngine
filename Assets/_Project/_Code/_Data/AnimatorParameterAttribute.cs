using UnityEngine;
using UnityEditor.Animations;

namespace TownsPeople.Data
{
    /// <summary>
    /// Draws a string field as a dropdown of real Animator Controller parameter names (filtered
    /// by type), pulled from the controller referenced by another field on the same object —
    /// same idea as AnimatorStateNameAttribute, but for parameters instead of states. Makes
    /// typo/mismatch bugs against a Speed/Trigger/Bool parameter name structurally impossible.
    /// </summary>
    public class AnimatorParameterNameAttribute : PropertyAttribute
    {
        /// <summary>The name of the sibling field (Animator or AnimatorController reference) to pull parameters from.</summary>
        public readonly string ControllerFieldName;
        public readonly AnimatorControllerParameterType ParameterType;

        public AnimatorParameterNameAttribute(string controllerFieldName, AnimatorControllerParameterType parameterType = AnimatorControllerParameterType.Float)
        {
            ControllerFieldName = controllerFieldName;
            ParameterType = parameterType;
        }
    }
}