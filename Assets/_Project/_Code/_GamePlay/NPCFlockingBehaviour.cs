using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer;
using TownsPeople.Data;
using TownsPeople.Services;

namespace TownsPeople.GamePlay
{
    /// <summary>
    /// Temporary flocking/fleeing behavior layered on top of LocomotionAgent. Normally inert —
    /// checks its configured trigger pairs on an interval, and does nothing until one of them
    /// fires. Once triggered, takes over this NPC's movement (driving LocomotionAgent.MoveTo()
    /// directly toward a computed flee/flock destination each evaluation tick) until THAT SAME
    /// pair's own return condition fires, at which point it hands control back —
    /// OnFlockingEnded lets whatever normally drives this NPC's movement (LocomotionTester
    /// today; a future WandererBehavior eventually) resume exactly where it left off.
    ///
    /// Steering (cohesion/alignment/separation among nearby OTHER currently-flocking NPCs, plus
    /// a flee vector away from the threat) is adapted from a classic boids implementation, with
    /// the raycast-based obstacle/bounds avoidance deliberately dropped — LocomotionAgent's own
    /// NavMeshAgent already handles pathfinding and obstacle avoidance robustly; reimplementing
    /// raycast dodging on top would fight it rather than help. This component only ever decides
    /// WHERE to run toward; LocomotionAgent (and NavMesh underneath it) decides HOW to get there.
    ///
    /// Fully optional, like every other Locomotion component: an NPC without this component is
    /// completely unaffected, and nothing outside Locomotion references this type directly.
    /// </summary>
    // v4 FIX: v1 used two independent flat lists (ALL triggers checked with OR to start; ALL
    // return conditions checked with OR to end) — this meant a return condition UNRELATED to
    // whatever actually triggered flocking could end it almost instantly. Fixed by pairing each
    // trigger with ITS OWN return condition (FlockTriggerPair) — only the pair that actually
    // caused the current flocking episode is checked for when to end it.
    //
    // v6: Max Flee Distance + Waiting Animation. _maxFleeDistance (0 = unlimited) caps how far
    // this NPC will run from wherever the CURRENT flocking episode began. Reaching it does NOT
    // end flocking — the actual return condition still evaluates normally the whole time — it
    // only stops outward movement and plays a random pose from _waitingAnimationStates instead.
    // Played with useTimer: false (no auto-revert) — the timed revert would prematurely resume
    // movement mid-wait via NPCAnimationBridge's own RevertToRestingState(), fighting this
    // component's explicit pause; the mask is released explicitly instead.
    //
    // v9 FIX: The waiting state was flickering on/off on a regular interval (every
    // _evaluationInterval) — a "snap back to standing, replay the animation" loop. Root cause:
    // entering and exiting the waiting state used the EXACT SAME _maxFleeDistance threshold.
    // Once parked, tiny positional jitter (NavMeshAgent settling after Stop(), floating-point
    // noise) could nudge the measured distance back under the threshold for a single tick,
    // which read as "pulled back — resume fleeing" and released the animation, only to
    // re-cross the threshold and re-enter waiting the very next tick. Fixed with hysteresis —
    // _maxFleeDistanceHysteresis is subtracted from the threshold ONLY for the resume check,
    // so the NPC must be pulled back a real, deliberate distance under the max before it's
    // allowed to resume, not just a hair's width.
    [RequireComponent(typeof(LocomotionAgent))]
    public class NPCFlockingBehavior : MonoBehaviour, IPriorityBehaviorState
    {
        [Serializable]
        public class FlockTriggerPair
        {
            [Tooltip("Starts flocking when this fires.")]
            public FlockTriggerCondition Trigger;

            [Tooltip("Ends flocking when THIS fires — checked only while this specific pair is the one currently active, not any other pair's return condition.")]
            public FlockReturnCondition ReturnCondition;
        }

