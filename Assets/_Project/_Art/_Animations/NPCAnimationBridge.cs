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
    // names, per feedback that clip-based lookup was a bigger architectural risk without
    // actually removing the failure mode. Instead, the string field now uses
    // [AnimatorStateName], which draws it as a dropdown of real states pulled straight from the
    // assigned Animator's controller — typos and mismatches are no longer possible at all.
    // v8: Added a public Animator getter so sibling components (e.g. NPCWitnessReaction,
    // LocomotionAgent) reuse this bridge's already-resolved Animator reference instead of
    // re-resolving independently.
    // v9: Added a configurable Animator layer index for reactive animations (tone/witness
    // reactions, idle reverts) — default 0 (Base Layer), supporting a separate Reactions layer
    // for Locomotion-equipped NPCs.
    // v10: Added a Default Startup State single dropdown selection. REMOVED in v12 (see below).
    // v11 FIX: Non-Locomotion NPCs with no startup state configured silently inherited the
    // shared Animator Controller's own m_DefaultState — on the standard NPC_GossipAnimator
    // controller, that's the "Locomotion" Blend Tree, which sits frozen at Speed = 0 without a
    // LocomotionAgent driving it. Fixed by explicitly Play()-ing a state on spawn instead of
    // relying on the Controller's own default.
    // v12: Default Startup State field REMOVED entirely — a single fixed startup pose isn't
    // wanted. A non-Locomotion NPC now ALWAYS explicitly Play()s a RANDOM entry from Default
    // Idle States on spawn — the same pool already used for idle-reverts, now doing double duty
    // as the startup pool too. Only one list to maintain per NPC instead of two.
    // v13 FIX: The v12 fix still froze in practice — Play() was being called from Awake(),
    // before the Animator component finishes its own internal initialization. That
    // initialization runs afterward regardless, silently re-entering the Controller's own
    // default state ("Locomotion") and overwriting our explicit Play() call — the real cause
    // of the freeze. Moved the startup call from Awake() to Start(), where Animator init is
    // guaranteed complete, and added a forced Animator.Update(0f) immediately after Play() so
    // the pose is applied within the same frame instead of waiting for the next Update() tick.
    //
    // v14 FIX: v13 had no effect because it wasn't the actual root cause. The real culprit is
    // Unity's default Animator Culling Mode (CullUpdateTransforms) — it keeps the state machine
    // and parameters evaluating normally (so interaction/dialogue/scripts all keep working
    // fine), but stops applying the results to bone transforms whenever the renderer's bounds
    // are considered off-screen, which is easy to false-trigger if bounds weren't recomputed
    // for the current pose. This is EXACTLY "frozen but interactable." NPCCreatorWizardWindow
    // already sets cullingMode = AlwaysAnimate, but only at generation time — it never helps an
    // NPC created before that fix, or any NPC that bypasses the wizard entirely. Now enforced
    // here instead, unconditionally, at runtime, on every NPC regardless of origin.
    //
    // v15 FIX (actual root cause): v14 also had no effect, because culling wasn't it either.
    // NPC_GossipAnimator has TWO layers — Base Layer (Running/Walking/Locomotion) and Reactions
    // (every Idle_* state, plus Empty). EVERY Idle_* state lives on the Reactions layer (index
    // 1) — none exist on Base Layer (index 0). _animationLayerIndex defaults to 0. Every
    // Play()/CrossFade() call in this script was therefore targeting a layer that doesn't
    // contain the requested state at all — Unity silently can't find it, so Base Layer just
    // stays on ITS OWN default, the Locomotion Blend Tree, frozen at Speed = 0. That's the
    // entire bug. Fixed by resolving the correct layer per-state via ResolveLayerForState()
    // instead of trusting a single configured layer index everywhere — applied consistently to
    // the startup call AND to SetAnimationState()/CrossFadeToRandomIdle(), which had the exact
    // same latent bug for idle-reverts and tone reactions.
    //
    // v16: Locomotion ↔ Interaction integration. Added PlayIdleForInteraction() and
    // ReleaseReactionOverride() as public entry points for NPCProximityGossip to call on a
    // Locomotion-equipped NPC at interaction start/end respectively — the former drops this NPC
    // into its idle pool (masking Base Layer's Locomotion animation via the Reactions layer's
    // Override blend), the latter releases that mask afterward via _reactionsPassThroughState.
    //
    // v17 FIX: "sliding through the floor" bug. v15's layer fix had a side effect nobody
    // noticed until now — it made this script's PRE-EXISTING ambient reaction system (tone
    // animations from PresentRumor, witness reactions from NPCWitnessReaction, both of which
    // predate Locomotion entirely) actually reach the Reactions layer for the first time. Any
    // Locomotion NPC that plays ANY ambient reaction — completely independent of the v16
    // interaction flow, e.g. an Auto-Proximity rumor firing while it's still walking — gets its
    // Reactions layer masked exactly like during an interaction, but NOTHING released that mask
    // afterward, because only the v16 interaction path called ReleaseReactionOverride(). The
    // NavMeshAgent was never paused for this ambient case either, so the NPC kept physically
    // moving while its VISUAL animation sat frozen on a static idle pose — reads as "sliding
    // through the floor." Fixed with a single decision point, RevertToRestingState(), used by
    // every revert path (the timer AND ForceRevertToIdle()): a Locomotion NPC that is NOT
    // currently under an active v16 interaction now releases the mask AND resumes movement when
    // any ambient reaction ends, instead of just picking another idle pose. SetAnimationState()
    // also now pauses movement for the duration of an ambient reaction, so the visual and the
    // physical position stay in sync for its whole duration, not just at the end. This required
    // giving this script its OWN optional INpcMovementController reference (resolved exactly
    // like NPCProximityGossip's — plain GetComponent&lt;T&gt;(), no reflection) — replacing the old
    // reflection-based HasLocomotionAgent() check entirely, now redundant with that reference.
    public class NPCAnimationBridge : MonoBehaviour
    {
        [Header("Animation Settings")]
        [Tooltip("The duration to hold a transient animation (e.g., Whisper) before returning to Idle.")]
        [SerializeField] private float _defaultRevertDelay = 3.0f;
        [Tooltip("The transition duration for CrossFade.")]
        [SerializeField] private float _crossFadeDuration = 0.2f;

        // v15: This field is now only a FALLBACK, used solely if ResolveLayerForState() can't
        // find the target state on any layer. Normal playback auto-detects the correct layer.
        [Tooltip("FALLBACK ONLY (v15+): the correct Animator layer for any given state is now auto-detected via ResolveLayerForState(). This value is only used if a target state can't be found on any layer at all — safe to leave at its default.")]
        [SerializeField] private int _animationLayerIndex = 0;

        [Header("Idle / Default Animation Pool")]
        // v12: Tooltip updated — this list is now ALSO this NPC's startup animation pool, not
        // just the idle-revert pool. Default Startup State (the old separate single-dropdown
        // field) has been removed; there is only one pool to configure now.
        [Tooltip("Pool of Animator state names this NPC can revert to when returning to a resting state. One is chosen at random each time a revert happens — AND one is chosen at random for this NPC's startup animation when it spawns (if it has no LocomotionAgent). Populated as a dropdown from the Animator assigned below.")]
        [AnimatorStateName(nameof(_animator))]
        [SerializeField] private List<string> _defaultIdleStates = new List<string> { "Idle_Neutral" };

        [Tooltip("The Animator driving this NPC's animation states. Auto-resolved from this GameObject or its children if left empty. Also used to populate the Default Idle States dropdown above.")]
        [SerializeField] private Animator _animator;

        [Header("Locomotion Add-on Compatibility")]
        // v16: New — only relevant if this NPC ALSO has a LocomotionAgent (Locomotion add-on).
        // While this NPC is mid-interaction, the Reactions layer is put into a real idle state
        // (see PlayIdleForInteraction()), which — being a full-weight Override layer — visually
        // masks Base Layer's Locomotion Blend Tree underneath. ReleaseReactionOverride() below
        // CrossFades this layer back to a state with no motion assigned, so Base Layer's
        // Locomotion animation becomes visible again once the interaction ends.
        [Tooltip("Name of a state (typically on the Reactions layer) with NO Motion assigned, used to release this layer's override once an interaction ends, letting a LocomotionAgent's Locomotion Blend Tree animation show through again. On the standard NPC_GossipAnimator controller, this is the Reactions layer's own default state, \"Empty\". Irrelevant — and safely ignored — for any NPC without a LocomotionAgent.")]
        [AnimatorStateName(nameof(_animator))]
        [SerializeField] private string _reactionsPassThroughState = "Empty";

        [Tooltip("v19: How fast (seconds) THIS SPECIFIC transition — releasing a reaction/waiting animation back to normal movement — blends, independent of Cross Fade Duration above (which is shared by every OTHER blend: entering a reaction, idle reverts, etc.). Lower = snappier return to movement, useful when a reaction (e.g. a flocking NPC's scared/waiting pose) needs to visually clear quickly once the NPC starts moving again, without speeding up every other transition too.")]
        [SerializeField] private float _reactionReleaseCrossFadeDuration = 0.1f;

        /// <summary>
        /// Read-only access to this bridge's resolved Animator — lets sibling components
        /// (e.g. NPCWitnessReaction, LocomotionAgent) reuse the exact same reference instead of
        /// re-resolving independently.
        /// </summary>
        public Animator Animator => _animator;

        private ReputationService _reputation;
        // Cached Animator.StringToHash() results for every entry in _defaultIdleStates, kept in
        // sync by RebuildIdleHashes() — avoids re-hashing strings every time a revert happens,
        // and is also what PlayDefaultStartupAnimation() picks from on spawn.
        private readonly List<int> _defaultIdleStateHashes = new List<int>();
        private Coroutine _revertCoroutine;

        // v17: OPTIONAL — null on any NPC without a component implementing
        // INpcMovementController (no Locomotion add-on, or Locomotion not attached to this
        // specific NPC). Replaces the old reflection-based HasLocomotionAgent() check entirely.
        private INpcMovementController _movementController;

        // v17: True only while NPCProximityGossip currently owns a FULL, externally-controlled
        // interaction pause (set by PlayIdleForInteraction(), cleared by
        // ReleaseReactionOverride()). Gates RevertToRestingState()'s decision, and stops
        // SetAnimationState() from redundantly pausing movement that's already paused.
        private bool _isPausedForInteraction;

        private void Awake()
        {
            // v18: Extracted into a shared helper — see ResolveAnimatorReference() below. Reused
            // by Reset() too, so runtime and Editor-time resolution can never drift apart.
            ResolveAnimatorReference();

            // No Animator anywhere on/under this NPC — every animation call below is a no-op,
            // so warn loudly now rather than failing silently later.
            if (_animator == null)
            {
                Debug.LogWarning($"<color=orange>[NPCAnimationBridge]</color> No Animator found on '{gameObject.name}' or its children. Animation calls will be ignored.", this);
            }
            else
            {
                // v14: Force AlwaysAnimate unconditionally, at runtime, on every NPC — the
                // wizard's own generation-time fix (v31) doesn't cover NPCs created before that
                // fix existed, or any NPC that skips the wizard entirely. Without this, Unity's
                // default CullUpdateTransforms mode can silently freeze the visible pose while
                // every other system (scripts, interaction, the state machine itself) keeps
                // running normally underneath — this line is the actual fix for that.
                _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }

            // Build the hashed idle-state lookup used by CrossFadeToRandomIdle() and (as of v11)
            // by the startup fallback below — must run before PlayDefaultStartupAnimation().
            RebuildIdleHashes();

            if (_defaultIdleStateHashes.Count == 0)
            {
                Debug.LogWarning($"<color=orange>[NPCAnimationBridge]</color> '{gameObject.name}' has no entries in Default Idle States. Reverting to idle — and this NPC's startup animation, if it has no LocomotionAgent — will not work.", this);
            }

            // v17: Plain interface lookup, replacing the old reflection-based
            // HasLocomotionAgent() check — no reflection needed, since INpcMovementController is
            // defined in Core. Resolves LocomotionAgent automatically if present.
            _movementController = GetComponent<INpcMovementController>();

            // v13: The actual startup Play() call has moved to Start() — see the class-level
            // v13 comment above for why calling it here, in Awake(), was the root cause of the
            // freeze. Awake() is still responsible for resolving _animator and building the
            // idle hash list, both of which Start() below depends on.
        }

        /// <summary>
        /// v18: Fires automatically the instant this component is added via Add Component, and
        /// is also available as a one-click "Reset" from the component's context menu on any
        /// GameObject that already has it — Unity's standard convention for auto-populating
        /// sensible defaults. Solves having to manually drag the Animator in every time.
        /// </summary>
        private void Reset()
        {
            ResolveAnimatorReference();
        }

        /// <summary>
        /// v18: Extracted from Awake() into its own method so Reset() (Editor-time, on Add
        /// Component or manual Reset) and Awake() (runtime) share one resolution order that can
        /// never drift out of sync between the two. Checks this GameObject itself first, then
        /// falls back to searching its children — the standard "rig might be on a child mesh
        /// object" case.
        /// </summary>
        private void ResolveAnimatorReference()
        {
            if (_animator != null) return;

            _animator = GetComponent<Animator>();
            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>();
            }
        }

        /// <summary>
        /// v13: Moved here from Awake(). By the time Start() runs, every component's own
        /// initialization (including the Animator's internal state machine setup) is guaranteed
        /// complete, so an explicit Play() call here actually sticks instead of being silently
        /// overwritten by the Animator re-entering the Controller's own default state.
        /// </summary>
        private void Start()
        {
            // v10/v11/v12/v13/v17: Only this bridge is responsible for entering a startup state
            // if this NPC has NO movement controller. If it DOES have one, that add-on is
            // responsible for its own starting animation state instead.
            if (_animator != null && _movementController == null)
            {
                PlayDefaultStartupAnimation();
            }
        }

        /// <summary>
        /// v12: This NPC's spawn animation is ALWAYS a random pick from Default Idle States —
        /// there is no longer a separate single fixed startup state to prefer first. Only
        /// reachable for non-Locomotion NPCs (see the HasLocomotionAgent() check in Start()).
        /// </summary>
        private void PlayDefaultStartupAnimation()
        {
            // Nothing configured to pick from — warn instead of leaving this as a silent freeze
            // on the shared Animator Controller's own (possibly unsafe) default state.
            if (_defaultIdleStateHashes.Count == 0)
            {
                Debug.LogWarning($"<color=orange>[NPCAnimationBridge]</color> '{gameObject.name}' has no Default Idle States configured — it will remain on the Animator Controller's own default state. On the standard NPC_GossipAnimator controller, that is the 'Locomotion' Blend Tree, which will appear frozen without a LocomotionAgent driving its Speed parameter. Add at least one entry to Default Idle States to fix this.", this);
                return;
            }

            // Pick one entry at random out of the (already-hashed) idle pool.
            int randomIdleHash = _defaultIdleStateHashes[UnityEngine.Random.Range(0, _defaultIdleStateHashes.Count)];
            // v15: Resolve which layer ACTUALLY contains this state instead of assuming
            // _animationLayerIndex — on NPC_GossipAnimator, Idle_* states live on the
            // "Reactions" layer (index 1), not Base Layer (index 0, the configured default).
            int resolvedLayer = ResolveLayerForState(randomIdleHash);
            // Play() snaps straight into the state at normalizedTime 0 — appropriate for a
            // spawn-time pose, as opposed to CrossFade() which blends from a previous state.
            _animator.Play(randomIdleHash, resolvedLayer, 0f);
            // v13: Force the state machine to evaluate RIGHT NOW instead of waiting for the
            // next Update() tick — without this, there can be a one-frame gap where the
            // Animator is still sitting on whatever it initialized into.
            _animator.Update(0f);
            Debug.Log($"<color=cyan>[NPCAnimationBridge]</color> '{gameObject.name}' started in a random Default Idle State (layer {resolvedLayer}).");
        }

        /// <summary>
        /// v15: Resolves which Animator layer actually contains the given state hash, instead
        /// of blindly trusting the configured _animationLayerIndex. This is the fix for the
        /// root cause of the startup freeze — every Idle_* state on NPC_GossipAnimator lives on
        /// the "Reactions" layer (index 1), while _animationLayerIndex defaults to 0.
        /// </summary>
        private int ResolveLayerForState(int stateHash)
        {
            for (int layer = 0; layer < _animator.layerCount; layer++)
            {
                if (_animator.HasState(layer, stateHash))
                {
                    return layer;
                }
            }

            // Not found on any layer — fall back to the configured default rather than throwing.
            Debug.LogWarning($"<color=orange>[NPCAnimationBridge]</color> '{gameObject.name}' could not find state hash {stateHash} on any Animator layer. Falling back to configured Animation Layer Index ({_animationLayerIndex}).", this);
            return _animationLayerIndex;
        }

        // v17: HasLocomotionAgent() (reflection-based) was removed here — no longer needed now
        // that this script has its own resolved INpcMovementController reference (see Awake()).
        // Every call site that used to check HasLocomotionAgent() now checks
        // _movementController == null / != null instead — same result, no reflection required.

        private void OnValidate()
        {
            // Keep the hashed idle list in sync whenever _defaultIdleStates is edited in the Inspector.
            RebuildIdleHashes();
        }

        private void RebuildIdleHashes()
        {
            _defaultIdleStateHashes.Clear();
            if (_defaultIdleStates == null) return;

            // Hash every non-empty state name once, up front, so runtime code never re-hashes strings.
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

            // v15: Resolve the correct layer per-state instead of assuming _animationLayerIndex —
            // same fix as the startup path, applied here since tone/reaction states have the
            // identical latent bug (e.g. reaction states living on the Reactions layer).
            int resolvedLayer = ResolveLayerForState(stateHash);

            // v17: If this is an AMBIENT reaction (i.e. NOT already inside an active v16
            // interaction, which owns pause/resume itself) on a Locomotion-equipped NPC, pause
            // movement for the reaction's duration — otherwise the NavMeshAgent keeps advancing
            // this NPC's actual position while the Reactions layer masks the walk/run animation
            // visually, reading as "sliding through the floor." Skipped entirely if already
            // mid-interaction, to avoid fighting with NPCProximityGossip's own pause/resume.
            if (!_isPausedForInteraction)
            {
                _movementController?.PauseForInteraction();
            }

            // CrossFade blends smoothly into the target state over _crossFadeDuration seconds.
            _animator.CrossFade(stateHash, _crossFadeDuration, resolvedLayer);
            Debug.Log($"<color=cyan>[Animation]</color> State changed to: {stateHash} (layer {resolvedLayer})");

            // Don't schedule an auto-revert if we just crossfaded INTO an idle state already.
            bool isAlreadyAnIdleState = _defaultIdleStateHashes.Contains(stateHash);
            if (useTimer && !isAlreadyAnIdleState)
            {
                if (_revertCoroutine != null) StopCoroutine(_revertCoroutine);
                float delay = revertDelayOverride ?? _defaultRevertDelay;
                _revertCoroutine = StartCoroutine(RevertToIdleAfterDelay(delay));
            }
        }

        /// <summary>
        /// Immediately cancels any pending revert timer and returns this NPC to its resting
        /// state (see RevertToRestingState()). Intended for cases like the player walking out
        /// of an NPC's proximity zone, where waiting out the normal timed revert isn't
        /// appropriate.
        /// </summary>
        public void ForceRevertToIdle()
        {
            if (_animator == null) return;

            if (_revertCoroutine != null)
            {
                StopCoroutine(_revertCoroutine);
                _revertCoroutine = null;
            }

            RevertToRestingState();
            Debug.Log("<color=yellow>[Animation]</color> Force-reverted to resting state.");
        }

        private IEnumerator RevertToIdleAfterDelay(float delay)
        {
            // Wait out the configured delay (or the tone's own LoopDuration, if passed in) before reverting.
            yield return new WaitForSeconds(delay);

            RevertToRestingState();
            Debug.Log("<color=yellow>[Animation]</color> Auto-reverted to resting state.");
        }

        /// <summary>
        /// v17: Single decision point for what "returning to rest" means for THIS NPC, used by
        /// every revert path (the auto-revert timer AND ForceRevertToIdle()):
        /// - A non-Locomotion NPC, OR a Locomotion NPC currently under an ACTIVE v16 interaction
        ///   (_isPausedForInteraction), rests within its idle pool — same as always.
        /// - A Locomotion NPC that is NOT currently interacting instead releases the Reactions
        ///   layer override entirely AND resumes movement, letting its Locomotion Blend Tree
        ///   drive again — this is the actual v17 fix. Without this branch, an ambient reaction
        ///   (Auto-Proximity rumor, witnessed-deed reaction — anything outside the v16
        ///   interaction flow) would leave the NPC stuck on a static idle pose forever while its
        ///   NavMeshAgent kept moving underneath.
        /// </summary>
        private void RevertToRestingState()
        {
            if (!_isPausedForInteraction && _movementController != null)
            {
                ReleaseReactionOverride();
                _movementController.ResumeAfterInteraction();
            }
            else
            {
                CrossFadeToRandomIdle();
            }
        }

        /// <summary>
        /// v16: Public entry point for core interaction code (NPCProximityGossip) to use when a
        /// Locomotion-equipped NPC begins an interaction — drops this NPC into a random state
        /// from its idle pool, exactly like a non-Locomotion NPC already displays. Since that
        /// pool lives on the Reactions layer (a full-weight Override layer on the standard
        /// NPC_GossipAnimator controller), this visually overrides Base Layer's Locomotion Blend
        /// Tree regardless of whatever residual velocity the (now-paused) NavMeshAgent still has
        /// — the NPC reads as fully stopped and idle immediately, not just once velocity decays.
        /// Reuses the exact same idle-pool logic as CrossFadeToRandomIdle() — no duplicated pick
        /// logic, just a public, purpose-named entry point for this specific caller.
        /// v17: Also sets _isPausedForInteraction — this is what tells RevertToRestingState()
        /// (and SetAnimationState()'s ambient-pause check) that movement is ALREADY paused
        /// externally by NPCProximityGossip, for the whole interaction's duration, so ambient
        /// reaction timers firing mid-conversation don't prematurely resume movement.
        /// </summary>
        public void PlayIdleForInteraction()
        {
            _isPausedForInteraction = true;
            CrossFadeToRandomIdle();
        }

        /// <summary>
        /// v16: Public entry point for core interaction code to call once an interaction ends
        /// on a Locomotion-equipped NPC — CrossFades the Reactions layer to
        /// _reactionsPassThroughState (a state with no Motion assigned), releasing its override
        /// so Base Layer's Locomotion Blend Tree becomes visible again. Safe to call on any NPC:
        /// a non-Locomotion NPC simply CrossFades to an empty state briefly before its next
        /// normal idle-revert or reaction plays over it — harmless, and this method is never
        /// actually called for non-Locomotion NPCs by NPCProximityGossip in the first place.
        /// v17: Also clears _isPausedForInteraction. Deliberately does NOT touch movement itself
        /// — NPCProximityGossip's ResumeAfterInteraction() call owns that for the v16 flow;
        /// RevertToRestingState() calls both this method AND ResumeAfterInteraction() together
        /// for the ambient-reaction flow, where THIS script owns both halves instead.
        /// </summary>
        public void ReleaseReactionOverride()
        {
            _isPausedForInteraction = false;

            if (_animator == null || string.IsNullOrEmpty(_reactionsPassThroughState)) return;

            int passThroughHash = Animator.StringToHash(_reactionsPassThroughState);
            int resolvedLayer = ResolveLayerForState(passThroughHash);
            // v19: Uses the dedicated _reactionReleaseCrossFadeDuration instead of the shared
            // _crossFadeDuration — lets this specific transition be tuned independently (e.g.
            // faster, so a flocking NPC's waiting pose doesn't linger visually after it's
            // already moving again) without affecting every other blend in this script.
            _animator.CrossFade(passThroughHash, _reactionReleaseCrossFadeDuration, resolvedLayer);
            Debug.Log($"<color=cyan>[NPCAnimationBridge]</color> '{gameObject.name}' released the Reactions layer override — Locomotion animation should be visible again.");
        }

        private void CrossFadeToRandomIdle()
        {
            // Nothing to revert to if the idle pool is empty — guard against an out-of-range pick.
            if (_defaultIdleStateHashes.Count == 0) return;

            int randomIdleHash = _defaultIdleStateHashes[UnityEngine.Random.Range(0, _defaultIdleStateHashes.Count)];
            // v15: Same layer-resolution fix as the startup path and SetAnimationState().
            int resolvedLayer = ResolveLayerForState(randomIdleHash);
            _animator.CrossFade(randomIdleHash, _crossFadeDuration, resolvedLayer);
        }
    }
}