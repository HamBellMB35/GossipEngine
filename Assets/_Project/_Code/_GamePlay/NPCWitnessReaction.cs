using System.Collections.Generic;
using UnityEngine;
using TownsPeople.Data;
#if UNITY_EDITOR
using System.Linq;
using UnityEditor.Animations;
#endif

namespace TownsPeople.GamePlay
{
    /// <summary>
    /// OPTIONAL per-NPC override for how this specific NPC reacts when it witnesses a player
    /// deed (see PlayerDeedBroadcaster). Not required by any other system — safe to add or
    /// remove from an NPC freely, same as NPCRumorIndicator.
    ///
    /// If this component is absent, or its Mode is left at PresentRumor, behavior is
    /// unchanged: the NPC presents the rumor normally (speech bubble/audio/animation via its
    /// existing rumor-presentation pipeline). Setting Mode to PlayAnimation suppresses that
    /// presentation and instead plays one of this NPC's own configured reaction animations —
    /// routed through NPCAnimationBridge.SetAnimationState(), so it gets the exact same
    /// CrossFade + auto-revert-to-idle timing as every other animation this NPC plays, nothing
    /// duplicated — plus an optional accompanying sound.
    ///
    /// Learning the rumor and the personal reputation-opinion adjustment always happen
    /// regardless of this setting — those are game state, not presentation, and are applied by
    /// PlayerDeedBroadcaster before it even checks this component.
    /// </summary>
    // v2: Auto-resolves _animator from the sibling NPCAnimationBridge (Reset()/OnValidate(),
    // editor-only) and auto-populates Reaction Animation States from that Animator's Controller
    // the first time both are resolved and the list is empty.
    // v3: CollectAllStateNames() changed from private to public static, so
    // NPCCreatorWizardWindow can call it directly when auto-adding this component at NPC
    // generation time (Reset() doesn't fire from a scripted AddComponent call, so the wizard
    // replicates the same population logic itself rather than duplicating it separately).
    [RequireComponent(typeof(NPCAnimationBridge))]
    public class NPCWitnessReaction : MonoBehaviour
    {
        public enum ReactionMode
        {
            PresentRumor,
            PlayAnimation
        }

        [Tooltip("Present Rumor: this NPC reacts to a witnessed deed normally (speech bubble/audio/animation) — identical to not having this component at all. Play Animation: that presentation is suppressed, and one of this NPC's own Reaction Animation States plays instead, with an optional Reaction Audio Clip.")]
        [SerializeField] private ReactionMode _mode = ReactionMode.PresentRumor;

        [Tooltip("The Animator driving this NPC's animation states. Auto-resolved from this GameObject's NPCAnimationBridge (or its children) the moment this component is added. Used to populate the Reaction Animation States dropdown below — kept in sync with the same Animator as this NPC's NPCAnimationBridge.")]
        [SerializeField] private Animator _animator;

        [Tooltip("Pool of Animator state names this NPC can play when it witnesses a deed in Play Animation mode. One is chosen at random each time. Auto-populated with every state from the Animator above the first time both are resolved and this list is empty — freely add/remove afterward.")]
        [AnimatorStateName(nameof(_animator))]
        [SerializeField] private List<string> _reactionAnimationStates = new List<string>();

        [Tooltip("Optional pool of sounds to accompany the reaction animation. One is chosen at random and played alongside it. Leave empty for a silent reaction.")]
        [SerializeField] private List<AudioClip> _reactionAudioClips = new List<AudioClip>();

        [Tooltip("Where Reaction Audio Clips are played from. Auto-resolved from this GameObject if left empty.")]
        [SerializeField] private AudioSource _audioSource;

        private NPCAnimationBridge _animationBridge;

        public ReactionMode Mode => _mode;

        private void Awake()
        {
            ResolveRuntimeReferences();
        }

