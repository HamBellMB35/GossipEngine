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

        [Header("Animation Pool Mapping")]
        [Tooltip("Add the exact names of all the State boxes inside your Unity Animator Controller for this tone. The engine will pick one at random!")]
        public string[] AnimatorStateNames;


        //[Header("Animation Mapping")]
        //[Tooltip("The EXACT name of the State box inside your Unity Animator Controller.")]
        //public string AnimatorStateName;

        [Tooltip("How long it takes to blend into this animation (in seconds).")]
        [Range(0f, 1f)]
        public float CrossfadeDuration = 0.15f;

        /// <summary>
        /// A helper method. It safely selects a random state name from our array.
        /// </summary>

        public string GetRandomAnimatorStateName()
        {
            if (AnimatorStateNames == null || AnimatorStateNames.Length == 0)
            {
                Debug.LogWarning($"[GossipToneData] {name} has no animator state names assigned in its array!");
                return string.Empty;
            }
           
            int randomIndex = Random.Range(0, AnimatorStateNames.Length);
            return AnimatorStateNames[randomIndex];

        }

    }
}
