using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Project.UI;
using Project.Data;

namespace Project.GamePlay
{
    // v3: KnownRumors now stores real RuntimeRumorState objects (rumor + credibility + timestamp)
    // instead of raw ID strings. This was already a data class in the project (RuntimeRumorState)
    // but wasn't being used anywhere — this is what lets NPCProximityGossip actually check a
    // known rumor's TriggerMode at proximity/interaction time instead of just knowing its name.
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

            bool alreadyKnown = KnownRumors.Any(state => state.SourceTemplate != null && state.SourceTemplate.RumorID == rumor.RumorID);
            if (!alreadyKnown)
            {
                KnownRumors.Add(new RuntimeRumorState(rumor, credibility));
            }
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
        /// Returns null if none are known. Used by NPCProximityGossip to decide what to
        /// present on proximity-enter (AutoProximity) or on [E]-interact (ManualTalk).
        /// </summary>
        public RuntimeRumorState GetNextRumorToShare(RumorTriggerMode mode)
        {
            return KnownRumors.FirstOrDefault(state =>
                state.SourceTemplate != null &&
                state.SourceTemplate.TriggerMode == mode);
        }

        /// <summary>
        /// Presents a rumor on this NPC right now: shows its display text in the speech bubble,
        /// plays its optional voice line, and plays its associated tone's animation (respecting
        /// PlaybackMode: None = no animation, PlayOnce/Loop = play then auto-revert to Idle).
        /// </summary>
        public void PresentRumor(RumorTemplate rumor)
        {
            if (rumor == null) return;

            // v4: Text bubble now respects the rumor's ShowTextBubble toggle — a rumor can
            // still play audio/animation while staying silent in the speech bubble.
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

            Debug.Log($"<color=cyan>[NPCGossipMemory]</color> '{gameObject.name}' presented rumor '{rumor.RumorID}'.");
        }
    }
}