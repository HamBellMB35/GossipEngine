using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEditor.Animations;
using TownsPeople.Data;

namespace TownsPeople.GamePlay
{
    /// <summary>
    /// Core movement engine for locomotion-driven NPCs. Drives movement directly via
    /// NavMeshAgent. Animation blending is fed from the agent's own real velocity. Also
    /// supports a live per-pose State Speed Multiplier — distinct from each clip's own Time
    /// Scale — synced from a selected Blend Tree's actual children via this component's custom
    /// Inspector, so the number/names of poses adapts to whatever tree you point it at.
    ///
    /// Fully optional/removable, as an add-on: nothing outside the Locomotion system
    /// (NPCGossipMemory, NPCProximityGossip, NPCReputationOpinion, etc.) references or depends
    /// on this component in any way. Delete it off an NPC and every other system keeps working
    /// exactly as before — the NPC simply stands still.
    /// </summary>
    // v2: Implements INpcMovementController (a Core-defined interface) so core interaction code
    // (NPCProximityGossip) can pause/resume this NPC's movement and check whether it's
    // currently running, without Core taking any compile-time dependency on this add-on.
    // Pause/Resume forward straight to the existing Pause()/Resume() methods — no new movement
    // logic, just a Core-facing contract wrapped around what already existed. IsRunning tracks
    // a new _currentSpeedTier field, set alongside _currentLegTargetSpeed in MoveTo().
    //
    // v3: Turning + Stopping animations, for a more natural-feeling walk/run.
    // - TURNING: new Turn float parameter, computed every frame from the NavMeshAgent's
    //   velocity in this NPC's own LOCAL space (the standard technique used by most Unity
    //   third-person locomotion systems) — non-zero any time the agent's actual velocity points
    //   somewhat sideways relative to current facing, i.e. exactly the window where NavMeshAgent's
    //   own rotation (turning to face the desired direction) hasn't fully caught up yet. Intended
    //   to drive a 2D Freeform Blend Tree (Speed × Turn) on the Animator Controller side — see
    //   the Editor setup checklist provided alongside this script.
    // - STOPPING: a discrete, ONE-SHOT Stop animation (not part of the blend tree — a "plant the
    //   foot and stop" clip is inherently transient, not a sustained pose), triggered via the
    //   existing NPCAnimationBridge.SetAnimationState() the moment this NPC arrives at a
    //   waypoint from a meaningful speed. Reuses NPCAnimationBridge's Reactions-layer masking
    //   entirely as-is — inherits the v17 pause/resume-aware RevertToRestingState() behavior for
    //   free, no new plumbing required there. Skipped if arrival speed was already near zero
    //   (nothing dramatic to punctuate) or if no Stop State Name is configured.
    // v3.2: Smoothed both the physical arrival (ComputeArrivalSpeedMultiplier, an ease-in/out
    // ramp toward 0 as the NPC nears its destination) and the visual blend (damped SetFloat for
    // Speed/Turn instead of the instant overload) — REVISED in v4, see below.
    //
    // v4 FIX: v3.2's arrival deceleration applied to EVERY waypoint, including plain path
    // corners that were only ever meant to be flowed through — every single stop along a route
    // now visibly slowed down, which reads as worse than the original instant-halt behavior for
    // any waypoint that isn't actually meant to be a stopping point. Fixed by making
    // deceleration (and the Stop animation) conditional on the waypoint actually being a Point
    // of Interest (see LocomotionWaypoint.IsPointOfInterest) — a plain waypoint now flows
    // through at full speed with zero deceleration, exactly like before v3.2 ever existed. Added
    // a MoveTo(LocomotionWaypoint, LocomotionSpeedTier) overload that resolves this decision
    // (including rolling RandomChance fresh on every visit); the original
    // MoveTo(Vector3, LocomotionSpeedTier) overload is preserved for raw-destination use and
    // always behaves as a pass-through leg. Full POI behavior (lingering, facing a direction,
    // etc.) is intentionally NOT built here — that's a separate, future mechanic layered on top
    // of these same fields; this version only decides stop-or-flow-through.
    //
    // v5: The stop decision moved from IsPointOfInterest to the pre-existing LingerDuration
    // field instead (0 = flow through, > 0 = decelerate/stop/wait) — simpler, and reuses a field
    // that already existed for exactly this purpose. IsPointOfInterest/StopBehavior/StopChance
    // remain in LocomotionWaypoint untouched, for the separate future POI mechanic — they no
    // longer drive stopping themselves. Also: arrival now actually WAITS for LingerDuration
    // seconds (via a coroutine) before firing OnArrivedAtDestination — previously nothing waited
    // at all, so this field had never had any real effect despite existing since Phase 1.
    //
    // v6 FIX: The Editor-side dedup added to fix v2's threshold-collapse bug (2D trees) fixed
    // the multiplier's ACCURACY but at the cost of silently dropping every Turn-variant pose
    // sharing a Speed value with another pose — not acceptable; every pose needs its own real
    // multiplier. Replaced the entire mechanism: instead of interpolating a single value along
    // the Speed axis, ComputeLiveClipMultiplier() reads Unity's own live blend weights
    // (Animator.GetCurrentAnimatorClipInfo) each frame, finds whichever clip is CURRENTLY
    // dominant in the blend, and uses THAT clip's own configured Multiplier directly. This is
    // blend-tree-dimensionality-agnostic (works the same for 1D, 2D, or any future N-D setup)
    // and every pose keeps its own independent value — nothing collapsed, nothing approximated.
    // LocomotionAgentEditor's sync no longer deduplicates by Speed — every Blend Tree child gets
    // its own row again.
    //
    // v7: Corner anticipation, for curved turns instead of sharp pivots. LocomotionAgent is
    // leg-based — it never called SetDestination() for the NEXT waypoint until fully arrived at
    // the current one, so NavMeshAgent's path was always a strict polyline: walk dead straight
    // into a corner, stop-ish, pivot onto the next straight segment. New OnApproachingDestination
    // event fires once, BEFORE full arrival, for a pass-through leg only (never a POI/stop leg —
    // those still fully stop as intended) — the driver (LocomotionTester) redirects to the next
    // waypoint right then, while still approaching the current one. Redirecting a NavMeshAgent
    // mid-approach makes it recompute a smooth path toward a point beyond the corner instead of
    // into it, which is what actually curves the trajectory — no change to the underlying
    // steering/pathfinding itself, just WHEN the next destination is issued.
    [RequireComponent(typeof(NavMeshAgent))]
    public class LocomotionAgent : MonoBehaviour, INpcMovementController
    {
        [Header("Per-NPC Speed Tiers")]
        [Tooltip("Exact movement speed (world units/sec) this NPC uses when a waypoint's Arrival Speed Tier is Walk. Also the 0.5 threshold for Blend Tree pose selection.")]
        [SerializeField] private float _walkSpeed = 1.6f;

