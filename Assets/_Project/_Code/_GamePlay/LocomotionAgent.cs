using System;
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
    [RequireComponent(typeof(NavMeshAgent))]
    public class LocomotionAgent : MonoBehaviour
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

        [Serializable]
        public struct PosePlaybackRate
        {
            public string MotionName;
            public float Threshold;
            public float Multiplier;
        }

        [Header("Per-Pose Playback Rate (State Speed Multiplier)")]
        [Tooltip("Synced from a selected Blend Tree via this component's custom Inspector (Blend Tree section) — one entry per motion in that tree, in Threshold order. Edit each Multiplier directly there. Only meaningful if the SAME tree driving this NPC's Speed parameter was selected — the runtime interpolation below is based on that tree's live blend position.")]
        [SerializeField] private List<PosePlaybackRate> _posePlaybackRates = new List<PosePlaybackRate>();

        public List<PosePlaybackRate> PosePlaybackRates => _posePlaybackRates;

        [Tooltip("Float parameter bound to the Locomotion state's Speed > Multiplier > Parameter field in the Animator Controller. Leave empty to disable this feature entirely.")]
        [AnimatorParameterName(nameof(_animator), AnimatorControllerParameterType.Float)]
        [SerializeField] private string _stateSpeedMultiplierParameterName = "";

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
        private float _currentLegTargetSpeed;
        private bool _isMoving;
        private bool _hasArrivedThisLeg;
        private float _currentEffectiveSpeed;
        private Vector3 _smoothedAgentVelocity;

        public event Action OnArrivedAtDestination;

        public bool IsMoving => _isMoving;
        public bool IsPaused => _agent.isStopped;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _agent.avoidancePriority = _avoidancePriority;

            ResolveAnimatorIfNeeded();

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

        public void MoveTo(Vector3 destination, LocomotionSpeedTier speedTier)
        {
            _currentLegTargetSpeed = speedTier == LocomotionSpeedTier.Run ? _runSpeed : _walkSpeed;

            _agent.SetDestination(destination);
            _isMoving = true;
            _hasArrivedThisLeg = false;
        }

        public void Stop()
        {
            _isMoving = false;
            _agent.ResetPath();
        }

        public void Pause()
        {
            _agent.isStopped = true;
        }

        public void Resume()
        {
            _agent.isStopped = false;
        }

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
            SetTargetSpeed(_currentLegTargetSpeed * turnMultiplier);

            if (remainingDistance <= _agent.stoppingDistance)
            {
                _isMoving = false;
                _hasArrivedThisLeg = true;
                OnArrivedAtDestination?.Invoke();
            }
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
                _animator.SetFloat(_speedParameterName, normalizedSpeed);
            }

            if (!string.IsNullOrEmpty(_stateSpeedMultiplierParameterName))
            {
                float multiplier = ComputePlaybackRateMultiplier(normalizedSpeed);
                _animator.SetFloat(_stateSpeedMultiplierParameterName, multiplier);
            }
        }

        /// <summary>
        /// Generalized to any number of poses (not hardcoded to exactly Idle/Walk/Run) — finds
        /// the two entries in _posePlaybackRates (sorted ascending by Threshold, as maintained
        /// by the editor's sync) that bracket normalizedSpeed, and Lerps between their
        /// Multiplier values based on relative position between those two thresholds.
        /// </summary>
        private float ComputePlaybackRateMultiplier(float normalizedSpeed)
        {
            if (_posePlaybackRates == null || _posePlaybackRates.Count == 0) return 1f;
            if (_posePlaybackRates.Count == 1) return _posePlaybackRates[0].Multiplier;

            if (normalizedSpeed <= _posePlaybackRates[0].Threshold) return _posePlaybackRates[0].Multiplier;
            int lastIndex = _posePlaybackRates.Count - 1;
            if (normalizedSpeed >= _posePlaybackRates[lastIndex].Threshold) return _posePlaybackRates[lastIndex].Multiplier;

            for (int i = 0; i < lastIndex; i++)
            {
                float thresholdA = _posePlaybackRates[i].Threshold;
                float thresholdB = _posePlaybackRates[i + 1].Threshold;

                if (normalizedSpeed >= thresholdA && normalizedSpeed <= thresholdB)
                {
                    float span = thresholdB - thresholdA;
                    float t = span > 0.0001f ? (normalizedSpeed - thresholdA) / span : 0f;
                    return Mathf.Lerp(_posePlaybackRates[i].Multiplier, _posePlaybackRates[i + 1].Multiplier, t);
                }
            }

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