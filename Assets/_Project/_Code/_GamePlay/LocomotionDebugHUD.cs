using UnityEngine;
using UnityEngine.AI;

namespace TownsPeople.GamePlay
{
    /// <summary>
    /// TEMPORARY DIAGNOSTIC TOOL � not part of the shipped Locomotion add-on. Displays live
    /// NavMeshAgent/Animator/root-motion values on screen during Play mode, to pin down
    /// exactly what's happening rather than reasoning about it blind. Delete once locomotion
    /// is confirmed working correctly.
    /// </summary>
    [RequireComponent(typeof(LocomotionAgent))]
    public class LocomotionDebugHUD : MonoBehaviour
    {
        [SerializeField] private Animator _animator;
        private NavMeshAgent _agent;
        private LocomotionAgent _locomotionAgent;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _locomotionAgent = GetComponent<LocomotionAgent>();
            if (_animator == null) _animator = GetComponentInChildren<Animator>();
        }

        private void OnGUI()
        {
            if (_agent == null) return;

            GUIStyle style = new GUIStyle(GUI.skin.box);
            style.fontSize = 28;
            style.alignment = TextAnchor.UpperLeft;
            style.normal.textColor = Color.white;

            string text =
                $"NPC: {gameObject.name}\n" +
                $"Root transform.position: {transform.position:F2}\n" +
                $"agent.nextPosition: {_agent.nextPosition:F2}\n" +
                $"Ground raycast hit Y: {GetGroundRaycastY():F3}\n" +
                $"agent.speed (target): {_agent.speed:F2}\n" +
                $"agent.velocity: {_agent.velocity:F2} (mag {_agent.velocity.magnitude:F2})\n" +
                $"agent.isOnNavMesh: {_agent.isOnNavMesh}\n" +
                $"agent.pathPending: {_agent.pathPending}\n" +
                $"agent.remainingDistance: {_agent.remainingDistance:F2}\n" +
                $"agent.updatePosition: {_agent.updatePosition}\n" +
                $"IsMoving: {_locomotionAgent.IsMoving}   IsPaused: {_locomotionAgent.IsPaused}\n" +
                $"CurrentLegWillStop: {_locomotionAgent.CurrentLegWillStop}\n";

            if (_animator != null)
            {
                text +=
                    $"\nAnimator GO: {_animator.gameObject.name}\n" +
                    $"Animator.rootPosition (world): {_animator.rootPosition:F2}\n" +
                    $"Animator GO transform.position: {_animator.transform.position:F2}\n" +
                    $"Animator GO transform.localPosition: {_animator.transform.localPosition:F3}\n" +
                    $"Animator.applyRootMotion: {_animator.applyRootMotion}\n" +
                    $"Animator Speed param: {_animator.GetFloat("Speed"):F2}\n";
            }
            else
            {
                text += "\nNo Animator found!\n";
            }

            GUI.Box(new Rect(10, 10, 900, 560), text, style);
        }

        /// <summary>
        /// Casts straight down from well above the NPC to find the actual floor collider's
        /// height at this position � the ground truth to compare nextPosition.y against.
        /// </summary>
        private float GetGroundRaycastY()
        {
            Vector3 origin = transform.position + Vector3.up * 5f;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 20f))
            {
                return hit.point.y;
            }
            return float.NaN;
        }
    }
}