using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using TownsPeople.Data;
using TownsPeople.UI;

namespace TownsPeople.GamePlay
{
    // v6: The [E] prompt now fades in/out via CanvasGroupFader instead of snapping alpha
    // instantly — matching NPCSpeechBubble's existing fade behavior. Fade durations are
    // editable per-NPC on the CanvasGroupFader component itself (Fade In/Out Duration).

    public class NPCProximityGossip : MonoBehaviour
    {
        [Header("Dependency Mappings")]
        [SerializeField] private TownsPeople.Data.NPCArchetypeConfiguration archetypeConfig;
        [SerializeField] private TownsPeople.UI.NPCSpeechBubble speechBubble;
        [SerializeField] private CanvasGroupFader interactionPromptFader;

        [Header("Gossip Timing Configurations")]
        [SerializeField] private float interactionCooldownDuration = 10f;
        [SerializeField] private float speechBubbleHideDuration = 13f;

        [Header("Editor Visualization")]
        [Tooltip("If enabled, draws a wire sphere in the Scene view showing this NPC's proximity/interaction range at all times (not just when selected).")]
        [SerializeField] private bool _showRangeGizmo = true;

        [Tooltip("Color of the range gizmo sphere.")]
        [SerializeField] private Color _rangeGizmoColor = new Color(0f, 0.6f, 1f, 0.5f);

        private bool _isPlayerInZone = false;
        private bool _isOnCooldown = false;
        private bool _isDeferringInteraction = false;
        private NpcAddonRegistry _addonRegistry;
        private NPCGossipMemory _gossipMemory;
        private NPCAnimationBridge _animationBridge;
        private NPCGreetingResponder _greetingResponder;
        private NPCReputationOpinion _reputationOpinion;
        private TownsPeople.UI.NPCNameplate _nameplate;
        private AudioSource _audioSource;

        private void Awake()
        {
            _addonRegistry = GetComponent<NpcAddonRegistry>();
            _gossipMemory = GetComponent<NPCGossipMemory>();
            _animationBridge = GetComponent<NPCAnimationBridge>();
            _greetingResponder = GetComponent<NPCGreetingResponder>();
            _reputationOpinion = GetComponent<NPCReputationOpinion>();
            _nameplate = GetComponent<TownsPeople.UI.NPCNameplate>();
            _audioSource = GetComponent<AudioSource>();
        }

