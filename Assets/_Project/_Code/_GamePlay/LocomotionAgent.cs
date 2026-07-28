using System;
using UnityEngine;
using UnityEngine.AI;

namespace TownsPeople.GamePlay
{
    /// <summary>
    /// Core movement engine for locomotion-driven NPCs — the ONE thing every locomotion
    /// behavior (Phase 2: WandererBehavior, VendorScheduleBehavior, BuskerBehavior) rides on
    /// top of. Wraps NavMeshAgent for actual pathfinding and obstacle avoidance (Unity's
    /// built-in NavMesh system already provides A*-family pathfinding plus local avoidance
    /// between agents — see this project's Locomotion setup notes for configuring it), and
    /// adds two things that aren't free: a per-NPC speed ramp between symbolic Walk/Run tiers
    /// (so an NPC reaches its target speed right around when it arrives at a waypoint instead
    /// of snapping instantly), and a reference to which LocomotionRoute this NPC is currently
    /// assigned to walk — a shared, single source of truth that Phase 2's behavior components
    /// will read rather than each duplicating their own route reference.
    ///
    /// Fully optional/removable, as an add-on: nothing outside the Locomotion system
    /// (NPCGossipMemory, NPCProximityGossip, NPCReputationOpinion, etc.) references or depends
    /// on this component in any way. Delete it off an NPC and every other system keeps working
    /// exactly as before — the NPC simply stands still.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class LocomotionAgent : MonoBehaviour
    {
        [Header("Per-NPC Speed Tiers")]
        [Tooltip("Actual movement speed (world units/sec) this NPC uses when a waypoint's Arrival Speed Tier is Walk.")]
        [SerializeField] private float _walkSpeed = 1.6f;

        [Tooltip("Actual movement speed (world units/sec) this NPC uses when a waypoint's Arrival Speed Tier is Run.")]
        [SerializeField] private float _runSpeed = 4.5f;

        [Header("Avoidance")]
        [Tooltip("Lower values yield right-of-way to NPCs with higher priority (lower number) when paths conflict — Unity's built-in NavMesh local avoidance handles the actual steering/waiting/veering. Range 0-99, matching NavMeshAgent's own Avoidance Priority.")]
        [SerializeField, Range(0, 99)] private int _avoidancePriority = 50;

        [Tooltip("How close (world units) counts as 'arrived' at the current destination. Mirrors NavMeshAgent's own Stopping Distance for convenience.")]
        [SerializeField] private float _arrivalThreshold = 0.15f;

        [Header("Route Assignment")]
        [Tooltip("The LocomotionRoute this NPC currently walks. Lives on its own separate GameObject — routes are shareable across multiple NPCs, so this is a reference, not ownership. Behavior components (Wanderer/Vendor/Busker, Phase 2) read this same field rather than each holding their own separate route reference.")]
        [SerializeField] private LocomotionRoute _assignedRoute;

        public LocomotionRoute AssignedRoute => _assignedRoute;

        private NavMeshAgent _agent;
        private float _currentLegStartSpeed;
        private float _currentLegTargetSpeed;
        private float _currentLegTotalDistance;
        private bool _isMoving;
        private bool _hasArrivedThisLeg;

        /// <summary>Fired once, the frame this agent's current MoveTo() destination is reached (or the path turns out to be unreachable — see Update()).</summary>
        public event Action OnArrivedAtDestination;

        public bool IsMoving => _isMoving;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _agent.avoidancePriority = _avoidancePriority;

            // Sensible default so avoidance actually engages meaningfully out of the box —
            // still directly overridable on the NavMeshAgent component itself if wanted.
            if (_agent.obstacleAvoidanceType == ObstacleAvoidanceType.NoObstacleAvoidance)
            {
                _agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
            }

            _agent.stoppingDistance = _arrivalThreshold;
            _agent.speed = _walkSpeed;
        }

        /// <summary>
        /// Begins moving toward destination, ramping speed from whatever it currently is toward
        /// the given tier's per-NPC speed value over the course of this leg — timed so the NPC
        /// is at (or very near) that speed by the time it arrives.
        /// </summary>
        public void MoveTo(Vector3 destination, LocomotionSpeedTier speedTier)
        {
            _currentLegStartSpeed = _agent.speed;
            _currentLegTargetSpeed = speedTier == LocomotionSpeedTier.Run ? _runSpeed : _walkSpeed;

            _agent.SetDestination(destination);
            _isMoving = true;
            _hasArrivedThisLeg = false;

            // Captured on the next stable Update() once the path is actually ready — see
            // Update(). NavMeshAgent.remainingDistance isn't valid the same frame
            // SetDestination() is called (pathPending is still true).
            _currentLegTotalDistance = -1f;
        }

        public void Stop()
        {
            _isMoving = false;
            _agent.ResetPath();
        }

        private void Update()
        {
            if (!_isMoving || _hasArrivedThisLeg) return;
            if (_agent.pathPending) return;

            // FIX: an agent not currently placed on a valid NavMesh (off the walkable area
            // entirely — e.g. spawned/moved inside a building or off the mesh's edge) can
            // return NaN/garbage from remainingDistance, which then propagates through the
            // Lerp math below straight into NavMeshAgent.speed, throwing
            // "Input speed is { NaN }". Skip processing entirely until the agent is confirmed
            // on-mesh — Unity will auto-snap it on if it's close enough, and this simply waits
            // for that to happen instead of feeding it invalid data in the meantime.
            if (!_agent.isOnNavMesh)
            {
                return;
            }

            if (_agent.pathStatus == NavMeshPathStatus.PathInvalid)
            {
                Debug.LogWarning($"<color=orange>[LocomotionAgent]</color> '{gameObject.name}' could not find a path to its destination — is the NavMesh baked and connected here?", this);
                _isMoving = false;
                _hasArrivedThisLeg = true;
                OnArrivedAtDestination?.Invoke(); // Still fire so a waiting behavior doesn't hang forever.
                return;
            }

            float remainingDistance = _agent.remainingDistance;

            // FIX: belt-and-suspenders — even with the isOnNavMesh guard above, never let a
            // NaN/Infinity value reach the Lerp/speed assignment below, regardless of cause.
            if (float.IsNaN(remainingDistance) || float.IsInfinity(remainingDistance))
            {
                return;
            }

            if (_currentLegTotalDistance < 0f)
            {
                _currentLegTotalDistance = Mathf.Max(remainingDistance, 0.01f);
            }

            float progress = 1f - Mathf.Clamp01(remainingDistance / _currentLegTotalDistance);
            _agent.speed = Mathf.Lerp(_currentLegStartSpeed, _currentLegTargetSpeed, progress);

            if (remainingDistance <= _agent.stoppingDistance)
            {
                _agent.speed = _currentLegTargetSpeed; // Snap the last small fraction — reads as fully "at speed" rather than stuck mid-ramp.
                _isMoving = false;
                _hasArrivedThisLeg = true;
                OnArrivedAtDestination?.Invoke();
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_agent == null) _agent = GetComponent<NavMeshAgent>();
            if (_agent != null)
            {
                _agent.avoidancePriority = _avoidancePriority;
            }
        }
#endif
    }
}