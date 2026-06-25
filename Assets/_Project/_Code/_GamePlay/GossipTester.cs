using UnityEngine;
using Project.Data;

namespace Project.GamePlay
{
    /// <summary>
    /// A temporary development script to feed testing data into our live systems.
    /// </summary>
    public class GossipTester : MonoBehaviour
    {
        [Header("Testing Targets")]
        [SerializeField] private NPCGossipMemory _targetNPC;
        [SerializeField] private RumorTemplate _testRumor;
        [SerializeField] private GossipToneData _testTone;


        public void ExecuteTestInjection()
        {
            if (_targetNPC == null || _testRumor == null) return;

            Debug.Log("<color=orange>[Simulation Driver]</color> Simulating a rumor injection...");

            // First, we feed the rumor striaght into our NPCs custom memory bank

            _targetNPC.LearnRumor(_testRumor, _testRumor.BaseSpiciness);

            // If the character has our animation bridge attached, we tell them to react to the gossip
            if (_targetNPC.TryGetComponent<NPCAnimationBridge>(out var animationBridge) && _testTone != null)   
            {
                animationBridge.TransitionToTone(_testTone);
            }

        }
    }
}