        [Tooltip("Exact movement speed (world units/sec) this NPC uses when a waypoint's Arrival Speed Tier is Run. Also the 1.0 threshold for Blend Tree pose selection.")]
        [SerializeField] private float _runSpeed = 4.5f;

        [Header("Animation")]
        [Tooltip("Animator driving this NPC's locomotion blend. Auto-resolved from this GameObject's NPCAnimationBridge (or its children) the moment this component is added.")]
        [SerializeField] private Animator _animator;

        [Tooltip("Float parameter on the Animator Controller driving the Idle/Walk/Run blend (0 = idle, 0.5 = Walk Speed, 1 = Run Speed). Set up as a 1D Blend Tree on the Base Layer with thresholds 0/0.5/1.")]
        [AnimatorParameterName(nameof(_animator), AnimatorControllerParameterType.Float)]
        [SerializeField] private string _speedParameterName = "Speed";

        [Tooltip("v3: Float parameter driving how sharply this NPC is CURRENTLY turning (-1 = full left, 0 = straight, 1 = full right), computed every frame from the NavMeshAgent's velocity in local space. Intended as the second axis (alongside Speed) of a 2D Freeform Blend Tree, blending in Turn Left/Right locomotion cycles. Leave empty to disable — the Speed-only 1D blend keeps working exactly as before.")]
        [AnimatorParameterName(nameof(_animator), AnimatorControllerParameterType.Float)]
        [SerializeField] private string _turnParameterName = "Turn";

        [Header("Stop Animation")]
        [Tooltip("v3: Name of a discrete, one-shot Stop animation state, played via NPCAnimationBridge.SetAnimationState() the moment this NPC arrives at a waypoint from a meaningful speed. Leave empty to disable — arrival will just settle the blend tree back to Idle with no dedicated stop flourish, the pre-v3 behavior.")]
        [AnimatorStateName(nameof(_animator))]
        [SerializeField] private string _stopStateName = "";

        [Tooltip("v3: Minimum normalized speed (0 = idle, 0.5 = Walk Speed, 1 = Run Speed) this NPC must have been moving at, at the exact moment it arrives, for the Stop animation to play. Arriving slower than this plays no Stop flourish — there's nothing dramatic to punctuate.")]
        [SerializeField, Range(0f, 1f)] private float _stopAnimationMinNormalizedSpeed = 0.3f;

        [Serializable]
        public struct PosePlaybackRate
        {
            public string MotionName;
            // v6: Display-only now for a 2D tree (no longer drives interpolation — see
            // ComputeLiveClipMultiplier()). Still the real interpolation key for a 1D tree, and
            // still useful as an at-a-glance Speed-axis reference either way.
            public float Threshold;
            public float Multiplier;
        }

        [Header("Per-Pose Playback Rate (State Speed Multiplier)")]
        [Tooltip("v6: Synced from a selected Blend Tree via this component's custom Inspector (Blend Tree section) — ONE entry per motion in the tree, no motions collapsed or dropped (including Turn variants sharing a Speed value on a 2D tree). Edit each Multiplier directly there. At runtime, whichever clip is CURRENTLY dominant in the live blend uses its own Multiplier — see ComputeLiveClipMultiplier().")]
        [SerializeField] private List<PosePlaybackRate> _posePlaybackRates = new List<PosePlaybackRate>();

