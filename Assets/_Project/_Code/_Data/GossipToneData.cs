using UnityEngine;

namespace TownsPeople.Data
{
    // Determines if this tone triggers an animation, plays once, or loops.
    public enum PlaybackMode { None, PlayOnce, Loop }

    [CreateAssetMenu(fileName = "NewTone", menuName = "Project/Gossip/ToneData")]
    public class GossipToneData : ScriptableObject
    {
        [Tooltip("The display name of the tone for debug purposes.")]
        public string ToneName;

        [Tooltip("Defines how the animation plays: None (static), PlayOnce (fire and forget), or Loop (timed).")]
        public PlaybackMode Mode;

        [Tooltip("Time in seconds to blend between animations.")]
        public float CrossfadeDuration = 0.25f;

        [Tooltip("How long to play the animation when in Loop mode.")]
        public float LoopDuration = 5.0f;

        // v3: Back to string state names (as requested), but now drawn as a dropdown of real
        // states pulled from TargetController below via [AnimatorStateName] — no more typing,
        // no more typos, and no hidden "clip name must match state name" rule to remember.
        [Tooltip("The Animator Controller this tone's states belong to. Assign this to populate the dropdown below.")]
        public RuntimeAnimatorController TargetController;

        [Tooltip("List of possible animation state names to pick from randomly.")]
        [AnimatorStateName(nameof(TargetController))]
        public string[] AnimatorStateNames;

        public string GetRandomAnimatorStateName() =>
            AnimatorStateNames != null && AnimatorStateNames.Length > 0
                ? AnimatorStateNames[Random.Range(0, AnimatorStateNames.Length)]
                : "";
    }
}