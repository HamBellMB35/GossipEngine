using UnityEngine;

namespace Project.Data
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

        [Tooltip("List of possible animation state names to pick from randomly.")]
        public string[] AnimatorStateNames;

        public string GetRandomAnimatorStateName() =>
            AnimatorStateNames.Length > 0 ? AnimatorStateNames[Random.Range(0, AnimatorStateNames.Length)] : "";
    }
}