        public List<PosePlaybackRate> PosePlaybackRates => _posePlaybackRates;

        [Tooltip("Float parameter bound to the Locomotion state's Speed > Multiplier > Parameter field in the Animator Controller. Leave empty to disable this feature entirely.")]
        [AnimatorParameterName(nameof(_animator), AnimatorControllerParameterType.Float)]
        [SerializeField] private string _stateSpeedMultiplierParameterName = "";

        [Tooltip("v6: Which Animator layer this NPC's Locomotion state lives on — used only to read live blend weights for the Speed Multiplier feature (GetCurrentAnimatorClipInfo). Base Layer (0) matches this project's standard NPC_GossipAnimator convention; change only if your own Controller places Locomotion on a different layer.")]
        [SerializeField] private int _locomotionLayerIndex = 0;

        [Header("Animation Smoothing")]
        [Tooltip("v3.2: How long (seconds) the Speed and Turn Animator parameters take to smoothly ease toward their actual target values, instead of snapping instantly every frame. This is what makes blend tree transitions (including arriving/stopping) feel smooth rather than rough. Higher = smoother but more lag between real movement and the animation catching up. 0 = instant, the pre-v3.2 behavior.")]
        [SerializeField, Range(0f, 0.5f)] private float _animatorParameterDampTime = 0.15f;

        [Header("Arrival Deceleration")]
        [Tooltip("v3.2: If enabled, this NPC smoothly slows its TARGET speed down as it approaches its destination, instead of moving at full speed right up until arrival. Deliberately separate from NavMeshAgent's own Auto Braking (kept OFF — it would fight this and the existing Turn Anticipation slowdown, double-applying deceleration unpredictably).")]
        [SerializeField] private bool _decelerateOnArrival = true;

        [Tooltip("v13: How far (world units) before the destination this NPC starts smoothly decelerating, tuned against WALK SPEED. Run automatically gets a proportionally larger effective distance based on the Run/Walk speed ratio — one value now works for both tiers. Larger = a longer, gentler slowdown; smaller = a shorter, more sudden one.")]
        [SerializeField] private float _arrivalDecelerationDistance = 1.5f;

        [Header("Corner Anticipation")]
        [Tooltip("v7: How far (world units) before a PLAIN (pass-through) waypoint this NPC signals it's ready for the next leg (OnApproachingDestination), instead of waiting for exact arrival. Lets the driver redirect toward the next waypoint early, so NavMeshAgent naturally curves through the corner instead of pivoting sharply at a dead stop. Never applies to a POI/stop waypoint (LingerDuration > 0) — those still fully arrive and stop as intended. 0 disables this entirely, reverting to the original sharp-corner behavior.")]
        [SerializeField] private float _cornerAnticipationDistance = 2f;

        [Header("Avoidance")]
        [Tooltip("Lower values yield right-of-way to NPCs with higher priority (lower number) when paths conflict — Unity's built-in NavMesh local avoidance handles the actual steering/waiting/veering. Range 0-99.")]
        [SerializeField, Range(0, 99)] private int _avoidancePriority = 50;

        [Tooltip("How close (world units) counts as 'arrived' at the current destination.")]
        [SerializeField] private float _arrivalThreshold = 0.15f;

        [Header("Route Assignment")]
        [Tooltip("The LocomotionRoute this NPC currently walks. Lives on its own separate GameObject — shareable across multiple NPCs.")]
        [SerializeField] private LocomotionRoute _assignedRoute;

        [Header("Movement Responsiveness")]
        [Tooltip("How quickly this NPC's actual velocity ramps toward its target speed (NavMeshAgent.Acceleration) — the sole mechanism controlling that ramp. Higher = reaches full speed faster.")]
        [SerializeField] private float _acceleration = 20f;

        [Tooltip("How quickly this NPC turns to face a new direction (NavMeshAgent.Angular Speed, degrees/sec).")]
        [SerializeField] private float _angularSpeed = 360f;

        [Header("Turn Anticipation")]
        [Tooltip("If enabled, this NPC slows down approaching a sharp turn in its path and speeds back up after.")]
        [SerializeField] private bool _slowForTurns = true;

        [Tooltip("Turns sharper than this angle (degrees) trigger slowing. 180 = straight ahead.")]
        [SerializeField, Range(1f, 179f)] private float _turnAngleThreshold = 45f;

        [Tooltip("How far (world units) before a sharp turn this NPC starts slowing down.")]
        [SerializeField] private float _turnAnticipationDistance = 2.5f;

        [Tooltip("Speed multiplier at the sharpest point of a turn (1 = no slowdown, 0.4 = slows to 40% speed).")]
        [SerializeField, Range(0.1f, 1f)] private float _minTurnSpeedMultiplier = 0.4f;

