using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using VContainer;
using Project.UI;
using Project.Data;
using Project.Services;

namespace Project.GamePlay
{
    // v8: PresentRumor now chooses WHAT to say from three tiers, in order:
    // 1. The rumor's own SpecificResponses (rotated, shared usage count game-wide via
    //    GossipManager) — fresh, unique reactions to this specific piece of gossip.
    // 2. Once those are exhausted, the general Positive/Negative pool (GeneralRumorResponseLibrary),
    //    chosen by the PLAYER'S CURRENT REPUTATION as seen by this NPC — not by the rumor's own
    //    Alignment. This is what makes NPCs eventually just react to "how do I feel about the
    //    player right now" instead of endlessly repeating specific gossip content.
    // 3. The rumor's own RumorDisplayText/VoiceLineAudio, as an always-available fallback if
    //    neither of the above produced anything (e.g. minimal setup with no response arrays).
    /// <summary>
    /// Per-NPC configuration for one dialogue menu option: whether it appears at all, and an
    /// optional custom label overriding the default generated text.
    /// </summary>
    [System.Serializable]
    public struct DialogueOptionSettings
    {
        [Tooltip("If disabled, this option never appears in the dialogue menu for this NPC.")]
        public bool Enabled;

        [Tooltip("Custom label for this option. Leave empty to use the default generated label.")]
        public string CustomLabel;
    }

    /// <summary>
    /// v18: A fully custom, NPC-authored dialogue option. Wire OnSelected to any method on any
    /// component directly in the Inspector — no code required per new option. Unlike Greet/
    /// Rumors, these have no built-in conditional-interactability logic; they're always
    /// available whenever Enabled.
    /// </summary>
    [System.Serializable]
    public struct CustomDialogueOption
    {
        [Tooltip("If disabled, this option never appears.")]
        public bool Enabled;

        [Tooltip("Text shown for this option in the dialogue menu.")]
        public string Label;

        [Tooltip("Invoked when the player selects this option. Wire this to any method on any component.")]
        public UnityEvent OnSelected;
    }

    public class NPCGossipMemory : MonoBehaviour
    {
        [Tooltip("Display name for this NPC, used for debug logging and identification.")]
        public string NpcName;

        [Tooltip("Rumors this NPC currently knows about, with per-NPC credibility and timing.")]
        public List<RuntimeRumorState> KnownRumors = new List<RuntimeRumorState>();

        [Header("Presentation Dependencies (auto-resolved if left empty)")]
        [SerializeField] private NPCSpeechBubble _speechBubble;
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private NPCAnimationBridge _animationBridge;

        [Header("Fallback Response Pool")]
        [Tooltip("Shared library of generic Positive/Negative reactions, used once a rumor's own Specific Responses are exhausted. Safe to leave empty — the rumor's own default text/audio will be used instead.")]
        [SerializeField] private GeneralRumorResponseLibrary _responseLibrary;

        [Header("Dialogue Menu Options")]
        [Tooltip("Controls whether/how the 'Greet' option appears for this NPC in the dialogue menu.")]
        [SerializeField] private DialogueOptionSettings _greetOption = new DialogueOptionSettings { Enabled = true, CustomLabel = "" };

        [Tooltip("Controls whether/how the 'ask about rumors' option appears for this NPC in the dialogue menu. Still only shows if this NPC actually knows at least one rumor.")]
        [SerializeField] private DialogueOptionSettings _rumorsOption = new DialogueOptionSettings { Enabled = true, CustomLabel = "" };

        public DialogueOptionSettings GreetOptionSettings => _greetOption;
        public DialogueOptionSettings RumorsOptionSettings => _rumorsOption;

        [Tooltip("Additional, fully custom options this NPC offers beyond Greet/Ask About Rumors. Each can call any method via its own OnSelected event — add as many as you want.")]
        [SerializeField] private List<CustomDialogueOption> _customOptions = new List<CustomDialogueOption>();

        public IReadOnlyList<CustomDialogueOption> CustomOptions => _customOptions;

        [Header("Voice Settings")]
        [Tooltip("Which gendered voice line this NPC uses when a response provides both. Falls back to whichever clip is actually assigned if the selected gender's is empty.")]
        [SerializeField] private VoiceGender _voiceGender = VoiceGender.Male;

        public event Action<int> OnKnownRumorCountChanged;

        private NPCReputationOpinion _reputationOpinion;
        private GossipManager _gossipManager;
        private ReputationService _reputationService;

