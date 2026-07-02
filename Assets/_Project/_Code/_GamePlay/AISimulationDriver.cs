using UnityEngine;
using System.Collections.Generic;
using Project.Data;

namespace Project.GamePlay
{
    public class AISimulationDriver : MonoBehaviour
    {
        [Header("Target Testing Node")]
        public NPCGossipMemory TargetNpc;

        [Header("Mock Rumor Asset Data")]
        public RumorTemplate TestRumorBlueprint;

        private System.Collections.IEnumerator Start()
        {
            if (TargetNpc == null || TestRumorBlueprint == null)
            {
                Debug.LogWarning("[Simulation Driver] Missing Target NPC or Test Rumor Blueprint!");
                yield break;
            }

            TargetNpc.LearnRumor(TestRumorBlueprint, 0.85f);

            yield return null;

            NPCProximityGossip proximityScript = TargetNpc.GetComponent<NPCProximityGossip>();

            if (proximityScript != null)
            {
                Debug.Log($"<color=green>[Simulated Event]</color> Triggering mock encounter for {TargetNpc.NpcName}...");

                GameObject fakePlayerObject = new GameObject("Fake_Player_Node");
                fakePlayerObject.tag = "Player";
                CapsuleCollider dummyCollider = fakePlayerObject.AddComponent<CapsuleCollider>();

                proximityScript.SendMessage("OnTriggerEnter", dummyCollider);

                Destroy(fakePlayerObject, 1f);
            }
        }
    }
}