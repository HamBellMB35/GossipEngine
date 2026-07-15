using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Project.Data;
using Project.UI;

namespace Project.GamePlay
{
    // v6: The [E] prompt now fades in/out via CanvasGroupFader instead of snapping alpha
    // instantly — matching NPCSpeechBubble's existing fade behavior. Fade durations are
    // editable per-NPC on the CanvasGroupFader component itself (Fade In/Out Duration).

    public class NPCProximityGossip : MonoBehaviour
    {
        [Header("Dependency Mappings")]
        [SerializeField] private Project.Data.NPCArchetypeConfiguration archetypeConfig;
        [SerializeField] private Project.UI.NPCSpeechBubble speechBubble;
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
        private NpcAddonRegistry _addonRegistry;
        private NPCGossipMemory _gossipMemory;
        private NPCAnimationBridge _animationBridge;
        private NPCGreetingResponder _greetingResponder;

        private void Awake()
        {
            _addonRegistry = GetComponent<NpcAddonRegistry>();
            _gossipMemory = GetComponent<NPCGossipMemory>();
            _animationBridge = GetComponent<NPCAnimationBridge>();
            _greetingResponder = GetComponent<NPCGreetingResponder>();
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
            if (_isOnCooldown) return;

            interactionPromptFader?.Hide();

            IInteractionExtension extension = _addonRegistry != null
                ? _addonRegistry.GetActiveInteractionExtension()
                : null;

            if (extension != null && extension.OnExtendInteraction())
            {
                return;
            }

            ExecuteAmbientGreeting();
            StartCoroutine(InteractionCooldownSequence());
        }

        private void ExecuteAmbientGreeting()
        {
            RuntimeRumorState manualRumor = _gossipMemory != null
                ? _gossipMemory.GetNextRumorToShare(RumorTriggerMode.ManualTalk)
                : null;

            if (manualRumor != null)
            {
                _gossipMemory.PresentRumor(manualRumor.SourceTemplate);
                return;
            }

            // v7: Reputation-driven greeting (Positive/Negative pool, gendered audio) now takes
            // priority over the old static ScriptedDialogues pool. This is what "Non-Dialogue"
            // NPCs (wizard toggle) rely on exclusively, since they have no NPCGossipMemory at all.
            if (_greetingResponder != null)
            {
                _greetingResponder.PlayGreeting();
                return;
            }

            if (archetypeConfig == null) return;
            if (archetypeConfig.ScriptedDialogues == null || archetypeConfig.ScriptedDialogues.Count == 0) return;

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

        private IEnumerator InteractionCooldownSequence()
        {
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