        [Header("Trigger / Return Pairs")]
        [Tooltip("Checked every evaluation tick while NOT currently flocking. The first pair whose Trigger fires starts flocking — and becomes the ONLY pair checked for when to stop, via its own ReturnCondition, until this episode ends. Add your own FlockTriggerCondition/FlockReturnCondition subclasses to extend this beyond the two shipped defaults.")]
        [SerializeField] private List<FlockTriggerPair> _triggerPairs = new List<FlockTriggerPair>();

        [Header("Steering Weights")]
        [Tooltip("Pull toward the average position of nearby flocking neighbors — keeps the group loosely together while fleeing.")]
        [Range(0f, 5f)][SerializeField] private float _cohesionWeight = 1f;

        [Tooltip("Pull toward the average facing direction of nearby flocking neighbors.")]
        [Range(0f, 5f)][SerializeField] private float _alignmentWeight = 1f;

        [Tooltip("Push away from nearby flocking neighbors — prevents this NPC overlapping/crowding others while fleeing.")]
        [Range(0f, 5f)][SerializeField] private float _separationWeight = 1.5f;

        [Tooltip("Push away from the threat (PlayerCombatState.PlayerTransform). This is normally the DOMINANT weight — it's what makes this fleeing rather than just ambient flocking.")]
        [Range(0f, 10f)][SerializeField] private float _fleeWeight = 4f;

        [Header("Detection & Timing")]
        [Tooltip("How far away another currently-flocking NPC counts as a neighbor for cohesion/alignment/separation.")]
        [SerializeField] private float _neighborRadius = 8f;

        [Tooltip("How far ahead of this NPC's current position the computed steering destination is placed — larger values commit to a direction for longer between recalculations.")]
        [SerializeField] private float _lookAheadDistance = 6f;

        [Tooltip("How often (seconds) triggers/return conditions are checked and, while flocking, steering is recalculated. Lower = more responsive but more pathfinding overhead; higher = cheaper but laggier reactions.")]
        [SerializeField] private float _evaluationInterval = 0.2f;

        [Tooltip("Minimum angle (degrees) the newly computed flee direction must differ from the one currently being pursued before a NEW destination is actually issued — prevents re-triggering NavMeshAgent's decelerate/rotate/re-accelerate cycle needlessly every evaluation tick when the desired direction has barely changed.")]
        [SerializeField] private float _minRedirectAngle = 15f;

        [Header("Max Flee Distance")]
        [Tooltip("Maximum distance (world units) this NPC will flee from wherever the CURRENT flocking episode began. 0 = unlimited. Reaching this does NOT end flocking — the actual return condition still decides that — it only stops outward movement and plays a Waiting Animation State instead.")]
        [SerializeField] private float _maxFleeDistance = 0f;

        [Tooltip("v9 FIX: Buffer (world units) subtracted from Max Flee Distance for the 'resume fleeing' check only. Prevents rapid on/off toggling of the waiting state caused by tiny positional jitter right at the exact threshold — the NPC must be pulled back at least this far under Max Flee Distance before it resumes active fleeing, not just barely under it.")]
        [SerializeField] private float _maxFleeDistanceHysteresis = 0.5f;

        [Tooltip("Pool of Animator state names this NPC can play once it reaches Max Flee Distance, while waiting for its return condition to fire. One is chosen at random each time it enters this waiting state. Edit via this component's custom Inspector — populated as a dropdown from the NPCAnimationBridge on this same GameObject's assigned Animator. Irrelevant if Max Flee Distance is 0.")]
        [SerializeField] private List<string> _waitingAnimationStates = new List<string>();

        [Header("Recovery")]
        [Tooltip("How long (seconds), AFTER the return condition has already fired, this NPC continues playing a Recovery Animation State before actually resuming its previous behavior (route, etc). 0 = no recovery — resumes immediately, the original behavior. Applies regardless of whether the NPC was actively fleeing or already parked at Max Flee Distance when the return condition fired.")]
        [SerializeField] private float _recoveryDuration = 3f;

        [Tooltip("Pool of Animator state names this NPC can play during the recovery period. One is chosen at random each time recovery begins. Edit via this component's custom Inspector — populated the same way as Waiting Animation States. Irrelevant if Recovery Duration is 0.")]
        [SerializeField] private List<string> _recoveryAnimationStates = new List<string>();

