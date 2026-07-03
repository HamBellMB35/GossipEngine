using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;
using Project.Services;
using Project.GamePlay;
using Project.Data;

namespace Project.Testing
{
    // v3: G now calls LearnRumor (not LearnAndPresentRumor). Pressing G simulates "this NPC
    // has just learned about this rumor" and nothing more — actual presentation is now driven
    // entirely by the real trigger paths in NPCProximityGossip: AutoProximity rumors present
    // when the player enters the trigger zone, ManualTalk rumors present on [E]-press. This
    // lets you test the real end-to-end flow instead of a shortcut that bypasses it.
    public class GossipTester : MonoBehaviour
    {
        [Header("Target")]
        [Tooltip("The NPC to inject the test rumor into.")]
        [SerializeField] private NPCGossipMemory _targetNpc;

        [Header("Test Data")]
        [SerializeField] private RumorTemplate _rumorToTest;
        [Range(0f, 1f)]
        [SerializeField] private float _initialCredibility = 0.5f;

        [Header("Test Trigger")]
        [Tooltip("Press this key to load the test rumor into the Target NPC's memory.")]
        [SerializeField] private Key _testTriggerKey = Key.G;

        private GossipManager _gossipManager;

        [Inject]
        public void Construct(GossipManager gossipManager) => _gossipManager = gossipManager;

        private void Update()
        {
            if (Keyboard.current == null) return;

            if (Keyboard.current[_testTriggerKey].wasPressedThisFrame)
            {
                TriggerTestGossip();
            }
        }

        private void TriggerTestGossip()
        {
            if (_targetNpc == null)
            {
                Debug.LogWarning("<color=orange>[GossipTester]</color> No Target NPC assigned — nothing to load the rumor into.", this);
                return;
            }

            if (_rumorToTest == null)
            {
                Debug.LogWarning("<color=orange>[GossipTester]</color> No Rumor To Test assigned.", this);
                return;
            }

            // Just loads the rumor into memory — presentation happens via the real trigger
            // (proximity for Auto, [E] for Manual), not immediately here.
            _targetNpc.LearnRumor(_rumorToTest, _initialCredibility);

            // Existing hook kept for future propagation/reputation integration — currently just logs.
            int rumorHash = Animator.StringToHash(_rumorToTest.RumorID);
            _gossipManager?.UpdateRumor(rumorHash);

            Debug.Log($"<color=cyan>[GossipTester]</color> Loaded '{_rumorToTest.RumorID}' into '{_targetNpc.NpcName}''s memory (Credibility: {_initialCredibility}, TriggerMode: {_rumorToTest.TriggerMode}). It will present when the real trigger condition is met.");
        }
    }
}