using UnityEngine;

namespace TownsPeople.GamePlay
{
    /// <summary>
    /// TEMPORARY TEST HARNESS � not part of the shipped Locomotion add-on. Since Phase 2's
    /// behavior components (WandererBehavior, etc.) don't exist yet, nothing currently calls
    /// LocomotionAgent.MoveTo() on its own. This component does that manually: walks the
    /// assigned LocomotionRoute in a loop, advancing to the next waypoint each time
    /// OnArrivedAtDestination fires. Delete this once Phase 2 behaviors exist and you're doing
    /// real gameplay testing instead of isolated Phase 1 verification.
    /// </summary>
    [RequireComponent(typeof(LocomotionAgent))]
    public class LocomotionTester : MonoBehaviour
    {
        [Tooltip("Route to walk in a loop for testing. If left empty, uses whatever's already assigned on this NPC's LocomotionAgent.")]
        [SerializeField] private LocomotionRoute _routeOverride;

        [Tooltip("If enabled, starts walking automatically on Play. If disabled, press the configured key to start.")]
        [SerializeField] private bool _autoStart = true;

        [Tooltip("Key to manually start/restart the test loop, if Auto Start is off.")]
        [SerializeField] private KeyCode _startKey = KeyCode.T;

        private LocomotionAgent _agent;
        private LocomotionRoute _route;
        private int _currentWaypointIndex;
        private bool _isRunning;

        private NPCFlockingBehavior _flockingBehavior;

        private void Awake()
        {
            _agent = GetComponent<LocomotionAgent>();
            _agent.OnArrivedAtDestination += HandleArrived;
            // v2: Corner anticipation � fires early for a plain (pass-through) waypoint,
            // before full arrival. Routed through the same AdvanceToNextWaypoint() as a real
            // arrival � for a pass-through leg with anticipation enabled, only THIS one ends
            // up firing (redirecting the destination before the old leg's own arrival check can
            // trigger), so there's no double-advance to guard against.
            _agent.OnApproachingDestination += HandleApproaching;

            // v3: Optional � pauses route-following while this NPC is flocking/fleeing
            // (NPCFlockingBehavior drives MoveTo() directly during that time), resuming the
            // CURRENT, unfinished leg once it returns to normal. No-op entirely if this NPC
            // doesn't have the Flocking add-on component.
            _flockingBehavior = GetComponent<NPCFlockingBehavior>();
            if (_flockingBehavior != null)
            {
                _flockingBehavior.OnFlockingStarted += HandleFlockingStarted;
                _flockingBehavior.OnFlockingEnded += HandleFlockingEnded;
            }
        }

        private void OnDestroy()
        {
            if (_agent != null)
            {
                _agent.OnArrivedAtDestination -= HandleArrived;
                _agent.OnApproachingDestination -= HandleApproaching;
            }

            if (_flockingBehavior != null)
            {
                _flockingBehavior.OnFlockingStarted -= HandleFlockingStarted;
                _flockingBehavior.OnFlockingEnded -= HandleFlockingEnded;
            }
        }

        private void HandleFlockingStarted()
        {
            // Route-following pauses � NPCFlockingBehavior takes over MoveTo() calls directly
            // until it hands control back.
            _isRunning = false;
        }

        private void HandleFlockingEnded()
        {
            // Resume the CURRENT (unfinished) leg, right where the route left off � "go back
            // to whatever they were doing."
            if (_route == null) return;
            _isRunning = true;
            MoveToCurrentWaypoint();
        }

        private void Start()
        {
            _route = _routeOverride != null ? _routeOverride : _agent.AssignedRoute;

            if (_route == null)
            {
                Debug.LogWarning($"<color=orange>[LocomotionTester]</color> '{gameObject.name}' has no route assigned (neither Route Override nor LocomotionAgent's Assigned Route).", this);
                return;
            }

            if (_autoStart) BeginLoop();
        }

        private void Update()
        {
            if (!_autoStart && Input.GetKeyDown(_startKey))
            {
                BeginLoop();
            }
        }

        private void BeginLoop()
        {
            if (_route == null || _route.Count == 0)
            {
                Debug.LogWarning($"<color=orange>[LocomotionTester]</color> '{gameObject.name}''s route has no waypoints.", this);
                return;
            }

            _isRunning = true;
            _currentWaypointIndex = 0;
            MoveToCurrentWaypoint();
        }

        private void MoveToCurrentWaypoint()
        {
            LocomotionWaypoint waypoint = _route.GetWaypoint(_currentWaypointIndex);
            // v4: Switched to the LocomotionWaypoint overload — passes the whole waypoint
            // through instead of just its Position, so LocomotionAgent can resolve whether this
            // visit should actually stop (Point of Interest) or flow through (plain waypoint).
            _agent.MoveTo(waypoint, waypoint.ArrivalSpeedTier);
            Debug.Log($"<color=cyan>[LocomotionTester]</color> '{gameObject.name}' heading to waypoint {_currentWaypointIndex} ({waypoint.ArrivalSpeedTier}).");
        }

        // v2: Both a real arrival AND a corner-anticipation "approaching" signal advance to
        // the next waypoint the same way � the only difference is WHEN LocomotionAgent fires
        // one versus the other (see its own v7 header comment).
        private void HandleArrived() => AdvanceToNextWaypoint();
        private void HandleApproaching() => AdvanceToNextWaypoint();

        private void AdvanceToNextWaypoint()
        {
            if (!_isRunning) return;

            _currentWaypointIndex++;
            if (_currentWaypointIndex >= _route.Count)
            {
                if (!_route.IsLoop)
                {
                    _isRunning = false;
                    Debug.Log($"<color=cyan>[LocomotionTester]</color> '{gameObject.name}' reached the end of a non-looping route.");
                    return;
                }
                _currentWaypointIndex = 0;
            }

            MoveToCurrentWaypoint();
        }
    }
}