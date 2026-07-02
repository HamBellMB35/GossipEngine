using UnityEngine;
using UnityEngine.InputSystem;
using Project.Data;

namespace Project.GamePlay
{
    public class GossipTester : MonoBehaviour
    {
        [Header("Testing Targets")]
        public NPCAnimationBridge TargetNPC;
        public RumorTemplate TestRumor;

        [Header("Developer Controls")]
        public Key InjectHotkey = Key.F5;

        private void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null)
            {
                // This log will prove if your F5 press is being detected
                if (kb[InjectHotkey].wasPressedThisFrame)
                {
                    Debug.Log("<color=magenta>[GossipTester]</color> F5 detected! Attempting injection...");
                    InjectTestRumor();
                }
            }
        }

        public void InjectTestRumor()
        {
            if (TargetNPC == null)
            {
                Debug.LogError("<color=red>[CRITICAL ERROR]</color> GossipTester: TargetNPC is NOT assigned in the Inspector!");
                return;
            }
            if (TestRumor == null)
            {
                Debug.LogError("<color=red>[CRITICAL ERROR]</color> GossipTester: TestRumor is NOT assigned in the Inspector!");
                return;
            }

            Debug.Log($"<color=cyan>[Gossip Tester]</color> Injecting Rumor: {TestRumor.RumorID}.");
            TargetNPC.UpdateRumor(TestRumor);
        }
    }
}