        private void ResolveRuntimeReferences()
        {
            if (_animator == null)
            {
                _animator = GetComponent<Animator>();
                if (_animator == null)
                {
                    _animator = GetComponentInChildren<Animator>();
                }
            }

            if (_audioSource == null)
            {
                _audioSource = GetComponent<AudioSource>();
            }

            _animationBridge = GetComponent<NPCAnimationBridge>();
        }

#if UNITY_EDITOR
        /// <summary>
        /// Fires the instant this component is added via Add Component (or via the Inspector's
        /// right-click "Reset"). Editor-only — never runs in a build, matching the
        /// UNITY_EDITOR-gated pattern already used elsewhere in this project (e.g.
        /// PlayerDeedBroadcaster.OnDrawGizmosSelected).
        /// </summary>
        private void Reset()
        {
            AutoResolveAnimator();
            AutoPopulateReactionStatesIfEmpty();
        }

        /// <summary>
        /// Also re-checks on every Inspector change, in case NPCAnimationBridge's own Animator
        /// gets assigned/changed AFTER this component was first added (Reset() only fires once).
        /// </summary>
        private void OnValidate()
        {
            AutoResolveAnimator();
            AutoPopulateReactionStatesIfEmpty();
        }

        private void AutoResolveAnimator()
        {
            if (_animator != null) return;

            NPCAnimationBridge bridge = GetComponent<NPCAnimationBridge>();
            if (bridge != null && bridge.Animator != null)
            {
                _animator = bridge.Animator;
                return;
            }

            _animator = GetComponent<Animator>();
            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>();
            }
        }

        private void AutoPopulateReactionStatesIfEmpty()
        {
            if (_animator == null) return;
            if (_reactionAnimationStates != null && _reactionAnimationStates.Count > 0) return;

            AnimatorController controller = _animator.runtimeAnimatorController as AnimatorController;
            if (controller == null) return;

            _reactionAnimationStates = CollectAllStateNames(controller);
        }

        /// <summary>
        /// v3: Public so NPCCreatorWizardWindow can reuse this exact logic when auto-adding
        /// this component at NPC generation time, instead of duplicating it separately there.
        /// </summary>
        public static List<string> CollectAllStateNames(AnimatorController controller)
        {
            var names = new List<string>();
            foreach (AnimatorControllerLayer layer in controller.layers)
            {
                CollectStateNamesRecursive(layer.stateMachine, names);
            }
            return names.Distinct().OrderBy(n => n).ToList();
        }

        private static void CollectStateNamesRecursive(AnimatorStateMachine stateMachine, List<string> names)
        {
            foreach (ChildAnimatorState childState in stateMachine.states)
            {
                names.Add(childState.state.name);
            }
            foreach (ChildAnimatorStateMachine childMachine in stateMachine.stateMachines)
            {
                CollectStateNamesRecursive(childMachine.stateMachine, names);
            }
        }
#endif

        /// <summary>
        /// Plays a random reaction animation (via NPCAnimationBridge) and, if any are
        /// configured, a random accompanying sound. Called by PlayerDeedBroadcaster in place of
        /// this NPC's normal rumor presentation when Mode is PlayAnimation.
        /// </summary>
        public void PlayWitnessReaction()
        {
            if (_reactionAnimationStates.Count == 0)
            {
                Debug.LogWarning($"<color=orange>[NPCWitnessReaction]</color> '{gameObject.name}' is set to Play Animation mode, but has no Reaction Animation States configured.", this);
            }
            else if (_animationBridge != null)
            {
                string stateName = _reactionAnimationStates[Random.Range(0, _reactionAnimationStates.Count)];
                if (!string.IsNullOrEmpty(stateName))
                {
                    int stateHash = Animator.StringToHash(stateName);
                    _animationBridge.SetAnimationState(stateHash);
                }
            }

            if (_reactionAudioClips.Count > 0 && _audioSource != null)
            {
                AudioClip clip = _reactionAudioClips[Random.Range(0, _reactionAudioClips.Count)];
                if (clip != null)
                {
                    _audioSource.PlayOneShot(clip);
                }
            }
        }
    }
}