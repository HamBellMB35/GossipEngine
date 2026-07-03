using UnityEngine;

namespace Project.Services
{
    public class GossipManager
    {
        // This is the method your GossipTester is looking for
        public void UpdateRumor(int rumorId)
        {
            Debug.Log($"<color=blue>[Gossip]</color> Rumor {rumorId} processed.");
        }
    }
}
