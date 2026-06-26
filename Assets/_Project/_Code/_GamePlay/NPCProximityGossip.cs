using UnityEngine;
using System.Collections.Generic;
using VContainer;
using Project.Architecture;
using Project.Data;
using static UnityEngine.Audio.ProcessorInstance;
using System.Threading;
using Unity.VisualScripting;

namespace Project.GamePlay
{
    // NOTE: Now we create a specialized, highly decoupled component called NpcProximityGossip.
    // This script will sit on a NPC GameObject alongside a Sphere Collider set to Is Trigger.
    // When the player walks within range, this component handles fetching a rumor from the NPC's
    // memory and sending it off to the local AI service.

    /// <summary>
    /// Detects passing players using a proximity radius trigger.
    /// Fetches local rumors from the NPC's memory and pushes them into our offline AI generation pipeline.
    /// </summary>

    [RequireComponent(typeof(SphereCollider))]
    public class NPCProximityGossip : MonoBehaviour
    {

        private NPCGossipMemory _myMemory;
        private IAiGenerationService _aiService;

        [Header("Proximity Trigger Settings")]
        [Tooltip("Cooldown time in seconds before this NPC will mutter another piece of gossip to the player.")]
        public float TalkCooldown = 15f;
        private float _lastTalkTime = -999f;

        /// <summary>
        /// VContainer automatically delivers our localized AI service straight to this character on frame-zero.
        /// </summary>

        [Inject]
        public void Construct(IAiGenerationService aiService)
        {
            _aiService = aiService;
        }

        private void Awake()
        {
            _myMemory = GetComponent<NPCGossipMemory>();

            // We enforce a hard physical trigger zone set up automatically 
            SphereCollider triggerCollider = GetComponent<SphereCollider>();
            triggerCollider.isTrigger = true;
        }

        /// <summary>
        /// Physics engine hook detecting when objects cross into our character's immediate personal space.
        /// </summary>

        private async void OnTriggerEnter(Collider other)
        {


            // 1. Check if the object walking past is labeled as our Player
            if (!other.CompareTag("Player")) return;

            // 2. Prevent continuous dialogue spamming using our cooldown timer check
            if (Time.time < _lastTalkTime + TalkCooldown) return;

            // 3. Look up highest priority rumor currently floating inside this character's dictionary memory
            RuntimeRumorState priorityRumor = FetchPriorityRumor();
            // ADD THIS CRITICAL SAFETY CHECK RIGHT HERE:
            if (priorityRumor == null)
            {
                Debug.Log($"<color=orange>[Proximity System]</color> {_myMemory.NpcName} noticed the Player, but has no rumors in memory to talk about yet.");
                return; // Stops the function safely before line 86 can crash!
            }

            _lastTalkTime = Time.time;
            Debug.Log($"<color=yellow>[Proximity Trigger]</color> {_myMemory.NpcName} noticed the Player! Generating custom AI dialogue line...");

            /// Create our isolated profile transaction details payload to pass downstream
            AiPromptContext promptContext = new AiPromptContext
            {
                SpeakerID = _myMemory.NpcName,
                Gender = _myMemory.Gender,
                VocalStyle = _myMemory.VocalStyle,
                RumorCoreFact = priorityRumor.SourceTemplate != null ? priorityRumor.SourceTemplate.CoreFact : "Unverified secrets",
                PersonalCredibilityScore = priorityRumor.PersonalCredibilityScore,
                CurrentToneName = "Anxious"
            };

            // 4. Fire the async task over our VContainer-injected AI network service client
            // This safely contacts our local Ollama endpoint, extracts the string, and strips the JSON metadata
            string cleanAiDialogue = await _aiService.GenerateGossipDialogueAsync(promptContext);

            // 5. Output our beautiful, isolated character whisper line directly to the tracking log
            Debug.Log($"<color=cyan>[NPC Overlay Bubble]</color> {_myMemory.NpcName}: {cleanAiDialogue}");
        }

        /// <summary>
        /// Helper utility searching internal dictionary records to pick out an interesting rumor to spread.
        /// </summary>
        private RuntimeRumorState FetchPriorityRumor()
        {
            Dictionary<string, RuntimeRumorState> knownRumors = _myMemory.GetKnownRumors();
            if (knownRumors == null || knownRumors.Count == 0) return null;

            // For now, let's grab the first rumor token we successfully track down inside our brain storage
            foreach (var rumorKeyPair in knownRumors.Values)
            {
                return rumorKeyPair;
            }
            return null;
        }






    }
}
