using UnityEngine;

namespace Project.Data
{
    /// <summary>
    /// This ScriptableObject defines an emotional tone entirely through data.
    /// If you want a new dialogue stance later, you just create a new asset file!
    /// </summary>
    [CreateAssetMenu(menuName = "Gossip Engine/ Gossip Tone Data")]
    public class GossipToneData : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("The human-readable name of this tone (e.g., Whispering, Excited).")]
        public string ToneName;

        [Header("Animation Mapping")]
        [Tooltip("The EXACT name of the State box inside your Unity Animator Controller.")]
        public string AnimatorStateName;

        [Tooltip("How long it takes to blend into this animation (in seconds).")]
        [Range(0f, 1f)]
        public float CrossfadeDuration = 0.15f;

    }
}