        // Same no-repeat tracking as NPCGreetingResponder, applied to this NPC's own
        // general-pool fallback tier AND its standalone PlayGreeting() below.
        private int _lastPositiveIndex = -1;
        private int _lastNegativeIndex = -1;

        // v15: Which known rumor "What do you hear on the streets?" tells next. Cycles
        // through KnownRumors in order, wrapping around — independent of TriggerMode/
        // HasBeenPresented, since the player is explicitly asking, not passively triggering.
        private int _nextRumorToTellIndex = 0;

        [Inject]
        public void Construct(GossipManager gossipManager, ReputationService reputationService)
        {
            _gossipManager = gossipManager;
            _reputationService = reputationService;
        }

        private void Awake()
        {
            if (_speechBubble == null) _speechBubble = GetComponentInChildren<NPCSpeechBubble>();
            if (_audioSource == null) _audioSource = GetComponent<AudioSource>();
            if (_animationBridge == null) _animationBridge = GetComponent<NPCAnimationBridge>();
            _reputationOpinion = GetComponent<NPCReputationOpinion>();
        }

        public void LearnRumor(RumorTemplate rumor, float credibility)
        {
            if (rumor == null) return;
            if (KnowsRumor(rumor.RumorID)) return;

            KnownRumors.Add(new RuntimeRumorState(rumor, credibility));
            OnKnownRumorCountChanged?.Invoke(KnownRumors.Count);
        }

        public bool KnowsRumor(string rumorId)
        {
            return KnownRumors.Any(state => state.SourceTemplate != null && state.SourceTemplate.RumorID == rumorId);
        }

        public void LearnAndPresentRumor(RumorTemplate rumor, float credibility)
        {
            if (rumor == null)
            {
                Debug.LogWarning($"<color=orange>[NPCGossipMemory]</color> '{gameObject.name}' tried to learn a null rumor.", this);
                return;
            }

            LearnRumor(rumor, credibility);

            if (rumor.TriggerMode == RumorTriggerMode.AutoProximity)
            {
                PresentRumor(rumor);
            }
            else
            {
                Debug.Log($"<color=grey>[NPCGossipMemory]</color> '{gameObject.name}' learned '{rumor.RumorID}' (ManualTalk) — will surface on player interaction.");
            }
        }

        /// <summary>
        /// Looks through this NPC's known rumors for one matching the given TriggerMode.
        /// In practice, this is now only meaningfully called with AutoProximity (from
        /// NPCProximityGossip.OnTriggerEnter) — AutoProximity rumors that have already been
        /// presented once are skipped, so they don't re-fire every time the player re-enters
        /// the trigger zone. ManualTalk rumors no longer auto-surface through this method at
        /// all (see NPCProximityGossip's dialogue menu integration); they're only reachable via
        /// TryTellNextRumor(), which ignores TriggerMode entirely. The parameter/signature is
        /// kept generic in case a future system wants ManualTalk-specific auto-surfacing again.
        /// Returns null if none are eligible.
        /// </summary>
        public RuntimeRumorState GetNextRumorToShare(RumorTriggerMode mode)
        {
            return KnownRumors.FirstOrDefault(state =>
                state.SourceTemplate != null &&
                state.SourceTemplate.TriggerMode == mode &&
                (mode != RumorTriggerMode.AutoProximity || !state.HasBeenPresented));
        }

        /// <summary>
        /// v15: Presents a standalone reputation-driven greeting (Positive/Negative pool,
        /// gendered audio) — the same mechanism NPCGreetingResponder uses, but reusing this
        /// NPC's own _responseLibrary/_voiceGender fields directly rather than requiring a
        /// separate component. Used as the opening line when the dialogue menu is opened.
        /// </summary>
        public void PlayGreeting()
        {
            if (_responseLibrary == null)
            {
                Debug.LogWarning($"<color=orange>[NPCGossipMemory]</color> '{gameObject.name}' has no Response Library assigned — cannot play a greeting.", this);
                return;
            }

            RumorAlignment standing = GetPlayerStandingAlignment();
            RumorResponse? response = standing == RumorAlignment.Positive
                ? _responseLibrary.GetRandomResponse(standing, ref _lastPositiveIndex)
                : _responseLibrary.GetRandomResponse(standing, ref _lastNegativeIndex);

            if (response == null) return;

            if (_speechBubble != null && !string.IsNullOrEmpty(response.Value.ResponseText))
            {
                _speechBubble.DisplayText(response.Value.ResponseText);
            }

            AudioClip clip = response.Value.GetVoiceLine(_voiceGender);
            if (clip != null && _audioSource != null)
            {
                _audioSource.clip = clip;
                _audioSource.Play();
            }
        }

