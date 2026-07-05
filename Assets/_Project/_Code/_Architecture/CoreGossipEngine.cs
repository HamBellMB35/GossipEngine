using UnityEngine;
using Project.Data;
using Project.GamePlay;

namespace Project.Architecture
{
    // NOTE: This is the muscle of our system. Notice it does NOT inherit from MonoBehaviour!
    // It doesn't need to sit on a GameObject in the scene to work.
    //
    // v2: RunPropagationTick() implemented. This is distance-agnostic on purpose — proximity
    // only gates the WITNESS step (see PlayerDeedBroadcaster), not ongoing NPC-to-NPC spread.
    // A rumor can hop across the map through this tick loop without the two NPCs ever being
    // near each other, same as real gossip.
    public class CoreGossipEngine : IGossipEngine
    {
        public void Initialize()
        {
            Debug.Log("Gossip Engine Initialized!" + " <color=green>[Gossip Engine]</color> Core systems online. Data arrays allocated.");
        }

        public void PropagateRumor(RumorTemplate rumor, string sourceNpcId, string targetNpcId)
        {
            Debug.Log($"<color=cyan>[Transmission]</color> {sourceNpcId} passed '{rumor.RumorID}' to {targetNpcId}.");
        }

        public void RunPropagationTick()
        {
            NPCGossipMemory[] allNpcs = Object.FindObjectsByType<NPCGossipMemory>(
                FindObjectsInactive.Exclude);

            if (allNpcs.Length < 2) return; // Nobody to gossip with.

            foreach (NPCGossipMemory speaker in allNpcs)
            {
                // Iterate a snapshot since LearnRumor on a listener could theoretically be
                // re-entrant with future systems — safer to copy first.
                RuntimeRumorState[] knownRumors = speaker.KnownRumors.ToArray();

                foreach (RuntimeRumorState knownState in knownRumors)
                {
                    RumorTemplate rumor = knownState.SourceTemplate;
                    if (rumor == null) continue;

                    foreach (NPCGossipMemory listener in allNpcs)
                    {
                        if (listener == speaker) continue;
                        if (listener.KnowsRumor(rumor.RumorID)) continue;

                        int roll = Random.Range(0, 100);
                        if (roll >= rumor.ShareLikelihood) continue;

                        // Hearsay: lower credibility, no personal witness reaction, no repeat
                        // reputation change (that already happened once, at the deed itself).
                        listener.LearnRumor(rumor, rumor.HearsayCredibility);
                        PropagateRumor(rumor, speaker.NpcName, listener.NpcName);
                    }
                }
            }
        }
    }
}