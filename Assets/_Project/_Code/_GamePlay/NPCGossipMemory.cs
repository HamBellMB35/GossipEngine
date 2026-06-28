using System.Collections.Generic;
using UnityEngine;
using VContainer;
using Project.Architecture;     // Gives access to the Core Gossip Engine Contract
using Project.Data;             // Gives access to our data states

// NOTE: Now we need to create the actual memory storage component that will sit directly on your character GameObjects.
// It will use a C# Dictionary to index their known rumors by their ID string for fast lookups.

namespace Project.GamePlay
{
    /// <summary>
    /// This component represents the personal brain and memory storage of a single NPC.
    /// </summary>
    public class NPCGossipMemory : MonoBehaviour
    {
        [Header("NPC Profile Identity")]
        public string NpcName = "NPC_Villager_1";
        public string Gender = "Male"; // e.g. Male, Female
        [TextArea(2, 4)]
        public string VocalStyle = "Raspy elder, speaks in anxious whispers";

        // NEW: Drag and drop your pre-made rumor ScriptableObjects directly here in the editor!
        [Header("Starting Rumor Inventory")]
        [Tooltip("Drop your pre-made RumorTemplate assets into this list via the Inspector.")]
        public List<RumorTemplate> StartingRumors = new List<RumorTemplate>();

        [Header("NPC Profile")]
        [SerializeField] private string _npcID;
        public string NPCID => _npcID;

        // Our memory directory: Key is the rumorID string, Values is the personal tracking state of that rumor for this specific NPC.
        private readonly Dictionary<string, RuntimeRumorState> _knownRumors = new Dictionary<string, RuntimeRumorState>();

        // Our DI (Dependency Injection) reference to the central engine ( automatically delivered by Vcontainer )
        private IGossipEngine _gossipEngine;

        /// <sumary>
        /// Injection hook. VContainer looks fo this attribute and atomatically drops our shared central engine reference right here at setup
        /// </sumary>
        [Inject]
        public void Construct(IGossipEngine gossipEngine)
        {
            _gossipEngine = gossipEngine;
            Debug.Log($"<color=green>[NPC Memory]</color> {gameObject.name} loaded into the social matrix as ID: {_npcID}");
        }

        private void Start()
        {
            // Dynamic Initialization: Loop through all pre-made rumor assets dropped into the inspector list
            if (StartingRumors != null && StartingRumors.Count > 0)
            {
                foreach (RumorTemplate rumorAsset in StartingRumors)
                {
                    if (rumorAsset == null) continue;

                    // Dynamically set a high baseline test credibility score if the memory is empty, matching your architecture
                    float defaultTestCredibility = 0.85f;

                    // Pass the real asset and your credibility data into your strict constructor signature
                    RuntimeRumorState dynamicMemory = new RuntimeRumorState(rumorAsset, defaultTestCredibility);

                    // Add it directly to your dictionary under its unique asset ID
                    if (!_knownRumors.ContainsKey(rumorAsset.RumorID))
                    {
                        _knownRumors.Add(rumorAsset.RumorID, dynamicMemory);
                        Debug.Log($"<color=green>[Dynamic Memory Load]</color> {NpcName} successfully loaded asset rumor: '{rumorAsset.RumorID}' into memory storage.");
                    }
                }
            }
        }



        public void LearnRumor(RumorTemplate rumorTemplate, float incomingCredibility)
        {
            if (rumorTemplate == null) return;

            string id = rumorTemplate.RumorID;

            // Scenario A: This is brand new information for this NPC
            if(!_knownRumors.ContainsKey(id))
            {
                // We crete a fresh memory for this rumor and add it in to our dictionary
                RuntimeRumorState freshMemory = new RuntimeRumorState(rumorTemplate, incomingCredibility);
                _knownRumors.Add(id, freshMemory);

                Debug.Log($"<color=yellow>[Memory Add]</color> NPC '{_npcID}' heard something new: {id} (Credibility: {incomingCredibility})");
            }

            // Scenario B: This NPC has already heard this rumor before, so we update their tracking statistics
            else
            {
                // If the source is huhgly believable, we can increase the NPC's belief in it. 
                _knownRumors[id].PersonalCredibilityScore = Mathf.Max(_knownRumors[id].PersonalCredibilityScore , incomingCredibility);
                _knownRumors[id].LastInteractionTime = System.DateTime.Now;

                Debug.Log($"<color=cyan>[Memory Update]</color> NPC '{_npcID}' heard reinforcement about rumor: {id}. Belief updated to: {_knownRumors[id].PersonalCredibilityScore}");
            }


        }

        /// <summary>
        /// Public utility lookup searching internal memory files to fetch a single runtime rumor element.
        /// </summary>
        public RuntimeRumorState GetRumorState(string rumorId)
        {
            if (string.IsNullOrEmpty(rumorId)) return null;

            _knownRumors.TryGetValue(rumorId, out RuntimeRumorState state);
            return state;
        }

        /// <summary>
        /// Public getter that safely exposes the entire dictionary of known runtime rumors 
        /// so systems like proximity triggers can evaluate them.
        /// </summary>
        public Dictionary<string, RuntimeRumorState> GetKnownRumors()
        {
            return _knownRumors;
        }
    }
}
