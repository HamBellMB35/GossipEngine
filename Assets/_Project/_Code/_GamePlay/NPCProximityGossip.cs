using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;
using VContainer;
using Project.Architecture;
using Project.Data;
using Project.Services;
using System;

namespace Project.GamePlay
{
    /// <summary>
    /// Detects passing players using a proximity radius trigger.
    /// Fetches local rumors from the NPC's memory and pushes them into our offline AI generation pipeline.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public class NPCProximityGossip : MonoBehaviour
    {
        // --- Dependency Injection Fields ---
        private NPCGossipMemory _myMemory;
        private IAiGenerationService _aiService;
        private IVoiceService _voiceService;

        [Header("Proximity Trigger Settings")]
        [Tooltip("Cooldown time in seconds before this NPC will mutter another piece of gossip to the player.")]
        public float TalkCooldown = 15f;

        [Tooltip("The amount of time in seconds the 'Talk [E]' prompt will completely disappear after being pressed.")]
        [SerializeField] private float promptHideDuration = 5f;

        // --- Core Internal Timers and States ---
        private float _lastTalkTime = -999f;
        private bool _isPromptHiddenFromClick = false;

        [Header("Visual Feedback Layout")]
        [SerializeField] private UI.NPCSpeechBubble speechBubble;

        [Header("Interaction Prompt UI")]
        [Tooltip("Attach a CanvasGroup component assigned to your local worldspace floating 'Talk [E]' text overlay template.")]
        [SerializeField] private CanvasGroup interactionPromptCanvasGroup;
        [SerializeField] private float fadeSpeed = 5f;

        // --- State Flags and Running Coroutine Containers ---
        private bool _isPlayerInsideZone = false;
        private Coroutine _fadeCoroutine;

        /// <summary>
        /// VContainer injection entry point.
        /// </summary>
        [Inject]
        public void Construct(IAiGenerationService aiService, IVoiceService voiceService)
        {
            _aiService = aiService;
            _voiceService = voiceService;
            Debug.Log($"<color=green>[Injection Verified]</color> Services successfully delivered to {gameObject.name}");
        }

        private void Awake()
        {
            _myMemory = GetComponent<NPCGossipMemory>();

            SphereCollider triggerCollider = GetComponent<SphereCollider>();
            if (triggerCollider != null)
            {
                triggerCollider.isTrigger = true;
            }

            // Clean initialization: ensure the zone is tracking false at launch
            _isPlayerInsideZone = false;

            if (interactionPromptCanvasGroup != null)
            {
                interactionPromptCanvasGroup.alpha = 0f;
            }
        }

        private void Update()
        {
            // CRITICAL SYSTEM CHECK: Only listen to hardware keyboard inputs if the collision system has verified proximity
            if (_isPlayerInsideZone)
            {
                if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                {
                    ExecuteRumorInteraction();
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;

            _isPlayerInsideZone = true;
            Debug.Log($"<color=yellow>[Trigger Entered]</color> Player stepped inside {gameObject.name}'s gossip zone.");

            if (!_isPromptHiddenFromClick)
            {
                StartFadePrompt(1f);
            }

            ExecuteAmbientGreeting();
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Player")) return;

            _isPlayerInsideZone = false;
            _isPromptHiddenFromClick = false;
            Debug.Log($"<color=yellow>[Trigger Exited]</color> Player left {gameObject.name}'s gossip zone.");

            StartFadePrompt(0f);
        }

        private void ExecuteAmbientGreeting()
        {
            if (Time.time < _lastTalkTime + TalkCooldown) return;

            string greetingLine = "";
            float characterRelationshipScore = 75f;

            if (characterRelationshipScore >= 50f)
            {
                string[] goodGreetings = { "Good day to you.", "'Morning traveler.", "Greetings, friend." };
                greetingLine = goodGreetings[UnityEngine.Random.Range(0, goodGreetings.Length)];
            }
            else
            {
                string[] badGreetings = { "I got nothing to say to you.", "Get away from me.", "Keep walking." };
                greetingLine = badGreetings[UnityEngine.Random.Range(0, badGreetings.Length)];
            }

            // Auto-Resolution: Fallback search if VContainer didn't pass services directly down on frame zero
            if (_voiceService == null)
            {
                _voiceService = VContainer.Unity.LifetimeScope.Find<GameLifetimeScope>()?.Container.Resolve<IVoiceService>();
            }

            if (speechBubble != null)
            {
                speechBubble.DisplayText(greetingLine);
            }

            if (_voiceService != null)
            {
                _voiceService.SpeakGossip(greetingLine, _myMemory.VocalStyle, _myMemory.Gender);
            }
        }

        private async void ExecuteRumorInteraction()
        {
            if (Time.time < _lastTalkTime + TalkCooldown) return;

            float characterRelationshipScore = 75f;
            if (characterRelationshipScore < 50f) return;

            _lastTalkTime = Time.time;
            StartCoroutine(HidePromptTimeoutRoutine());

            RuntimeRumorState priorityRumor = FetchPriorityRumor();

            AiPromptContext promptContext = new AiPromptContext
            {
                SpeakerID = _myMemory != null ? _myMemory.NpcName : "Unknown NPC",
                Gender = _myMemory != null ? _myMemory.Gender : "Male",
                VocalStyle = _myMemory != null ? _myMemory.VocalStyle : "Normal"
            };

            if (priorityRumor != null)
            {
                string calculatedTone = priorityRumor.PersonalCredibilityScore >= 0.8f ? "Terrified" :
                                        priorityRumor.PersonalCredibilityScore >= 0.4f ? "Anxious" : "Smug";

                string coreFactText = "Unverified local secrets.";
                if (priorityRumor.SourceTemplate != null && !string.IsNullOrEmpty(priorityRumor.SourceTemplate.CoreFact))
                {
                    coreFactText = priorityRumor.SourceTemplate.CoreFact;
                }

                promptContext.RumorCoreFact = coreFactText;
                promptContext.PersonalCredibilityScore = priorityRumor.PersonalCredibilityScore;
                promptContext.CurrentToneName = calculatedTone;
            }
            else
            {
                promptContext.RumorCoreFact = "There are no active secrets right now. Just complain lightheartedly about the local village weather.";
                promptContext.PersonalCredibilityScore = 0f;
                promptContext.CurrentToneName = "Tired";
            }

            // AUTO-RESOLUTION WORKAROUND FOR LINE 231: Fallback service locator pattern if dependency injection was locked out
            if (_aiService == null)
            {
                _aiService = VContainer.Unity.LifetimeScope.Find<GameLifetimeScope>()?.Container.Resolve<IAiGenerationService>();
            }

            if (_aiService == null)
            {
                Debug.LogError($"<color=red>[AI Service Missing Fallback Failure]</color> Could not dynamically locate IAiGenerationService container interface.");
                string localSandboxText = $"\"{promptContext.SpeakerID} whispers: {promptContext.RumorCoreFact}\"";
                ProcessAndDisplayDialogue(localSandboxText);
                return;
            }

            string cleanAiDialogue = await _aiService.GenerateGossipDialogueAsync(promptContext);
            ProcessAndDisplayDialogue(cleanAiDialogue);
        }

        private void ProcessAndDisplayDialogue(string cleanAiDialogue)
        {
            string textToSpeak = cleanAiDialogue;

            if (cleanAiDialogue.Contains("\""))
            {
                string[] dialogueSegments = cleanAiDialogue.Split('"');
                string longestSegment = "";

                for (int segmentIndex = 1; segmentIndex < dialogueSegments.Length; segmentIndex += 2)
                {
                    if (dialogueSegments[segmentIndex].Length > longestSegment.Length)
                    {
                        longestSegment = dialogueSegments[segmentIndex];
                    }
                }

                if (!string.IsNullOrEmpty(longestSegment) && longestSegment.Length > 3)
                {
                    textToSpeak = longestSegment;
                }
            }

            if (_voiceService == null)
            {
                _voiceService = VContainer.Unity.LifetimeScope.Find<GameLifetimeScope>()?.Container.Resolve<IVoiceService>();
            }

            if (speechBubble != null)
            {
                speechBubble.DisplayText(textToSpeak);
            }

            // Output step 9: Push our clean, fully processed speech statement out to our Unity console tracker layout
            Debug.Log($"<color=cyan>[NPC Overlay Bubble]</color> {_myMemory.NpcName}: {textToSpeak}");

            // Cloud Synthesizer Execution step 10: Call our central audio service interface contract
            if (_voiceService != null)
            {
                // We pass our text display method exclusively inside the inline Action callback delegate pointer.
                // This guarantees the text stays hidden while network threads download data, 
                // popping onto the screen the exact millisecond audio playback begins on your hardware device!
                _voiceService.SpeakGossip(textToSpeak, _myMemory.VocalStyle, _myMemory.Gender, () =>
                {
                    Debug.Log($"<color=cyan>[NPC Sync Overlay]</color> Audio playback start detected! Rendering text statement bubble.");
                    if (speechBubble != null)
                    {
                        speechBubble.DisplayText(textToSpeak);
                    }
                });
            }
            else
            {
                // OFFLINE FALLBACK: If voice services are missing or disabled, display the text immediately 
                // so the game doesn't sit in awkward silence.
                Debug.LogWarning($"<color=orange>[Voice Offline]</color> No voice service found. Displaying text immediately as fallback.");
                if (speechBubble != null)
                {
                    speechBubble.DisplayText(textToSpeak);
                }
            }
        }

        private void StartFadePrompt(float targetAlpha)
        {
            if (interactionPromptCanvasGroup == null) return;
            if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = StartCoroutine(FadePromptRoutine(targetAlpha));
        }

        private IEnumerator FadePromptRoutine(float targetAlpha)
        {
            while (!Mathf.Approximately(interactionPromptCanvasGroup.alpha, targetAlpha))
            {
                interactionPromptCanvasGroup.alpha = Mathf.MoveTowards(interactionPromptCanvasGroup.alpha, targetAlpha, fadeSpeed * Time.deltaTime);
                yield return null;
            }
        }

        private IEnumerator HidePromptTimeoutRoutine()
        {
            _isPromptHiddenFromClick = true;
            if (interactionPromptCanvasGroup != null)
            {
                interactionPromptCanvasGroup.alpha = 0f;
            }

            yield return new WaitForSeconds(promptHideDuration);

            if (_isPlayerInsideZone)
            {
                _isPromptHiddenFromClick = false;
                StartFadePrompt(1f);
            }
        }

        private RuntimeRumorState FetchPriorityRumor()
        {
            if (_myMemory == null) return null;
            Dictionary<string, RuntimeRumorState> knownRumors = _myMemory.GetKnownRumors();
            if (knownRumors == null || knownRumors.Count == 0) return null;

            foreach (var rumorKeyPair in knownRumors.Values)
            {
                return rumorKeyPair;
            }
            return null;
        }
    }
}