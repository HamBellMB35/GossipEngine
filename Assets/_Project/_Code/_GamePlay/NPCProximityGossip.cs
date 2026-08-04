using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using TownsPeople.Data;
using TownsPeople.UI;

namespace TownsPeople.GamePlay
{
    // v6: The [E] prompt now fades in/out via CanvasGroupFader instead of snapping alpha
    // instantly � matching NPCSpeechBubble's existing fade behavior. Fade durations are
    // editable per-NPC on the CanvasGroupFader component itself (Fade In/Out Duration).
    //
    // v7: Locomotion <-> Interaction integration. Resolves an OPTIONAL INpcMovementController
    // (currently implemented only by LocomotionAgent) via plain GetComponent<T>() � no
    // reflection needed, since the interface itself lives in Core. Every reference is null-safe:
    // an NPC without Locomotion behaves exactly as it did before this version existed.
    // - While IsRunning is true, this NPC is not interactable at all: no [E] prompt, no
    //   Auto-Proximity rumor trigger, no response to [E] being pressed. Running NPCs must never
    //   be stopped mid-dash by an interaction.
    // - At the moment ANY interaction begins (dialogue menu, vendor/quest hijack, ambient
    //   greeting, or the oldest scripted-dialogue fallback), PauseForInteraction() halts
    //   movement and NPCAnimationBridge.PlayIdleForInteraction() swaps this NPC onto the same
    //   idle pool a non-Locomotion NPC already uses � "displaying all the animations and
    //   behaviours a non-Locomotion NPC would display," exactly as specified.
    // - The moment that interaction ends � dialogue closed OR the player walks away �
    //   ResumeAfterInteraction() and NPCAnimationBridge.ReleaseReactionOverride() run together,
    //   at the START of InteractionCooldownSequence() (not gated behind the cooldown wait,
    //   which exists only to prevent E-spam, not to delay movement resumption).


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
        // v7: OPTIONAL — null on any NPC without a component implementing INpcMovementController
        // (i.e. no Locomotion add-on, or Locomotion not attached to this specific NPC). Every
        // usage below is null-conditional (?.) so behavior is unchanged for those NPCs.
        private INpcMovementController _movementController;

        private void Awake()
        {
            _addonRegistry = GetComponent<NpcAddonRegistry>();
            _gossipMemory = GetComponent<NPCGossipMemory>();
            _animationBridge = GetComponent<NPCAnimationBridge>();
            _greetingResponder = GetComponent<NPCGreetingResponder>();
            _reputationOpinion = GetComponent<NPCReputationOpinion>();
            _nameplate = GetComponent<TownsPeople.UI.NPCNameplate>();
            _audioSource = GetComponent<AudioSource>();
            // v7: Plain interface lookup — no reflection required, since INpcMovementController
            // is defined in Core. Resolves LocomotionAgent automatically if present, without
            // this script ever referencing that (or any future movement add-on's) concrete type.
            _movementController = GetComponent<INpcMovementController>();
        }