        /// <summary>
        /// v15: Tells the next known rumor (cycling through all of them, wrapping around),
        /// used by the dialogue menu's "What do you hear on the streets?" option — one rumor
        /// per call. Returns false if this NPC knows nothing yet.
        /// </summary>
        public bool TryTellNextRumor()
        {
            if (KnownRumors.Count == 0) return false;

            RuntimeRumorState state = KnownRumors[_nextRumorToTellIndex % KnownRumors.Count];
            _nextRumorToTellIndex++;

            PresentRumor(state.SourceTemplate);
            return true;
        }

        /// <summary>
        /// Presents a rumor on this NPC right now. See the class-level comment above for the
        /// three-tier content selection (Specific Response -> General pool by player standing
        /// -> rumor's own default text/audio). Animation is always driven by the rumor's own
        /// AssociatedTone, regardless of which text/audio tier was used.
        /// </summary>
        public void PresentRumor(RumorTemplate rumor)
        {
            if (rumor == null) return;

            RumorResponse? specificResponse = _gossipManager?.GetSpecificResponse(rumor);
            RumorResponse? chosenResponse;

            if (specificResponse != null)
            {
                chosenResponse = specificResponse;
            }
            else if (_responseLibrary != null)
            {
                RumorAlignment fallbackPool = GetPlayerStandingAlignment();
                chosenResponse = fallbackPool == RumorAlignment.Positive
                    ? _responseLibrary.GetRandomResponse(fallbackPool, ref _lastPositiveIndex)
                    : _responseLibrary.GetRandomResponse(fallbackPool, ref _lastNegativeIndex);
            }
            else
            {
                chosenResponse = null;
            }

            string textToShow = (chosenResponse.HasValue && !string.IsNullOrEmpty(chosenResponse.Value.ResponseText))
                ? chosenResponse.Value.ResponseText
                : rumor.RumorDisplayText;

            AudioClip audioToPlay = chosenResponse.HasValue
                ? chosenResponse.Value.GetVoiceLine(_voiceGender)
                : null;

            if (audioToPlay == null)
            {
                audioToPlay = rumor.VoiceLineAudio;
            }

            if (rumor.ShowTextBubble && !string.IsNullOrEmpty(textToShow))
            {
                if (_speechBubble != null)
                {
                    _speechBubble.DisplayText(textToShow);
                }
                else
                {
                    Debug.LogWarning($"<color=orange>[NPCGossipMemory]</color> '{gameObject.name}' has no NPCSpeechBubble to display rumor text on.", this);
                }
            }

            if (audioToPlay != null)
            {
                if (_audioSource != null)
                {
                    _audioSource.clip = audioToPlay;
                    _audioSource.Play();
                }
                else
                {
                    Debug.LogWarning($"<color=orange>[NPCGossipMemory]</color> '{gameObject.name}' has a response voice line but no AudioSource to play it on.", this);
                }
            }

            if (_animationBridge != null)
            {
                _animationBridge.PlayToneAnimation(rumor.AssociatedTone);
            }

            RuntimeRumorState matchedState = KnownRumors.FirstOrDefault(s => s.SourceTemplate != null && s.SourceTemplate.RumorID == rumor.RumorID);
            if (matchedState != null)
            {
                matchedState.HasBeenPresented = true;
            }

            Debug.Log($"<color=cyan>[NPCGossipMemory]</color> '{gameObject.name}' presented rumor '{rumor.RumorID}'.");
        }

        /// <summary>
        /// This NPC's read on the player's current standing: its own NPCReputationOpinion
        /// (general + faction + personal witness modifier) if present, otherwise just the
        /// shared general reputation. Used only to pick which General pool to fall back to —
        /// unrelated to the triggering rumor's own Alignment.
        /// </summary>
        private RumorAlignment GetPlayerStandingAlignment()
        {
            float effectiveReputation = _reputationOpinion != null
                ? _reputationOpinion.GetEffectiveReputation()
                : (_reputationService != null ? _reputationService.GetGeneralReputation() : 0f);

            return effectiveReputation >= 0f ? RumorAlignment.Positive : RumorAlignment.Negative;
        }
    }
}