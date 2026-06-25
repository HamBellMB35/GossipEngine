using UnityEngine;
using Project.Architecture;
using Project.Data;

namespace Project.GamePlay
{

    /// <summary>
    /// This component bridges our architecture to the physical character model,
    /// shifting poses dynamically based on the provided data asset.
    /// </summary>
    /// 

    public class NPCAnimationBridge : MonoBehaviour, INpcAnimationController
    {
        [Header("References")]
        // We declare the actual private field here so the script knows what "_animator" is.
        // [SerializeField] allows us to drag and drop the Animator component inside the Unity Inspector!
        [SerializeField] private Animator _animator;
        public void TransitionToTone(GossipToneData toneData)
        {
            if (_animator == null) return;
            if (toneData == null)
            {
                Debug.LogWarning($"[Animation Bridge] {gameObject.name} was passed null tone data!");
                return;
            }

            // We ask our data asset to choose a random state name from its internal pool
            string chosenState = toneData.GetRandomAnimatorStateName();

            // If the array is empty or null, we log a warning and exit early so Unity doesnt crash or thow an error.
            if (string.IsNullOrEmpty(chosenState)) return;


            // Instead of dealing with transitions, we smoothly blend directly into the target state by name!
            // This is completely data-driven and bypasses structural Animator graph bottlenecks.
            _animator.CrossFadeInFixedTime(chosenState, toneData.CrossfadeDuration);

            Debug.Log($"<color=magenta>[Animation Bridge]</color> Smoothly blending into tone: {toneData.ToneName}");
        }
    }
}
