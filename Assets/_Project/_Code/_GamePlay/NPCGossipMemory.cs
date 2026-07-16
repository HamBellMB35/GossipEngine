using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
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

        [Header("Voice Settings")]
        [Tooltip("Which gendered voice line this NPC uses when a response provides both. Falls back to whichever clip is actually assigned if the selected gender's is empty.")]
        [SerializeField] private VoiceGender _voiceGender = VoiceGender.Male;

        public event Action<int> OnKnownRumorCountChanged;

        private NPCReputationOpinion _reputationOpinion;
        private GossipManager _gossipManager;
        private ReputationService _reputationService;

        // Same no-repeat tracking as NPCGreetingResponder, applied to this NPC's own
        // general-pool fallback tier.
        private int _lastPositiveIndex = -1;
        private int _lastNegativeIndex = -1;

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

        public RuntimeRumorState GetNextRumorToShare(RumorTriggerMode mode)
        {
            return KnownRumors.FirstOrDefault(state =>
                state.SourceTemplate != null &&
                state.SourceTemplate.TriggerMode == mode &&
                (mode != RumorTriggerMode.AutoProximity || !state.HasBeenPresented));
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