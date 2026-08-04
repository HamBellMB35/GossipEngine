using UnityEngine;

namespace TownsPeople.GamePlay
{
    /// <summary>
    /// Bridges Unity's OnAnimatorMove() callback — which only fires on scripts attached to the
    /// SAME GameObject as the Animator component — up to this NPC's LocomotionAgent, which
    /// typically lives on a separate parent GameObject (the wizard-generated hierarchy puts
    /// the Animator on "Character_Visual_Mesh", a child of the NPC root that actually carries
    /// LocomotionAgent/NavMeshAgent). Required for LocomotionAgent's Use Root Motion option to
    /// function at all — add this to whichever GameObject has the Animator.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class LocomotionRootMotionRelay : MonoBehaviour
    {
        private Animator _animator;
        private LocomotionAgent _locomotionAgent;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _locomotionAgent = GetComponentInParent<LocomotionAgent>();

            if (_locomotionAgent == null)
            {
                Debug.LogWarning($"<color=orange>[LocomotionRootMotionRelay]</color> '{gameObject.name}' could not find a LocomotionAgent on itself or any parent — root motion will play locally on the Animator but never reach the NPC's actual movement/pathfinding. Add this component only on the same NPC as a LocomotionAgent with Use Root Motion enabled.", this);
            }
        }

        private void OnAnimatorMove()
        {
            if (_locomotionAgent == null) return;
            _locomotionAgent.ReceiveRootMotion();
        }
    }
}