        private void Start()
        {
            interactionPromptFader?.SetInstant(false);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;

            _isPlayerInZone = true;

            RuntimeRumorState autoRumor = _gossipMemory != null
                ? _gossipMemory.GetNextRumorToShare(RumorTriggerMode.AutoProximity)
                : null;

            if (autoRumor != null)
            {
                _gossipMemory.PresentRumor(autoRumor.SourceTemplate);
                return; // Skip showing the [E] prompt — nothing further required from the player.
            }

            if (!_isOnCooldown)
            {
                interactionPromptFader?.Show();
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Player")) return;

            _isPlayerInZone = false;

            interactionPromptFader?.Hide();

            // v9: Force-close the dialogue menu if it's currently open for THIS NPC — walking
            // away now has the same full cleanup effect as clicking Leave. Close() itself
            // handles hiding this NPC's speech bubble.
            if (DialogueMenuUI.Instance != null && _gossipMemory != null && DialogueMenuUI.Instance.IsOpenFor(_gossipMemory))
            {
                DialogueMenuUI.Instance.Close();
            }
            else
            {
                // Menu wasn't open — still make sure a lingering speech bubble (e.g. from an
                // Auto-Proximity greeting or the old ScriptedDialogues fallback) fades out now
                // instead of waiting out its own internal timer.
                speechBubble?.HideImmediately();
                _gossipMemory?.HideSpeechBubble();
                _greetingResponder?.HideSpeechBubble();
            }

            // Don't leave the NPC frozen mid-animation just because the player walked off.
            if (_animationBridge != null)
            {
                _animationBridge.ForceRevertToIdle();
            }
        }

        private void Update()
        {
            if (Keyboard.current == null) return;

            if (_isPlayerInZone && !_isOnCooldown && Keyboard.current.eKey.wasPressedThisFrame)
            {
                ExecuteInteraction();
            }
        }

        public void ExecuteInteraction()
        {
            if (_isOnCooldown || _isDeferringInteraction) return;

            // v10: If this NPC is currently mid-audio (e.g. reacting to a just-witnessed
            // PlayerDeedBroadcaster event), defer the interaction instead of immediately
            // playing new audio over it — Unity's AudioSource.Play() always cuts off whatever
            // is currently playing, which previously caused witness-reaction audio to get cut
            // off by the interaction's own greeting audio if both fired close together. This
            // is intentionally one-directional: a NEW witness reaction is still allowed to
            // interrupt an already-open interaction, since that's treated as more urgent.
            if (_audioSource != null && _audioSource.isPlaying)
            {
                _isDeferringInteraction = true;
                StartCoroutine(DeferInteractionUntilAudioFinishes());
                return;
            }

            ExecuteInteractionImmediate();
        }

        private IEnumerator DeferInteractionUntilAudioFinishes()
        {
            while (_audioSource != null && _audioSource.isPlaying)
            {
                yield return null;
            }

            _isDeferringInteraction = false;
            ExecuteInteractionImmediate();
        }

        private void ExecuteInteractionImmediate()
        {
            // v11: Click sound for [E] press itself — covers every interaction path
            // uniformly (vendor hijack, dialogue menu opening, greeting responder, old
            // fallback). No-ops silently if no click sound is assigned.
            TownsPeople.UI.DialogueMenuUI.Instance?.PlayClickSound();

            interactionPromptFader?.Hide();

            // v10: Clear any lingering nameplate/bubble the instant the player actually
            // interacts, instead of stale proximity-driven UI overlapping with whatever comes
            // next (menu, vendor, greeting). Nameplate stays suppressed for the whole
            // interaction — see InteractionCooldownSequence, which un-suppresses it once the
            // interaction actually ends (including once the dialogue menu closes).
            _nameplate?.SetSuppressed(true);
            speechBubble?.HideImmediately();
            _gossipMemory?.HideSpeechBubble();
            _greetingResponder?.HideSpeechBubble();

            IInteractionExtension extension = _addonRegistry != null
                ? _addonRegistry.GetActiveInteractionExtension()
                : null;

            if (extension != null && extension.OnExtendInteraction())
            {
                // Vendor/Quest hijack behavior is UNCHANGED — instant, no menu, starts
                // cooldown immediately, exactly as before.
                StartCoroutine(InteractionCooldownSequence());
                return;
            }

            ExecuteAmbientGreeting();
        }

        private void ExecuteAmbientGreeting()
        {
            // v8: Common NPCs (have NPCGossipMemory) now open the dialogue menu instead of
            // auto-presenting a single response. The previous "pending Manual-Talk rumor
            // auto-presents immediately" behavior is folded into the menu's "What do you hear
            // on the streets?" option instead — the player now asks for it explicitly.
            if (_gossipMemory != null)
            {
                OpenDialogueMenu();
                return; // Cooldown starts when the menu closes, not here — see OnDialogueMenuClosed.
            }

            // Non-Dialogue NPCs: unchanged, single reputation-driven greeting, no menu.
            if (_greetingResponder != null)
            {
                _greetingResponder.PlayGreeting();
                StartCoroutine(InteractionCooldownSequence());
                return;
            }

            // Oldest fallback, for any NPC predating both systems above.
            if (archetypeConfig != null && archetypeConfig.ScriptedDialogues != null && archetypeConfig.ScriptedDialogues.Count > 0)
            {
                int randomIndex = UnityEngine.Random.Range(0, archetypeConfig.ScriptedDialogues.Count);
                var dialoguePacket = archetypeConfig.ScriptedDialogues[randomIndex];

                if (dialoguePacket.VoiceLineAudio != null)
                {
                    AudioSource myAudioSource = GetComponent<AudioSource>();
                    if (myAudioSource != null)
                    {
                        myAudioSource.clip = dialoguePacket.VoiceLineAudio;
                        myAudioSource.Play();
                    }
                }

                if (speechBubble != null)
                {
                    speechBubble.DisplayText(dialoguePacket.ResponseText);
                }
            }

            StartCoroutine(InteractionCooldownSequence());
        }

        private void OpenDialogueMenu()
        {
            if (DialogueMenuUI.Instance == null)
            {
                Debug.LogWarning("<color=orange>[NPCProximityGossip]</color> No DialogueMenuUI found in the scene — generate one via Tools > NPC Creator > Generate Dialogue Menu UI.", this);
                StartCoroutine(InteractionCooldownSequence());
                return;
            }

            DialogueMenuUI.Instance.Open(_gossipMemory.NpcName, _gossipMemory, _reputationOpinion, OnDialogueMenuClosed);
        }

        private void OnDialogueMenuClosed()
        {
            StartCoroutine(InteractionCooldownSequence());
        }

        private IEnumerator InteractionCooldownSequence()
        {
            // v10: Un-suppress the nameplate immediately once the interaction ends (menu
            // closed, vendor hijack finished, etc.) — no reason to keep it hidden through the
            // whole cooldown wait too.
            _nameplate?.SetSuppressed(false);

            _isOnCooldown = true;
            yield return new WaitForSeconds(interactionCooldownDuration);
            _isOnCooldown = false;

            if (_isPlayerInZone)
            {
                interactionPromptFader?.Show();
            }
        }

        private void OnDrawGizmos()
        {
            if (!_showRangeGizmo) return;

            SphereCollider triggerCollider = GetComponent<SphereCollider>();
            if (triggerCollider == null) return;

            Gizmos.color = _rangeGizmoColor;
            Gizmos.DrawWireSphere(transform.position + triggerCollider.center, triggerCollider.radius);
        }
    }
}