        private static readonly List<NPCFlockingBehavior> _activeInstances = new List<NPCFlockingBehavior>();

        private LocomotionAgent _locomotionAgent;
        private NPCAnimationBridge _animationBridge;
        private PlayerCombatState _combatState;
        private float _evaluationTimer;
        private float _flockingStartTime;
        private bool _isFlocking;
        private Vector3 _lastIssuedDirection;
        private bool _hasIssuedDirection;
        private FlockTriggerPair _activeTriggerPair;
        private Vector3 _flockOrigin;
        private bool _isWaitingAtMaxDistance;
        // v11: True during the Recovery phase — the return condition already fired, but this
        // NPC continues playing a Recovery Animation State for _recoveryDuration before
        // EndFlocking() actually runs and previous behavior resumes.
        private bool _isRecovering;
        private float _recoveryStartTime;

        /// <summary>True while this NPC is currently flocking/fleeing.</summary>
        public bool IsFlocking => _isFlocking;

        /// <summary>IPriorityBehaviorState — reactive systems (rumor presentation, witness reactions) check this to know they should skip presentation while true.</summary>
        public bool IsActive => _isFlocking;

        /// <summary>Fires the instant a trigger condition starts flocking.</summary>
        public event Action OnFlockingStarted;

        /// <summary>Fires the instant a return condition ends flocking — whatever normally drives this NPC's movement should resume here.</summary>
        public event Action OnFlockingEnded;

        [Inject]
        public void Construct(PlayerCombatState combatState)
        {
            _combatState = combatState;
        }

        private void Awake()
        {
            _locomotionAgent = GetComponent<LocomotionAgent>();
            _animationBridge = GetComponent<NPCAnimationBridge>();
        }

        private void OnEnable()
        {
            _activeInstances.Add(this);
        }

        private void OnDisable()
        {
            _activeInstances.Remove(this);
        }

        private void Update()
        {
            // v11: Gated behind !_isRecovering — once the return condition has fired and
            // recovery has begun, this NPC is done fleeing/waiting entirely; none of the
            // per-frame flee-steering/redirect logic should run anymore.
            if (_isFlocking && !_isRecovering && !_locomotionAgent.IsMoving)
            {
                UpdateFleeSteering();
            }

            // v9: Also checked every frame while actively fleeing — otherwise the NPC could
            // physically cross Max Flee Distance up to _evaluationInterval seconds before the
            // waiting animation actually started.
            if (_isFlocking && !_isRecovering && !_isWaitingAtMaxDistance && _maxFleeDistance > 0f)
            {
                float distanceFromOrigin = Vector3.Distance(transform.position, _flockOrigin);
                if (distanceFromOrigin >= _maxFleeDistance)
                {
                    EnterWaitingAtMaxDistance();
                }
            }

            _evaluationTimer += Time.deltaTime;
            if (_evaluationTimer < _evaluationInterval) return;
            _evaluationTimer = 0f;

            if (_isFlocking)
            {
                if (_isRecovering)
                {
                    // v11: Only check whether recovery is OVER — nothing else runs during it
                    // (no steering, no re-triggering, no re-checking the return condition again).
                    if (Time.time - _recoveryStartTime >= _recoveryDuration)
                    {
                        EndFlocking();
                    }
                    return;
                }

                if (CheckActivePairReturnCondition())
                {
                    BeginRecovery();
                    return;
                }

                UpdateFleeSteering();
            }
            else
            {
                FlockTriggerPair firedPair = FindFiredTriggerPair();
                if (firedPair != null)
                {
                    BeginFlocking(firedPair);
                }
            }
        }

        /// <summary>Returns the first pair whose Trigger fires, or null if none did.</summary>
        private FlockTriggerPair FindFiredTriggerPair()
        {
            for (int i = 0; i < _triggerPairs.Count; i++)
            {
                FlockTriggerPair pair = _triggerPairs[i];
                if (pair?.Trigger != null && pair.Trigger.ShouldTrigger(_combatState, transform)) return pair;
            }
            return null;
        }

