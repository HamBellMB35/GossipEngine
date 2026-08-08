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
    // whatever actually triggered flocking could end it almost instantly. Concretely: testing
    // ONLY the "Player In Combat" trigger (weapon never drawn) still had "Weapon Put Away" in
    // the return list, and !IsWeaponDrawn is trivially true from tick one when the weapon was
    // never drawn — so flocking ended immediately regardless of the 8-second combat cooldown,
    // the NPC resumed its route (which passed back near the player), the combat trigger fired
    // again, and it looped: flee / instantly-end / walk back near player / flee again. Fixed by
    // pairing each trigger with ITS OWN return condition (FlockTriggerPair) — only the pair that
    // actually caused the current flocking episode is checked for when to end it.
    //
    // v6: Max Flee Distance + Waiting Animation. _maxFleeDistance (0 = unlimited, the original
    // behavior) caps how far this NPC will run from wherever the CURRENT flocking episode began
    // (not a fixed world point). Reaching it does NOT end flocking — the actual return condition
    // (weapon put away, cooldown, etc.) still evaluates normally the whole time — it only stops
    // the outward movement and plays a random pose from _waitingAnimationStates instead, until
    // either the return condition fires (flocking ends normally) or the NPC gets pulled back
    // under the threshold by cohesion/separation (resumes active fleeing automatically). Played
    // with useTimer: false (no auto-revert) — the normal timed revert would prematurely resume
    // movement mid-wait via NPCAnimationBridge's own RevertToRestingState(), fighting this
    // component's explicit pause; the mask is released explicitly instead, exactly when the
    // waiting state actually ends.
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

        [Tooltip("v2 FIX: minimum angle (degrees) the newly computed flee direction must differ from the one currently being pursued before a NEW destination is actually issued. Without this, every evaluation tick re-called MoveTo() even for a near-identical direction, forcing NavMeshAgent through a needless decelerate/rotate/re-accelerate cycle each time — that's what produced a rhythmic step/slow/step/stop stutter synced to the evaluation interval. Higher = smoother movement but slower to react to genuine direction changes; lower = more responsive but more prone to the original stutter creeping back in.")]
        [SerializeField] private float _minRedirectAngle = 15f;

        [Header("Max Flee Distance")]
        [Tooltip("v6: Maximum distance (world units) this NPC will flee from wherever the CURRENT flocking episode began. 0 = unlimited (the original behavior). Reaching this does NOT end flocking — the actual return condition still decides that — it only stops outward movement and plays a Waiting Animation State instead.")]
        [SerializeField] private float _maxFleeDistance = 0f;

        [Tooltip("v6: Pool of Animator state names this NPC can play once it reaches Max Flee Distance, while waiting for its return condition to fire. One is chosen at random each time it enters this waiting state. Edit via this component's custom Inspector — populated as a dropdown from the NPCAnimationBridge on this same GameObject's assigned Animator. Irrelevant if Max Flee Distance is 0.")]
        [SerializeField] private List<string> _waitingAnimationStates = new List<string>();

        private static readonly List<NPCFlockingBehavior> _activeInstances = new List<NPCFlockingBehavior>();

        private LocomotionAgent _locomotionAgent;
        private NPCAnimationBridge _animationBridge;
        private PlayerCombatState _combatState;
        private float _evaluationTimer;
        private float _flockingStartTime;
        private bool _isFlocking;
        // v2: Tracks what direction was last actually issued to LocomotionAgent — see
        // _minRedirectAngle above for why this exists.
        private Vector3 _lastIssuedDirection;
        private bool _hasIssuedDirection;
        // v4: The specific pair that caused the CURRENT flocking episode — only ITS
        // ReturnCondition is checked while active, not every pair's return condition.
        private FlockTriggerPair _activeTriggerPair;
        // v6: Where THIS flocking episode began — Max Flee Distance is measured from here.
        private Vector3 _flockOrigin;
        // v6: True while parked at Max Flee Distance, playing a waiting animation instead of
        // actively fleeing further.
        private bool _isWaitingAtMaxDistance;

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
            // v6: OPTIONAL — used only for the Waiting Animation feature. Null-safe everywhere.
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
            // v5 FIX: checked every frame (cheap boolean read) instead of only on the throttled
            // evaluation tick — waiting for the next tick after the agent stopped moving left a
            // visible pause of up to _evaluationInterval seconds each time it reached its
            // current lookahead point, which is what "stops from time to time while fleeing"
            // was. Trigger/return-condition checks and direction-based redirects below still
            // run on the normal throttled cadence — only the "has it stopped?" catch is per-frame.
            if (_isFlocking && !_locomotionAgent.IsMoving)
            {
                UpdateFleeSteering();
            }

            _evaluationTimer += Time.deltaTime;
            if (_evaluationTimer < _evaluationInterval) return;
            _evaluationTimer = 0f;

            if (_isFlocking)
            {
                if (CheckActivePairReturnCondition())
                {
                    EndFlocking();
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

        /// <summary>
        /// v4: Checks ONLY _activeTriggerPair's own ReturnCondition — not every configured
        /// pair's — so an unrelated pair's return condition can never end an episode it didn't
        /// start. If the active pair has no ReturnCondition assigned at all, treats that as
        /// "never automatically returns" (false) rather than defaulting to true, so an
        /// intentionally-open-ended trigger doesn't silently do nothing.
        /// </summary>
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
            // v6: Recorded fresh each episode — Max Flee Distance is measured from HERE, not a
            // fixed world point, so it works correctly no matter where the trigger fires.
            _flockOrigin = transform.position;
            _isWaitingAtMaxDistance = false;
            // v2: Reset so the first destination of a fresh flocking episode is always issued
            // immediately, regardless of what direction was pursued last time (if any).
            _hasIssuedDirection = false;
            OnFlockingStarted?.Invoke();
            // Kick off immediately instead of waiting a full evaluation interval to move.
            UpdateFleeSteering();
        }

        private void EndFlocking()
        {
            _isFlocking = false;
            _activeTriggerPair = null;
            _hasIssuedDirection = false;

            // v6: If flocking ends WHILE parked at Max Flee Distance, explicitly release the
            // waiting animation's Reactions-layer mask before route-following resumes —
            // otherwise it would keep masking the Locomotion Blend Tree even after this NPC
            // starts moving again (the same class of bug the v17 NPCAnimationBridge fix
            // addressed for ambient reactions).
            // v7 FIX: SetAnimationState() (called by EnterWaitingAtMaxDistance) pauses movement
            // as a side effect (PauseForInteraction()) — ReleaseReactionOverride() alone only
            // releases the ANIMATION mask, it never touches movement by design (that pairing is
            // normally owned by whichever caller paused it). Without this explicit Resume()
            // call, the NavMeshAgent stayed paused forever after a waiting episode — the
            // animation correctly reverted and OnFlockingEnded correctly fired, but the NPC
            // never actually moved again.
            if (_isWaitingAtMaxDistance)
            {
                _animationBridge?.ReleaseReactionOverride();
                _locomotionAgent.Resume();
                _isWaitingAtMaxDistance = false;
            }

            OnFlockingEnded?.Invoke();
        }

        private void UpdateFleeSteering()
        {
            // v6: Max Flee Distance check — measured from _flockOrigin, not a fixed world
            // point. Reaching it does NOT end flocking (the return condition still decides
            // that) — it only stops outward movement and plays a waiting animation instead.
            if (_maxFleeDistance > 0f)
            {
                float distanceFromOrigin = Vector3.Distance(transform.position, _flockOrigin);
                if (distanceFromOrigin >= _maxFleeDistance)
                {
                    if (!_isWaitingAtMaxDistance)
                    {
                        EnterWaitingAtMaxDistance();
                    }
                    return; // Don't issue a new flee destination while waiting.
                }

                if (_isWaitingAtMaxDistance)
                {
                    // Pulled back under the threshold (e.g. by cohesion) — resume active
                    // fleeing. Same v7 fix as EndFlocking(): ReleaseReactionOverride() alone
                    // never resumes movement, since SetAnimationState() paused it as a side
                    // effect when the waiting animation started.
                    _isWaitingAtMaxDistance = false;
                    _animationBridge?.ReleaseReactionOverride();
                    _locomotionAgent.Resume();
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
                // Everything cancelled out (e.g. no neighbors, no known threat position) —
                // fall back to continuing in the current facing direction rather than freezing.
                combined = transform.forward;
            }

            Vector3 desiredDirection = combined.normalized;

            // v3 FIX: v2's angle-only check had a gap — when direction DIDN'T change enough to
            // redirect, the NPC kept walking toward the SAME lookahead point and eventually
            // reached it. NavMeshAgent halts on its own arrival regardless of
            // _currentLegShouldStop (that flag only gates the deceleration ramp/Stop flourish,
            // not whether the agent physically stops at its destination) — with nothing to
            // redirect it, it just sat there until the next tick happened to swing far enough,
            // producing the exact "step, arrive, stall, step again" pattern, independent of
            // _minRedirectAngle's value. Now ALSO redirects immediately whenever the agent has
            // stopped moving, so it can never sit idle mid-flee.
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
        /// v6: Called once, the instant Max Flee Distance is first reached — halts movement and
        /// plays a random pose from _waitingAnimationStates with useTimer: false (no
        /// auto-revert; see the class-level v6 comment for why). Safe no-op if no waiting
        /// animations are configured — the NPC simply stands still at max distance instead.
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
                // Closer neighbors push harder — inverse-distance weighting, standard boids technique.
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