        [Header("Root Motion (Experimental — off by default)")]
        [Tooltip("EXPERIMENTAL. When enabled, physical position comes from the Animator's own root motion instead of NavMeshAgent — Walk/Run Speed, Acceleration, and Turn Anticipation all become non-functional for actual movement. Not recommended currently.")]
        [SerializeField] private bool _useRootMotion = false;

        [Tooltip("Only relevant if Use Root Motion is enabled — manual vertical correction for a NavMesh-bake-vs-floor height mismatch.")]
        [SerializeField] private float _groundHeightCorrection = 0f;

        private const float VelocitySmoothingTime = 0.15f;

        public LocomotionRoute AssignedRoute => _assignedRoute;

        private NavMeshAgent _agent;
        // v3: Resolved alongside the Animator in Awake() — used solely to trigger the discrete
        // Stop animation on arrival. NPCAnimationBridge is Core; a direct reference here follows
        // the exact same established direction as ResolveAnimatorIfNeeded() below.
        private NPCAnimationBridge _animationBridge;
        private float _currentLegTargetSpeed;
        // v2: Tracks which speed tier the CURRENT leg was assigned (set in MoveTo(), alongside
        // _currentLegTargetSpeed). Used by IsRunning below — separate from the numeric target
        // speed since two different NPCs' Walk/Run values could otherwise be ambiguous to
        // compare against.
        private LocomotionSpeedTier _currentSpeedTier;
        private bool _isMoving;
        private bool _hasArrivedThisLeg;
        private float _currentEffectiveSpeed;
        private Vector3 _smoothedAgentVelocity;
        // v3: The most recently computed normalized speed (0/0.5/1 piecewise, same scale as the
        // Speed parameter) — captured here so the arrival check can know how fast this NPC was
        // actually moving at the exact moment it stopped, without recomputing it separately.
        private float _lastNormalizedSpeed;
        // v5: Whether the CURRENT leg should decelerate/stop on arrival — resolved once, when
        // the leg begins, from waypoint.LingerDuration > 0 (see the MoveTo(LocomotionWaypoint, ...)
        // overload below). False for every leg started via the raw-destination
        // MoveTo(Vector3, ...) overload.
        private bool _currentLegShouldStop;
        // v5: How long (seconds) to actually WAIT once stopped, before firing
        // OnArrivedAtDestination — 0 for any leg that doesn't stop at all.
        private float _currentLegLingerDuration;
        // v5: The currently-running linger wait, if any — cancelled by Stop() or a new
        // BeginLeg(), so a pending linger can never fire OnArrivedAtDestination after this NPC
        // has already been redirected elsewhere.
        private Coroutine _lingerCoroutine;
        // v7: Guards OnApproachingDestination so it fires exactly ONCE per pass-through leg —
        // reset in BeginLeg() for the new leg.
        private bool _hasFiredApproachThisLeg;

        public event Action OnArrivedAtDestination;
        // v7: Fires ONCE, before full arrival, ONLY for a pass-through (non-stop) leg — see
        // _cornerAnticipationDistance below for why. A POI/stop leg never fires this; it only
        // ever fires OnArrivedAtDestination, once it has actually fully stopped.
        public event Action OnApproachingDestination;

        public bool IsMoving => _isMoving;
        public bool IsPaused => _agent.isStopped;
        // v5: Renamed from CurrentLegIsPointOfInterestStop (v4) — the stop decision is now
        // driven by LingerDuration, not IsPointOfInterest, so the old name no longer described
        // what this actually reflects. True the instant this NPC has stopped and is lingering.
        public bool CurrentLegWillStop => _currentLegShouldStop;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _agent.avoidancePriority = _avoidancePriority;

            ResolveAnimatorIfNeeded();
            // v3: Resolved once here — used only by TryPlayStopAnimation() on arrival.
            _animationBridge = GetComponent<NPCAnimationBridge>();

            if (_agent.obstacleAvoidanceType == ObstacleAvoidanceType.NoObstacleAvoidance)
            {
                _agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
            }

            _agent.stoppingDistance = _arrivalThreshold;
            _agent.acceleration = _acceleration;
            _agent.angularSpeed = _angularSpeed;
            _agent.autoBraking = false;

            if (_useRootMotion)
            {
                _agent.updatePosition = false;
                if (_animator != null) _animator.applyRootMotion = true;
            }

            _currentEffectiveSpeed = _walkSpeed;
            _agent.speed = _currentEffectiveSpeed;
        }

        private void ResolveAnimatorIfNeeded()
        {
            if (_animator != null) return;

            NPCAnimationBridge bridge = GetComponent<NPCAnimationBridge>();
            if (bridge != null && bridge.Animator != null)
            {
                _animator = bridge.Animator;
                return;
            }

            _animator = GetComponent<Animator>();
            if (_animator == null) _animator = GetComponentInChildren<Animator>();
        }