        /// <summary>Checks ONLY _activeTriggerPair's own ReturnCondition — not every configured pair's.</summary>
        private bool CheckActivePairReturnCondition()
        {
            if (_activeTriggerPair?.ReturnCondition == null) return false;

            float timeSpentFlocking = Time.time - _flockingStartTime;
            return _activeTriggerPair.ReturnCondition.ShouldReturnToNormal(_combatState, transform, timeSpentFlocking);
        }

        private void BeginFlocking(FlockTriggerPair triggeringPair)
        {
            _isFlocking = true;
            _activeTriggerPair = triggeringPair;
            _flockingStartTime = Time.time;
            _flockOrigin = transform.position;
            _isWaitingAtMaxDistance = false;
            _isRecovering = false;
            _hasIssuedDirection = false;
            OnFlockingStarted?.Invoke();
            UpdateFleeSteering();
        }

        /// <summary>
        /// v11: Called the instant the active pair's return condition fires — whether the NPC
        /// was still actively fleeing or already parked at Max Flee Distance. Always halts
        /// movement (harmless no-op if it was already stopped) and always plays a Recovery
        /// Animation State if one is configured, for Recovery Duration, before EndFlocking()
        /// actually runs. If Recovery Duration is 0, ends immediately — the original behavior.
        /// </summary>
        private void BeginRecovery()
        {
            _isRecovering = true;
            _recoveryStartTime = Time.time;
            _isWaitingAtMaxDistance = false;
            _locomotionAgent.Stop();

            if (_recoveryDuration <= 0f)
            {
                EndFlocking();
                return;
            }

            if (_animationBridge == null || _recoveryAnimationStates == null || _recoveryAnimationStates.Count == 0) return;

            string chosenState = _recoveryAnimationStates[UnityEngine.Random.Range(0, _recoveryAnimationStates.Count)];
            if (string.IsNullOrEmpty(chosenState)) return;

            int stateHash = Animator.StringToHash(chosenState);
            _animationBridge.SetAnimationState(stateHash, useTimer: false);
        }

        /// <summary>
        /// v11: Now ONLY ever reached via BeginRecovery() — either its Recovery Duration == 0
        /// immediate path, or once Update()'s recovery-elapsed check fires. Since BeginRecovery()
        /// always halts movement (and usually plays a masking animation) before this runs, the
        /// release + resume below is now unconditional instead of gated on _isWaitingAtMaxDistance.
        /// </summary>
        private void EndFlocking()
        {
            _isFlocking = false;
            _isRecovering = false;
            _isWaitingAtMaxDistance = false;
            _activeTriggerPair = null;
            _hasIssuedDirection = false;

            _animationBridge?.ReleaseReactionOverride();
            _locomotionAgent.Resume();

            OnFlockingEnded?.Invoke();
        }

        private void UpdateFleeSteering()
        {
            if (_maxFleeDistance > 0f)
            {
                float distanceFromOrigin = Vector3.Distance(transform.position, _flockOrigin);

                if (_isWaitingAtMaxDistance)
                {
                    // v9 FIX: hysteresis — only resume once pulled back MEANINGFULLY under the
                    // threshold, not just barely under it, so tiny positional jitter right at
                    // the boundary can't toggle the waiting state on/off every tick.
                    if (distanceFromOrigin < _maxFleeDistance - _maxFleeDistanceHysteresis)
                    {
                        _isWaitingAtMaxDistance = false;
                        _animationBridge?.ReleaseReactionOverride();
                        _locomotionAgent.Resume();
                        // Falls through to normal steering below — actively resumes fleeing.
                    }
                    else
                    {
                        return; // Still waiting — don't touch movement or the animation.
                    }
                }
                else if (distanceFromOrigin >= _maxFleeDistance)
                {
                    EnterWaitingAtMaxDistance();
                    return;
                }
            }

            List<NPCFlockingBehavior> neighbors = FindNearbyFlockingNeighbors();

            Vector3 cohesion = ComputeCohesionVector(neighbors) * _cohesionWeight;
            Vector3 alignment = ComputeAlignmentVector(neighbors) * _alignmentWeight;
            Vector3 separation = ComputeSeparationVector(neighbors) * _separationWeight;

            Vector3 flee = Vector3.zero;
            if (_combatState != null && _combatState.PlayerTransform != null)
            {
                flee = ComputeFleeVector(_combatState.PlayerTransform.position) * _fleeWeight;
            }

            Vector3 combined = cohesion + alignment + separation + flee;
            if (combined.sqrMagnitude < 0.0001f)
            {
                combined = transform.forward;
            }

            Vector3 desiredDirection = combined.normalized;

            bool shouldRedirect = !_hasIssuedDirection
                || !_locomotionAgent.IsMoving
                || Vector3.Angle(_lastIssuedDirection, desiredDirection) >= _minRedirectAngle;
            if (!shouldRedirect) return;

            Vector3 destination = transform.position + desiredDirection * _lookAheadDistance;
            _locomotionAgent.MoveTo(destination, LocomotionSpeedTier.Run);
            _lastIssuedDirection = desiredDirection;
            _hasIssuedDirection = true;
        }

