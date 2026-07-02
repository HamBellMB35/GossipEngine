using UnityEngine;
using Project.Data;

namespace Project.Architecture
{
    // NOTE: This is the muscle of our system. Notice it does NOT inherit from MonoBehaviour!
    // It doesn't need to sit on a GameObject in the scene to work.
    public class CoreGossipEngine : IGossipEngine
    {
        public void Initialize()
        {
            // Set up our data structures, arrays, and tracking variables here
            Debug.Log("Gossip Engine Initialized!" + " <color=green>[Gossip Engine]</color> Core systems online. Data arrays allocated.");
        }

        public void PropagateRumor(RumorTemplate rumor, string sourceNpcId, string targetNpcId)
        {
            // Handle the logic of transferring a rumor from one NPC to another
            Debug.Log($"<color=cyan>[Transmission]</color> {sourceNpcId} passed '{rumor.RumorID}' to {targetNpcId}.");
        }
    }
}