        private void Start()
        {
            interactionPromptFader?.SetInstant(false);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;

            _isPlayerInZone = true;

            // v7: A running NPC is not interactable at all — no Auto-Proximity trigger, no [E]
            // prompt. Bail out entirely before either check below runs.
            if (_movementController != null && _movementController.IsRunning)
            {
                return;
            }

            RuntimeRumorState autoRumor = _gossipMemory != null
                ? _gossipMemory.GetNextRumorToShare(RumorTriggerMode.AutoProximity)
                : null;

            if (autoRumor != null)
            {
                _gossipMemory.PresentRumor(autoRumor.SourceTemplate);
                return; // Skip showing the [E] prompt � nothing further required from the player.
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

            // v9: Force-close the dialogue menu if it's currently open for THIS NPC � walking
            // away now has the same full cleanup effect as clicking Leave. Close() itself
            // handles hiding this NPC's speech bubble.
            if (DialogueMenuUI.Instance != null && _gossipMemory != null && DialogueMenuUI.Instance.IsOpenFor(_gossipMemory))
            {
                DialogueMenuUI.Instance.Close();
            }
            else
            {
                // Menu wasn't open � still make sure a lingering speech bubble (e.g. from an
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

        // v7: Tracks the running state as of last frame, so the [E] prompt can react to a
        // Locomotion NPC transitioning into/out of a Run leg WHILE the player is already
        // standing in its trigger zone (OnTriggerEnter alone can't catch this, since it only
        // fires once, on entry).
        private bool _wasRunningLastFrame;

        private void Update()
        {
            if (Keyboard.current == null) return;

            // v7: Reactively show/hide the prompt as this NPC starts or stops running, for as
            // long as the player remains in the zone. Skipped entirely for NPCs with no
            // movement controller — zero extra cost for the common non-Locomotion case.
            if (_movementController != null && _isPlayerInZone && !_isOnCooldown)
            {
                bool isRunningNow = _movementController.IsRunning;
                if (isRunningNow != _wasRunningLastFrame)
                {
                    if (isRunningNow)
                    {
                        interactionPromptFader?.Hide();
                    }
                    else
                    {
                        interactionPromptFader?.Show();
                    }
                    _wasRunningLastFrame = isRunningNow;
                }
            }

            // v7: Extra safety net alongside the OnTriggerEnter/Update guard above — even if the
            // prompt's fade hasn't fully caught up yet, an actual [E] press while running is
            // ignored outright inside ExecuteInteraction() itself.
            if (_isPlayerInZone && !_isOnCooldown && Keyboard.current.eKey.wasPressedThisFrame)
            {
                ExecuteInteraction();
            }
        }

        public void ExecuteInteraction()
        {
            if (_isOnCooldown || _isDeferringInteraction) return;

            // v7: Running NPCs are never interactable, full stop — must never be yanked out of
            // a dash by the player pressing [E].
            if (_movementController != null && _movementController.IsRunning) return;

            // v10: If this NPC is currently mid-audio (e.g. reacting to a just-witnessed
            // PlayerDeedBroadcaster event), defer the interaction instead of immediately
            // playing new audio over it � Unity's AudioSource.Play() always cuts off whatever
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
            // v11: Click sound for [E] press itself � covers every interaction path
            // uniformly (vendor hijack, dialogue menu opening, greeting responder, old
            // fallback). No-ops silently if no click sound is assigned.
            TownsPeople.UI.DialogueMenuUI.Instance?.PlayClickSound();

            interactionPromptFader?.Hide();

            // v10: Clear any lingering nameplate/bubble the instant the player actually
            // interacts, instead of stale proximity-driven UI overlapping with whatever comes
            // next (menu, vendor, greeting). Nameplate stays suppressed for the whole
            // interaction � see InteractionCooldownSequence, which un-suppresses it once the
            // interaction actually ends (including once the dialogue menu closes).
            _nameplate?.SetSuppressed(true);
            speechBubble?.HideImmediately();
            _gossipMemory?.HideSpeechBubble();
            _greetingResponder?.HideSpeechBubble();

            // v7: If this NPC has Locomotion, halt its movement and swap it onto the same idle
            // pool a non-Locomotion NPC already displays — "displaying all the animations and
            // behaviours a non-Locomotion NPC would display," for the whole interaction. No-op
            // for any NPC without Locomotion, since _movementController is null there.
            if (_movementController != null)
            {
                _movementController.PauseForInteraction();
                _animationBridge?.PlayIdleForInteraction();
            }

            IInteractionExtension extension = _addonRegistry != null
                ? _addonRegistry.GetActiveInteractionExtension()
                : null;

            if (extension != null && extension.OnExtendInteraction())
            {
                // Vendor/Quest hijack behavior is UNCHANGED � instant, no menu, starts
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
            // on the streets?" option instead � the player now asks for it explicitly.
            if (_gossipMemory != null)
            {
                OpenDialogueMenu();
                return; // Cooldown starts when the menu closes, not here � see OnDialogueMenuClosed.
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
                Debug.LogWarning("<color=orange>[NPCProximityGossip]</color> No DialogueMenuUI found in the scene � generate one via Tools > NPC Creator > Generate Dialogue Menu UI.", this);
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
            // closed, vendor hijack finished, etc.) � no reason to keep it hidden through the
            // whole cooldown wait too.
            _nameplate?.SetSuppressed(false);

            // v7: Resume movement and release the Reactions-layer override IMMEDIATELY �
            // deliberately not gated behind the cooldown wait below, which exists only to
            // prevent E-spamming a fresh interaction, not to delay the NPC resuming its walk.
            // No-op for any NPC without Locomotion.
            if (_movementController != null)
            {
                _movementController.ResumeAfterInteraction();
                _animationBridge?.ReleaseReactionOverride();
            }

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