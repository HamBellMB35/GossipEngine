using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Project.Data;

namespace Project.GamePlay
{
    // v4: OnTriggerExit now calls NPCAnimationBridge.ForceRevertToIdle() when the player
    // leaves the trigger zone, so the NPC doesn't stay stuck mid-animation after the player
    // walks away (previously OnTriggerExit only hid the interaction prompt).

    public class NPCProximityGossip : MonoBehaviour
    {
        [Header("Dependency Mappings")]
        [SerializeField] private Project.Data.NPCArchetypeConfiguration archetypeConfig;
        [SerializeField] private Project.UI.NPCSpeechBubble speechBubble;
        [SerializeField] private CanvasGroup interactionPromptCanvasGroup;

        [Header("Gossip Timing Configurations")]
        [SerializeField] private float interactionCooldownDuration = 10f;
        [SerializeField] private float speechBubbleHideDuration = 13f;

        private bool _isPlayerInZone = false;
        private bool _isOnCooldown = false;
        private NpcAddonRegistry _addonRegistry;
        private NPCGossipMemory _gossipMemory;
        private NPCAnimationBridge _animationBridge;

        private void Awake()
        {
            _addonRegistry = GetComponent<NpcAddonRegistry>();
            _gossipMemory = GetComponent<NPCGossipMemory>();
            _animationBridge = GetComponent<NPCAnimationBridge>();
        }

        private void Start()
        {
            if (interactionPromptCanvasGroup != null)
            {
                interactionPromptCanvasGroup.alpha = 0f;
                interactionPromptCanvasGroup.interactable = false;
                interactionPromptCanvasGroup.blocksRaycasts = false;
            }
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

            if (!_isOnCooldown && interactionPromptCanvasGroup != null)
            {
                interactionPromptCanvasGroup.alpha = 1f;
                interactionPromptCanvasGroup.interactable = true;
                interactionPromptCanvasGroup.blocksRaycasts = true;
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Player")) return;

            _isPlayerInZone = false;

            if (interactionPromptCanvasGroup != null)
            {
                interactionPromptCanvasGroup.alpha = 0f;
                interactionPromptCanvasGroup.interactable = false;
                interactionPromptCanvasGroup.blocksRaycasts = false;
            }

            // v4: Don't leave the NPC frozen mid-animation just because the player walked off.
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

            if (interactionPromptCanvasGroup != null)
            {
                interactionPromptCanvasGroup.alpha = 0f;
                interactionPromptCanvasGroup.interactable = false;
                interactionPromptCanvasGroup.blocksRaycasts = false;
            }

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

            if (_isPlayerInZone && interactionPromptCanvasGroup != null)
            {
                interactionPromptCanvasGroup.alpha = 1f;
                interactionPromptCanvasGroup.interactable = true;
                interactionPromptCanvasGroup.blocksRaycasts = true;
            }
        }
    }
}