        /// <summary>
        /// Called once, the instant Max Flee Distance is first reached — halts movement and
        /// plays a random pose from _waitingAnimationStates with useTimer: false (no
        /// auto-revert). Safe no-op if no waiting animations are configured.
        /// </summary>
        private void EnterWaitingAtMaxDistance()
        {
            _isWaitingAtMaxDistance = true;
            _locomotionAgent.Stop();

            if (_animationBridge == null || _waitingAnimationStates == null || _waitingAnimationStates.Count == 0) return;

            string chosenState = _waitingAnimationStates[UnityEngine.Random.Range(0, _waitingAnimationStates.Count)];
            if (string.IsNullOrEmpty(chosenState)) return;

            int stateHash = Animator.StringToHash(chosenState);
            _animationBridge.SetAnimationState(stateHash, useTimer: false);
        }

        private List<NPCFlockingBehavior> FindNearbyFlockingNeighbors()
        {
            List<NPCFlockingBehavior> result = new List<NPCFlockingBehavior>();
            float radiusSqr = _neighborRadius * _neighborRadius;

            for (int i = 0; i < _activeInstances.Count; i++)
            {
                NPCFlockingBehavior other = _activeInstances[i];
                if (other == this || !other._isFlocking) continue;

                float sqrDistance = (other.transform.position - transform.position).sqrMagnitude;
                if (sqrDistance <= radiusSqr) result.Add(other);
            }

            return result;
        }

        private Vector3 ComputeCohesionVector(List<NPCFlockingBehavior> neighbors)
        {
            if (neighbors.Count == 0) return Vector3.zero;

            Vector3 centerOfMass = Vector3.zero;
            for (int i = 0; i < neighbors.Count; i++)
            {
                centerOfMass += neighbors[i].transform.position;
            }
            centerOfMass /= neighbors.Count;

            return (centerOfMass - transform.position).normalized;
        }

        private Vector3 ComputeAlignmentVector(List<NPCFlockingBehavior> neighbors)
        {
            if (neighbors.Count == 0) return transform.forward;

            Vector3 averageForward = Vector3.zero;
            for (int i = 0; i < neighbors.Count; i++)
            {
                averageForward += neighbors[i].transform.forward;
            }

            return (averageForward / neighbors.Count).normalized;
        }

        private Vector3 ComputeSeparationVector(List<NPCFlockingBehavior> neighbors)
        {
            if (neighbors.Count == 0) return Vector3.zero;

            Vector3 separation = Vector3.zero;
            for (int i = 0; i < neighbors.Count; i++)
            {
                Vector3 offset = transform.position - neighbors[i].transform.position;
                float distance = offset.magnitude;
                if (distance > 0.001f) separation += offset.normalized / distance;
            }

            return separation.normalized;
        }

        private Vector3 ComputeFleeVector(Vector3 threatPosition)
        {
            return (transform.position - threatPosition).normalized;
        }
    }
}