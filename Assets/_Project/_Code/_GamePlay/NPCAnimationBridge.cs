using System.Collections.Generic;
using UnityEngine;
using VContainer;
using TownsPeople.Services;
using TownsPeople.Data;
using System.Collections;

namespace TownsPeople.GamePlay
{
    /// <summary>
    /// NPCAnimationBridge: Manages animation states with Inspector-editable timing.
    /// Designed for modularity and user-friendly configuration.
    /// </summary>
    // v7: _defaultIdleAnimations (List<AnimationClip>) reverted back to a List<string> of state
    // names, per feedback that clip-based lookup was a bigger architectural risk (conflicts
    // with a future Locomotion add-on) without actually removing the failure mode (clip name
    // still had to match state name). Instead, the string field now uses [AnimatorStateName],
    // which draws it as a dropdown of real states pulled straight from the assigned Animator's
    // controller — typos and mismatches are no longer possible at all.
    // v8: Added a public Animator getter so sibling components (e.g. NPCWitnessReaction) can
    // reuse this bridge's already-resolved Animator reference instead of independently
    // re-resolving.
    // v9: Added a configurable Animator layer index for reactive animations (tone/witness
    // reactions, idle reverts) — default 0 (Base Layer), zero behavior change unless
    // deliberately opted into. Exists to support the Locomotion add-on's continuous Speed-
    // driven Blend Tree living on the Base Layer, with reactive animations moved to a separate
    // upper layer so the two systems don't fight for control of the same layer.
    public class NPCAnimationBridge : MonoBehaviour
    {
        [Header("Animation Settings")]
        [Tooltip("The duration to hold a transient animation (e.g., Whisper) before returning to Idle.")]
        [SerializeField] private float _defaultRevertDelay = 3.0f;
        [Tooltip("The transition duration for CrossFade.")]
        [SerializeField] private float _crossFadeDuration = 0.2f;

        [Tooltip("Which Animator layer reactive animations (tone/witness reactions, idle reverts) play on. Leave at 0 (Base Layer) unless you've split locomotion (a continuous Speed-driven Blend Tree, see LocomotionAgent) onto the Base Layer and moved reactive animations to a dedicated upper layer instead — in that setup, this should point at that layer's index so the two systems don't fight for control of the same layer.")]
        [SerializeField] private int _animationLayerIndex = 0;

        [Header("Idle / Default Animation Pool")]
        [Tooltip("Pool of Animator state names this NPC can revert to when returning to a resting state. One is chosen at random each time a revert happens. Populated as a dropdown from the Animator assigned below.")]
        [AnimatorStateName(nameof(_animator))]
        [SerializeField] private List<string> _defaultIdleStates = new List<string> { "Idle_Neutral" };

        [Tooltip("The Animator driving this NPC's animation states. Auto-resolved from this GameObject or its children if left empty. Also used to populate the Default Idle States dropdown above.")]
        [SerializeField] private Animator _animator;

        /// <summary>
        /// v8: Read-only access to this bridge's resolved Animator — lets sibling components
        /// (e.g. NPCWitnessReaction, LocomotionAgent) reuse the exact same reference instead of
        /// re-resolving independently.
        /// </summary>
        public Animator Animator => _animator;

        private ReputationService _reputation;
        private readonly List<int> _defaultIdleStateHashes = new List<int>();
        private Coroutine _revertCoroutine;

        private void Awake()
        {
            if (_animator == null)
            {
                _animator = GetComponent<Animator>();
                if (_animator == null)
                {
                    _animator = GetComponentInChildren<Animator>();
                }
            }

            if (_animator == null)
            {
                Debug.LogWarning($"<color=orange>[NPCAnimationBridge]</color> No Animator found on '{gameObject.name}' or its children. Animation calls will be ignored.", this);
            }

            RebuildIdleHashes();

            if (_defaultIdleStateHashes.Count == 0)
            {
                Debug.LogWarning($"<color=orange>[NPCAnimationBridge]</color> '{gameObject.name}' has no entries in Default Idle States. Reverting to idle will not work.", this);
            }
        }

        private void OnValidate()
        {
            RebuildIdleHashes();
        }

        private void RebuildIdleHashes()
        {
            _defaultIdleStateHashes.Clear();
            if (_defaultIdleStates == null) return;

            foreach (string stateName in _defaultIdleStates)
            {
                if (!string.IsNullOrWhiteSpace(stateName))
                {
                    _defaultIdleStateHashes.Add(Animator.StringToHash(stateName));
                }
            }
        }

        [Inject]
        public void Construct(ReputationService reputation)
        {
            _reputation = reputation;
            Debug.Log("<color=green>[Injection]</color> NPCAnimationBridge: ReputationService resolved.");
        }

        /// <summary>
        /// Plays the correct animation for a given GossipToneData, respecting its PlaybackMode:
        /// - None: does nothing (no animation change at all).
        /// - PlayOnce: plays once, then auto-reverts to a random idle state after the default revert delay.
        /// - Loop: plays and holds for the tone's own LoopDuration, then auto-reverts to a random idle state.
        /// </summary>
        public void PlayToneAnimation(GossipToneData tone)
        {
            if (tone == null) return;
            if (tone.Mode == PlaybackMode.None) return; // No animation for this tone by design.

            string stateName = tone.GetRandomAnimatorStateName();
            if (string.IsNullOrEmpty(stateName)) return;

            int stateHash = Animator.StringToHash(stateName);
            float revertDelay = tone.Mode == PlaybackMode.Loop ? tone.LoopDuration : _defaultRevertDelay;

            SetAnimationState(stateHash, useTimer: true, revertDelayOverride: revertDelay);
        }

        /// <summary>
        /// Public API to set state. Uses _defaultRevertDelay if no specific duration is passed.
        /// </summary>
        public void SetAnimationState(int stateHash, bool useTimer = true, float? revertDelayOverride = null)
        {
            if (_animator == null) return;

            _animator.CrossFade(stateHash, _crossFadeDuration, _animationLayerIndex);
            Debug.Log($"<color=cyan>[Animation]</color> State changed to: {stateHash}");

            bool isAlreadyAnIdleState = _defaultIdleStateHashes.Contains(stateHash);
            if (useTimer && !isAlreadyAnIdleState)
            {
                if (_revertCoroutine != null) StopCoroutine(_revertCoroutine);
                float delay = revertDelayOverride ?? _defaultRevertDelay;
                _revertCoroutine = StartCoroutine(RevertToIdleAfterDelay(delay));
            }
        }

        /// <summary>
        /// Immediately cancels any pending revert timer and cross-fades straight to a random
        /// idle state. Intended for cases like the player walking out of an NPC's proximity
        /// zone, where waiting out the normal timed revert isn't appropriate.
        /// </summary>
        public void ForceRevertToIdle()
        {
            if (_animator == null) return;

            if (_revertCoroutine != null)
            {
                StopCoroutine(_revertCoroutine);
                _revertCoroutine = null;
            }

            CrossFadeToRandomIdle();
            Debug.Log("<color=yellow>[Animation]</color> Force-reverted to Idle state.");
        }

        private IEnumerator RevertToIdleAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);

            CrossFadeToRandomIdle();
            Debug.Log("<color=yellow>[Animation]</color> Auto-reverted to Idle state.");
        }

        private void CrossFadeToRandomIdle()
        {
            if (_defaultIdleStateHashes.Count == 0) return;

            int randomIdleHash = _defaultIdleStateHashes[Random.Range(0, _defaultIdleStateHashes.Count)];
            _animator.CrossFade(randomIdleHash, _crossFadeDuration, _animationLayerIndex);
        }
    }
}