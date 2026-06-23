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
        // FIX: We declare the actual private field here so the script knows what "_animator" is.
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

            // Instead of dealing with transitions, we smoothly blend directly into the target state by name!
            // This is completely data-driven and bypasses structural Animator graph bottlenecks.
            _animator.CrossFadeInFixedTime(toneData.AnimatorStateName, toneData.CrossfadeDuration);

            Debug.Log($"<color=magenta>[Animation Bridge]</color> Smoothly blending into tone: {toneData.ToneName}");
        }
    }
}