        /// <summary>
        /// v4: Raw-destination overload — always begins a PASS-THROUGH leg (no deceleration, no
        /// Stop flourish, no linger wait on arrival), regardless of what's actually at that
        /// position. Use the LocomotionWaypoint overload below for linger-aware behavior.
        /// </summary>
        public void MoveTo(Vector3 destination, LocomotionSpeedTier speedTier)
        {
            BeginLeg(destination, speedTier, lingerDuration: 0f);
        }

        /// <summary>
        /// v5: Waypoint-aware overload — reads the waypoint's own LingerDuration directly
        /// (0 = flow through, exactly like a plain path corner; greater than 0 = decelerate,
        /// stop, wait that long, then continue). This is what LocomotionTester (and any future
        /// behavior driving a LocomotionRoute) should call.
        /// </summary>
        public void MoveTo(LocomotionWaypoint waypoint, LocomotionSpeedTier speedTier)
        {
            float lingerDuration = waypoint != null ? waypoint.LingerDuration : 0f;
            BeginLeg(waypoint.Position, speedTier, lingerDuration);
        }

        private void BeginLeg(Vector3 destination, LocomotionSpeedTier speedTier, float lingerDuration)
        {
            // v5: A leg that was still lingering from a PREVIOUS arrival (shouldn't normally
            // happen, since nothing should call MoveTo() again before OnArrivedAtDestination
            // fires — but defensive regardless) must not be allowed to fire
            // OnArrivedAtDestination late, after this NPC has already been redirected.
            if (_lingerCoroutine != null)
            {
                StopCoroutine(_lingerCoroutine);
                _lingerCoroutine = null;
            }

            _currentLegTargetSpeed = speedTier == LocomotionSpeedTier.Run ? _runSpeed : _walkSpeed;
            // v2: Recorded alongside the numeric target speed — this is what IsRunning reads.
            _currentSpeedTier = speedTier;
            // v5: Decided once per leg, here — read by Update()'s deceleration/Stop-flourish gate
            // and by the arrival branch to decide whether/how long to linger.
            _currentLegLingerDuration = lingerDuration;
            _currentLegShouldStop = lingerDuration > 0f;
            // v7: Reset for the new leg — OnApproachingDestination can fire once more.
            _hasFiredApproachThisLeg = false;

            // v14 FIX: unconditionally clears any lingering isStopped pause, regardless of what
            // set it — TryPlayStopAnimation()'s SetAnimationState() call pauses movement as a
            // side effect (PauseForInteraction()) whenever it plays a reaction, and that pause
            // was never guaranteed to be matched by a Resume() before the next leg begins. A
            // real arrival could look complete (_isMoving=true, a fresh destination set) while
            // the agent stayed permanently paused underneath, unable to actually move. BeginLeg()
            // represents "start moving" — it should always guarantee the agent isn't paused,
            // rather than depending on every possible pause path remembering to resume it first.
            _agent.isStopped = false;

            _agent.SetDestination(destination);
            _isMoving = true;
            _hasArrivedThisLeg = false;
        }

        public void Stop()
        {
            _isMoving = false;
            _agent.ResetPath();
            // v11 FIX: ResetPath() clears the destination but doesn't zero residual velocity —
            // the agent could keep coasting forward on its remaining momentum for a moment even
            // after being told to stop, while any animation switch (e.g. a flocking NPC's
            // waiting/scared pose) already looks fully static. That mismatch reads as a visible
            // slide across the floor. Explicit zero here guarantees an instant, hard stop.
            _agent.velocity = Vector3.zero;

            // v5: Cancel any pending linger wait — Stop() means this NPC's route has been
            // interrupted; a stale linger firing OnArrivedAtDestination afterward would be wrong.
            if (_lingerCoroutine != null)
            {
                StopCoroutine(_lingerCoroutine);
                _lingerCoroutine = null;
            }
        }

        public void Pause()
        {
            _agent.isStopped = true;
        }

        public void Resume()
        {
            _agent.isStopped = false;
        }

        // ---------- INpcMovementController ----------

        /// <summary>
        /// v2: True only while actually moving, not paused, and on the Run tier — an NPC that
        /// has arrived, is idle, or is paused (e.g. already mid-interaction) is never "running,"
        /// even if its last assigned leg was a Run leg.
        /// </summary>
        public bool IsRunning => _isMoving && !IsPaused && _currentSpeedTier == LocomotionSpeedTier.Run;

        /// <summary>
        /// v2: INpcMovementController entry point — forwards straight to the existing Pause().
        /// Kept as a separate method (rather than exposing Pause() directly to the interface)
        /// so the interface's intent stays self-documenting at the call site in core code.
        /// </summary>
        public void PauseForInteraction() => Pause();

        /// <summary>v2: INpcMovementController entry point — forwards straight to the existing Resume().</summary>
        public void ResumeAfterInteraction() => Resume();

        /// <summary>EXPERIMENTAL — only meaningful if Use Root Motion is enabled.</summary>
        public void ReceiveRootMotion()
        {
            if (!_useRootMotion || _animator == null) return;

            Vector3 position = _animator.rootPosition;
            float baseGroundY = _agent.isOnNavMesh ? _agent.nextPosition.y : transform.position.y;
            position.y = baseGroundY + _groundHeightCorrection;
            transform.position = position;

            if (_agent.isOnNavMesh)
            {
                _agent.nextPosition = new Vector3(transform.position.x, baseGroundY, transform.position.z);
            }
        }

