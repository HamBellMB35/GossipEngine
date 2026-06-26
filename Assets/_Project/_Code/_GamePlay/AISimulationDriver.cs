using UnityEngine;
using System.Collections.Generic;
using Project.Data;

namespace Project.GamePlay
{
    /// <summary>
    /// Development driver that automatically injects a rumor into an NPC's memory
    /// and triggers a mock proximity event so we can test without a player character.
    /// </summary>
    public class AISimulationDriver : MonoBehaviour
    {
        [Header("Target Testing Node")]
        public NPCGossipMemory TargetNpc; // Drop Villager_01 here

        [Header("Mock Rumor Asset Data")]
        public RumorTemplate TestRumorBlueprint; // Drop your Rumor ScriptableObject here
        // CHANGED: Swapped Start to an IEnumerator coroutine to manage timing
        private System.Collections.IEnumerator Start()
        {
            if (TargetNpc == null || TestRumorBlueprint == null)
            {
                Debug.LogWarning("[Simulation Driver] Missing Target NPC or Test Rumor Blueprint!");
                yield break;
            }

            // 1. Seed our rumor asset straight into Villager_01's memory dictionary
            TargetNpc.LearnRumor(TestRumorBlueprint, 0.85f);

            // 2. Wait exactly one frame for the dictionary entries to settle
            yield return null;

            // 3. Fetch the proximity script attached to Villager_01
            NPCProximityGossip proximityScript = TargetNpc.GetComponent<NPCProximityGossip>();

            if (proximityScript != null)
            {
                Debug.Log($"<color=green>[Simulated Event]</color> Triggering mock encounter for {TargetNpc.NpcName}...");

                GameObject fakePlayerObject = new GameObject("Fake_Player_Node");
                fakePlayerObject.tag = "Player";
                CapsuleCollider dummyCollider = fakePlayerObject.AddComponent<CapsuleCollider>();

                // 4. Force trigger the proximity loop method safely
                proximityScript.SendMessage("OnTriggerEnter", dummyCollider);

                Destroy(fakePlayerObject, 1f);
            }
        }
    }
}
