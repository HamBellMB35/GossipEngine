using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Project.GamePlay
{
    // NOTE: This version explicitly respects the ConversationalBrainType.
    // If set to GenerativeAI, it bypasses static lists and null audio checks, 
    // seamlessly handing control over to your AI generation extensions.

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
            if (other.CompareTag("Player"))
            {
                _isPlayerInZone = true;
                if (!_isOnCooldown && interactionPromptCanvasGroup != null)
                {
                    interactionPromptCanvasGroup.alpha = 1f;
                    interactionPromptCanvasGroup.interactable = true;
                    interactionPromptCanvasGroup.blocksRaycasts = true;
                }
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                _isPlayerInZone = false;
                if (interactionPromptCanvasGroup != null)
                {
                    interactionPromptCanvasGroup.alpha = 0f;
                    interactionPromptCanvasGroup.interactable = false;
                    interactionPromptCanvasGroup.blocksRaycasts = false;
                }
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

            IInteractionExtension extension = GetComponent<IInteractionExtension>();
            if (extension != null && extension.OnExtendInteraction())
            {
                return;
            }

            ExecuteAmbientGreeting();
            StartCoroutine(InteractionCooldownSequence());
        }

        private void ExecuteAmbientGreeting()
        {
            if (archetypeConfig == null) return;

            // ====================================================================
            // --- BRANCH 1: GENERATIVE AI OVERRIDE ---
            // ====================================================================
            if (archetypeConfig.BrainStyle == Project.Data.ConversationalBrainType.GenerativeAI)
            {
                Debug.Log("<color=magenta>[NPC Brain]</color> Generative AI active! Bypassing offline static lists and audio clip requirements.");

                // Awaken the UI layout immediately to provide snappy player feedback
                if (speechBubble != null)
                {
                    speechBubble.DisplayText("...");
                }

                // Broadcast a system message to awaken your attached AI modules (like NPCGossipMemory)
                // so they can begin generating the TTS audio and injecting text into the bubble!
                SendMessage("OnGenerativeAIInteract", SendMessageOptions.DontRequireReceiver);

                return; // Exit immediately so it doesn't run the FixedScripted logic below!
            }

            // ====================================================================
            // --- BRANCH 2: FIXED OFFLINE SCRIPTED ---
            // ====================================================================
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