        private void Update()
        {
            UpdateMovementAnimation();

            if (IsPaused || !_isMoving || _hasArrivedThisLeg) return;
            if (_agent.pathPending) return;
            if (!_agent.isOnNavMesh) return;

            if (_agent.pathStatus == NavMeshPathStatus.PathInvalid)
            {
                Debug.LogWarning($"<color=orange>[LocomotionAgent]</color> '{gameObject.name}' could not find a path to its destination — is the NavMesh baked and connected here?", this);
                _isMoving = false;
                _hasArrivedThisLeg = true;
                OnArrivedAtDestination?.Invoke();
                return;
            }

            float remainingDistance = _agent.remainingDistance;
            if (float.IsNaN(remainingDistance) || float.IsInfinity(remainingDistance)) return;

            float turnMultiplier = ComputeTurnSpeedMultiplier();
            // v5: Deceleration only applies to a leg with LingerDuration > 0 — a plain
            // pass-through waypoint (LingerDuration == 0) flows through at full speed
            // (multiplier stays 1), exactly like before v3.2 ever existed.
            float arrivalMultiplier = _currentLegShouldStop ? ComputeArrivalSpeedMultiplier(remainingDistance) : 1f;
            SetTargetSpeed(_currentLegTargetSpeed * turnMultiplier * arrivalMultiplier);

            // v7: Fires ONCE, before full arrival, ONLY for a pass-through leg — lets the driver
            // redirect toward the NEXT waypoint early so this NPC curves through the corner
            // instead of walking dead straight into it and pivoting. A POI/stop leg is
            // deliberately excluded (guarded by !_currentLegShouldStop) — those should still
            // fully arrive and stop, not curve past.
            if (!_currentLegShouldStop && !_hasFiredApproachThisLeg
                && _cornerAnticipationDistance > 0f
                && remainingDistance <= _cornerAnticipationDistance
                && remainingDistance > _agent.stoppingDistance)
            {
                _hasFiredApproachThisLeg = true;
                OnApproachingDestination?.Invoke();
            }

            if (remainingDistance <= _agent.stoppingDistance)
            {
                _isMoving = false;
                _hasArrivedThisLeg = true;

                if (_currentLegShouldStop)
                {
                    // v12 FIX: the deceleration ramp gets velocity CLOSE to zero by arrival, not
                    // exactly zero — the residual carries the character a couple more steps
                    // while the animation has already settled into idle, reading as a slide.
                    // Same root cause and fix as the flocking waiting-animation slide (v11 on
                    // Stop()). Deliberately NOT applied to the pass-through (else) branch below —
                    // that arrival is meant to flow continuously into the next leg, and zeroing
                    // velocity there would fight corner anticipation instead of helping.
                    _agent.velocity = Vector3.zero;
                    // v4: Only a real stop plays the Stop flourish — a pass-through waypoint's
                    // instant, imperceptible arrival (no preceding deceleration) doesn't warrant one.
                    TryPlayStopAnimation();
                    // v10: Also drop into a random pose from NPCAnimationBridge's Default Idle
                    // States pool — without this, a route stop just settles into the Locomotion
                    // Blend Tree's single fixed Speed=0 pose (always the same one animation),
                    // never touching the variety pool at all. Same mechanism flocking's waiting
                    // state already uses (PlayIdleForInteraction() -> CrossFades into the
                    // Reactions layer, masking Base Layer). Released in LingerThenNotifyArrived()
                    // below once the wait ends, so the walk animation shows through again.
                    _animationBridge?.PlayIdleForInteraction();
                    // v5: Actually WAIT for LingerDuration before signaling "ready for the next
                    // leg" — previously nothing waited at all; OnArrivedAtDestination fired the
                    // instant arrival was detected regardless of this field.
                    _lingerCoroutine = StartCoroutine(LingerThenNotifyArrived(_currentLegLingerDuration));
                }
                else
                {
                    OnArrivedAtDestination?.Invoke();
                }
            }
        }

        /// <summary>
        /// v5: Waits out this leg's LingerDuration, THEN fires OnArrivedAtDestination — this is
        /// what makes a stop actually pause for the configured duration instead of immediately
        /// signaling readiness for the next leg the instant arrival is physically detected.
        /// </summary>
        private IEnumerator LingerThenNotifyArrived(float duration)
        {
            yield return new WaitForSeconds(duration);
            _lingerCoroutine = null;
            // v10: Release the idle-variety pose's Reactions-layer mask right as the wait ends,
            // so the Locomotion Blend Tree's walk/run animation is visible again once movement
            // actually resumes — same masking pattern used everywhere else in this project.
            _animationBridge?.ReleaseReactionOverride();
            OnArrivedAtDestination?.Invoke();
        }

