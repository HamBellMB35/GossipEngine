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
        [Header("NPC Profile")]
        [SerializeField] private string _npcID;
        public string NPCID => _npcID;

        // Our memory directory: Key is the rumorID string, Values is the personal tracking state of that rumor for this specific NPC.
        private readonly Dictionary<string, RuntimeRumoreState> _memoryDatabase = new Dictionary<string, RuntimeRumoreState>();

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

        /// <summary>
        /// Call this when an NPC learns a rumor from another character or witnesses an event.
        /// </summary>
        
        public void LearnRumor(RumorTemplate rumorTemplate, float incomingCredibility)
        {
            if (rumorTemplate == null) return;

            string id = rumorTemplate.RumorID;

            // Scenario A: This is brand new information for this NPC
            if(!_memoryDatabase.ContainsKey(id))
            {
                // We crete a fresh memory for this rumor and add it in to our dictionary
                RuntimeRumoreState freshMemory = new RuntimeRumoreState(rumorTemplate, incomingCredibility);
                _memoryDatabase.Add(id, freshMemory);

                Debug.Log($"<color=yellow>[Memory Add]</color> NPC '{_npcID}' heard something new: {id} (Credibility: {incomingCredibility})");
            }

            // Scenario B: This NPC has already heard this rumor before, so we update their tracking statistics
            else
            {
                // If the source is huhgly believable, we can increase the NPC's belief in it. 
                _memoryDatabase[id].PersonalCredibility = Mathf.Max(_memoryDatabase[id].PersonalCredibility , incomingCredibility);
                _memoryDatabase[id].LastInteractionTime = System.DateTime.Now;

                Debug.Log($"<color=cyan>[Memory Update]</color> NPC '{_npcID}' heard reinforcement about rumor: {id}. Belief updated to: {_memoryDatabase[id].PersonalCredibility}");
            }


        }

        /// <summary>
        /// Public check to see if an NPC knows a specific rumor profile.
        /// </summary>
        public bool KnowsRumor(string rumorId) => _memoryDatabase.ContainsKey(rumorId);

    }
}
