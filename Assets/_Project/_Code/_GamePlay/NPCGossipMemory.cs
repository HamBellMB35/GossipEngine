using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Project.UI;
using Project.Data;

namespace Project.GamePlay
{
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

        // v7: Added. Fires only when a rumor is actually newly added to KnownRumors (not on
        // duplicate no-ops). Purely an optional hook — NPCGossipMemory has no idea who (if
        // anyone) is listening. Lets fully decoupled visualization tools (e.g. NPCRumorIndicator)
        // react without NPCGossipMemory needing any awareness of them.
        public event Action<int> OnKnownRumorCountChanged;

        private void Awake()
        {
            if (_speechBubble == null) _speechBubble = GetComponentInChildren<NPCSpeechBubble>();
            if (_audioSource == null) _audioSource = GetComponent<AudioSource>();
            if (_animationBridge == null) _animationBridge = GetComponent<NPCAnimationBridge>();
        }

        /// <summary>
        /// Adds a rumor to this NPC's memory. Called by the Gossip system when
        /// this NPC witnesses an event or receives a rumor from another NPC.
        /// Does nothing if this NPC already knows this rumor (by RumorID).
        /// </summary>
        public void LearnRumor(RumorTemplate rumor, float credibility)
        {
            if (rumor == null) return;
            if (KnowsRumor(rumor.RumorID)) return;

            KnownRumors.Add(new RuntimeRumorState(rumor, credibility));
            OnKnownRumorCountChanged?.Invoke(KnownRumors.Count);
        }

        /// <summary>
        /// Returns true if this NPC already has a rumor with the given ID in memory.
        /// </summary>
        public bool KnowsRumor(string rumorId)
        {
            return KnownRumors.Any(state => state.SourceTemplate != null && state.SourceTemplate.RumorID == rumorId);
        }

        /// <summary>
        /// Learns a rumor and, if its TriggerMode is AutoProximity, immediately presents it
        /// (text + optional audio + tone animation). ManualTalk rumors are learned but not
        /// auto-presented — they surface later via NPCProximityGossip when the player interacts.
        /// </summary>
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
        /// AutoProximity rumors that have already been presented once are skipped, so they
        /// don't re-fire every time the player re-enters the trigger zone. ManualTalk rumors
        /// are always eligible, since re-triggering on [E] is expected.
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
        /// Presents a rumor on this NPC right now: shows its display text in the speech bubble,
        /// plays its optional voice line, and plays its associated tone's animation (respecting
        /// PlaybackMode: None = no animation, PlayOnce/Loop = play then auto-revert to Idle).
        /// </summary>
        public void PresentRumor(RumorTemplate rumor)
        {
            if (rumor == null) return;

            if (rumor.ShowTextBubble && !string.IsNullOrEmpty(rumor.RumorDisplayText))
            {
                if (_speechBubble != null)
                {
                    _speechBubble.DisplayText(rumor.RumorDisplayText);
                }
                else
                {
                    Debug.LogWarning($"<color=orange>[NPCGossipMemory]</color> '{gameObject.name}' has no NPCSpeechBubble to display rumor text on.", this);
                }
            }

            if (rumor.VoiceLineAudio != null)
            {
                if (_audioSource != null)
                {
                    _audioSource.clip = rumor.VoiceLineAudio;
                    _audioSource.Play();
                }
                else
                {
                    Debug.LogWarning($"<color=orange>[NPCGossipMemory]</color> '{gameObject.name}' has a rumor voice line but no AudioSource to play it on.", this);
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
    }
}