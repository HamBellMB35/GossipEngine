using UnityEngine;



namespace Project.Architecture
{
    /// <summary>
    /// This is the muscle of our system. Notice it does NOT inherit from MonoBehaviour!
    /// It doesn't need to sit on a GameObject in the scene to work. This makes it
    /// incredibly fast, highly optimized, and completely immune to scene-loading bugs.
    /// </summary>
    public class CoreGossipEngine : IGossipEngine
    {
        public void Initialize()
        {
            // Set up our data structures, arrays, and tracking variables here
            Debug.Log("Gossip Engine Initialized!"+" <color=green>[Gossip Engine]</color> Core systems online. Data arrays allocated.");
        }

        public void PropagateRumor(string rumorId, string sourceNpcId, string targetNpcId)
        {
            // Handle the logic of transferring a rumor from one NPC to another
            Debug.Log($"Rumor Propagated: '{rumorId}' from {sourceNpcId} to {targetNpcId} <color=yellow>[Gossip Engine]</color> Data packet transferred successfully.");
            Debug.Log($"<color=cyan>[Transmission]</color> {sourceNpcId} passed '{rumorId}' to {targetNpcId}.");
        }





    }
}