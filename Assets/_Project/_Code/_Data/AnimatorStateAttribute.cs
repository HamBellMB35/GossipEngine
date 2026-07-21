using UnityEngine;

namespace TownsPeople.Data
{
    /// <summary>
    /// Draws a string field (or each element of a string list/array) as a dropdown of real
    /// Animator state names, pulled directly from the Animator Controller referenced by another
    /// field on the same object. This makes typo/mismatch bugs structurally impossible — the
    /// dropdown can only ever show state names that actually exist in the target controller.
    /// </summary>
    public class AnimatorStateNameAttribute : PropertyAttribute
    {
        /// <summary>
        /// The name of the sibling field on the same object that holds the source to pull
        /// states from. Accepts a RuntimeAnimatorController/AnimatorController reference, or
        /// an Animator component reference (its currently assigned controller will be used).
        /// </summary>
        public readonly string ControllerFieldName;

        public AnimatorStateNameAttribute(string controllerFieldName)
        {
            ControllerFieldName = controllerFieldName;
        }
    }
}