        /// <summary>
        /// v3: Plays the discrete, one-shot Stop animation via NPCAnimationBridge if this NPC
        /// was moving fast enough at the moment of arrival to warrant one. No-op entirely if no
        /// NPCAnimationBridge is present, no Stop State Name is configured, or arrival speed was
        /// below the configured threshold.
        /// </summary>
        private void TryPlayStopAnimation()
        {
            if (_animationBridge == null || string.IsNullOrEmpty(_stopStateName)) return;
            if (_lastNormalizedSpeed < _stopAnimationMinNormalizedSpeed) return;

            int stopHash = Animator.StringToHash(_stopStateName);
            _animationBridge.SetAnimationState(stopHash);
        }

        private float ComputeTurnSpeedMultiplier()
        {
            if (!_slowForTurns) return 1f;
            if (_agent.path == null) return 1f;

            Vector3[] corners = _agent.path.corners;
            if (corners.Length < 3) return 1f;

            Vector3 currentDir = corners[1] - corners[0];
            Vector3 nextDir = corners[2] - corners[1];
            if (currentDir.sqrMagnitude < 0.0001f || nextDir.sqrMagnitude < 0.0001f) return 1f;

            float turnAngle = Vector3.Angle(currentDir.normalized, nextDir.normalized);
            if (turnAngle < _turnAngleThreshold) return 1f;

            float distanceToCorner = Vector3.Distance(transform.position, corners[1]);
            if (distanceToCorner > _turnAnticipationDistance) return 1f;

            float proximityT = 1f - Mathf.Clamp01(distanceToCorner / _turnAnticipationDistance);
            float sharpnessT = Mathf.Clamp01((turnAngle - _turnAngleThreshold) / (180f - _turnAngleThreshold));
            float slowdownAmount = proximityT * sharpnessT;

            return Mathf.Lerp(1f, _minTurnSpeedMultiplier, slowdownAmount);
        }

        /// <summary>
        /// v3.2: Smoothly ramps target speed toward 0 as this NPC approaches its destination —
        /// deliberately separate from NavMeshAgent's own Auto Braking (kept OFF — it would fight
        /// this and the existing Turn Anticipation slowdown, double-applying deceleration
        /// unpredictably). SmoothStep gives an ease-in/ease-out curve rather than a linear ramp,
        /// which reads as noticeably more natural than a straight-line slowdown.
        /// </summary>
        /// <summary>
        /// v13: Arrival Deceleration Distance is now the value you tune AGAINST WALK SPEED (the
        /// baseline) — Run automatically gets a proportionally larger effective ramp based on
        /// the Run/Walk speed ratio, instead of needing separate manual tuning per tier (the
        /// same world-distance value used to feel abrupt at Run and fine at Walk, since Run
        /// covers that distance in far less time).
        /// </summary>
        private float ComputeArrivalSpeedMultiplier(float remainingDistance)
        {
            if (!_decelerateOnArrival || _arrivalDecelerationDistance <= 0f) return 1f;

            float speedScale = _walkSpeed > 0.0001f ? _currentLegTargetSpeed / _walkSpeed : 1f;
            float effectiveDecelerationDistance = _arrivalDecelerationDistance * speedScale;

            if (remainingDistance >= effectiveDecelerationDistance) return 1f;

            float t = Mathf.Clamp01(remainingDistance / effectiveDecelerationDistance);
            return Mathf.SmoothStep(0f, 1f, t);
        }

        private void SetTargetSpeed(float speed)
        {
            _currentEffectiveSpeed = speed;
            _agent.speed = speed;
        }

        private void UpdateMovementAnimation()
        {
            if (_animator == null) return;

            float measuredSpeed;

            if (_useRootMotion)
            {
                if (!_agent.isOnNavMesh) return;
                Vector3 worldDelta = _agent.nextPosition - transform.position;
                float smoothT = Mathf.Clamp01(Time.deltaTime / VelocitySmoothingTime);
                _smoothedAgentVelocity = Vector3.Lerp(_smoothedAgentVelocity, worldDelta / Mathf.Max(Time.deltaTime, 0.0001f), smoothT);
                measuredSpeed = new Vector3(_smoothedAgentVelocity.x, 0f, _smoothedAgentVelocity.z).magnitude;
            }
            else
            {
                measuredSpeed = _agent.velocity.magnitude;
            }

            UpdateAnimationParameter(measuredSpeed);
        }

        /// <summary>
        /// Feeds two independent Unity mechanisms from the same measured speed: the Speed
        /// blend parameter (0/0.5/1 piecewise, selects WHICH pose is shown), and — if a State
        /// Speed Multiplier parameter is configured — a live per-pose playback rate delivered
        /// to the Locomotion state's own native Speed > Multiplier > Parameter binding.
        /// </summary>
        private void UpdateAnimationParameter(float speed)
        {
            if (_animator == null) return;

            float normalizedSpeed;

            if (speed <= 0f || _walkSpeed <= 0.0001f)
            {
                normalizedSpeed = 0f;
            }
            else if (speed <= _walkSpeed)
            {
                normalizedSpeed = Mathf.Lerp(0f, 0.5f, speed / _walkSpeed);
            }
            else if (_runSpeed > _walkSpeed)
            {
                float runPhaseProgress = Mathf.Clamp01((speed - _walkSpeed) / (_runSpeed - _walkSpeed));
                normalizedSpeed = Mathf.Lerp(0.5f, 1f, runPhaseProgress);
            }
            else
            {
                normalizedSpeed = 0.5f;
            }

            if (!string.IsNullOrEmpty(_speedParameterName))
            {
                // v3.2: Damped overload instead of the instant one — Unity exponentially eases
                // the parameter toward normalizedSpeed over _animatorParameterDampTime seconds,
                // rather than snapping the blend tree's position every single frame. This is the
                // actual fix for "rough" blend tree feel, especially noticeable while stopping.
                _animator.SetFloat(_speedParameterName, normalizedSpeed, _animatorParameterDampTime, Time.deltaTime);
            }

            // v3: Recorded regardless of whether Speed Parameter Name is actually configured —
            // TryPlayStopAnimation() needs this reading independent of the Animator wiring.
            _lastNormalizedSpeed = normalizedSpeed;

            // v3: Second axis of a 2D Freeform Blend Tree (Speed × Turn), if configured.
            if (!string.IsNullOrEmpty(_turnParameterName))
            {
                // v3.2: Same damping as Speed above — an un-damped Turn value would still look
                // snappy even with Speed smoothed, since they drive the same blend tree together.
                _animator.SetFloat(_turnParameterName, ComputeTurnParameter(), _animatorParameterDampTime, Time.deltaTime);
            }

            if (!string.IsNullOrEmpty(_stateSpeedMultiplierParameterName))
            {
                // v6: Live dominant-clip lookup instead of Speed-axis interpolation — see
                // ComputeLiveClipMultiplier() for why.
                float multiplier = ComputeLiveClipMultiplier();
                _animator.SetFloat(_stateSpeedMultiplierParameterName, multiplier);
            }
        }

        /// <summary>
        /// v3: Standard local-space-velocity technique for driving a turning blend — how far the
        /// NavMeshAgent's ACTUAL velocity points sideways relative to this NPC's CURRENT facing,
        /// normalized against Walk Speed and clamped to [-1, 1]. Non-zero any time NavMeshAgent's
        /// own rotation (turning to face its desired direction) hasn't fully caught up yet —
        /// exactly the transient window a 2D blend tree needs in order to visibly show a turn.
        /// Naturally decays back to 0 once the NPC finishes rotating to face its new heading.
        /// </summary>
        private float ComputeTurnParameter()
        {
            Vector3 localVelocity = transform.InverseTransformDirection(_agent.velocity);
            float normalized = _walkSpeed > 0.0001f ? localVelocity.x / _walkSpeed : 0f;
            return Mathf.Clamp(normalized, -1f, 1f);
        }

        /// <summary>
        /// v6: Reads whichever clip currently has the HIGHEST blend weight on
        /// _locomotionLayerIndex — the dominant pose right now — and returns THAT clip's own
        /// configured Multiplier from _posePlaybackRates (matched by clip name). Works
        /// identically for a 1D or 2D (or N-D) blend tree, since it's driven entirely by
        /// Unity's own live blend weights rather than hand-rolled axis interpolation — every
        /// pose gets its own real multiplier, nothing is collapsed or approximated from a
        /// neighboring pose. Replaces the old Speed-axis-only bracket interpolation, which could
        /// never correctly handle a 2D tree's Turn-axis variants.
        /// </summary>
        private float ComputeLiveClipMultiplier()
        {
            if (_posePlaybackRates == null || _posePlaybackRates.Count == 0 || _animator == null) return 1f;

            AnimatorClipInfo[] clipInfos = _animator.GetCurrentAnimatorClipInfo(_locomotionLayerIndex);
            if (clipInfos == null || clipInfos.Length == 0) return 1f;

            AnimatorClipInfo dominant = clipInfos[0];
            for (int i = 1; i < clipInfos.Length; i++)
            {
                if (clipInfos[i].weight > dominant.weight) dominant = clipInfos[i];
            }
            if (dominant.clip == null) return 1f;

            foreach (PosePlaybackRate rate in _posePlaybackRates)
            {
                if (rate.MotionName == dominant.clip.name) return rate.Multiplier;
            }

            // Dominant clip has no synced entry (e.g. sync hasn't been re-run since a new pose
            // was added to the tree) — default to no change rather than guessing.
            return 1f;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_agent == null) _agent = GetComponent<NavMeshAgent>();
            if (_agent != null)
            {
                _agent.avoidancePriority = _avoidancePriority;
                _agent.acceleration = _acceleration;
                _agent.angularSpeed = _angularSpeed;
            }
            ResolveAnimatorIfNeeded();
        }

        private void Reset()
        {
            ResolveAnimatorIfNeeded();
        }
#endif
    }
}