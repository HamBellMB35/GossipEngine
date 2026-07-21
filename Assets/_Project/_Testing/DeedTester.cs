using UnityEngine;
using UnityEngine.InputSystem;
using TownsPeople.GamePlay;
using TownsPeople.Data;


namespace TownsPeople.Testing
{
    /// <summary>
    /// Manual test harness for the witness step of the Gossip Propagation Engine. Simulates
    /// "the player just did this deed" without needing real gameplay actions (theft, combat,
    /// quest completion, etc.) wired up yet.
    /// </summary>
    public class DeedTester : MonoBehaviour
    {
        [Tooltip("The PlayerDeedBroadcaster to trigger (should be on the Player).")]
        [SerializeField] private PlayerDeedBroadcaster _deedBroadcaster;

        [Tooltip("The deed to simulate. Its reputation impact fields and RumorID drive what happens.")]
        [SerializeField] private RumorTemplate _deedToTest;

        [SerializeField] private Key _testTriggerKey = Key.B;

        private void Update()
        {
            if (Keyboard.current == null) return;

            if (Keyboard.current[_testTriggerKey].wasPressedThisFrame)
            {
                TriggerTestDeed();
            }
        }

        private void TriggerTestDeed()
        {
            if (_deedBroadcaster == null)
            {
                Debug.LogWarning("<color=orange>[DeedTester]</color> No PlayerDeedBroadcaster assigned.", this);
                return;
            }

            if (_deedToTest == null)
            {
                Debug.LogWarning("<color=orange>[DeedTester]</color> No Deed To Test assigned.", this);
                return;
            }

            _deedBroadcaster.BroadcastDeed(_deedToTest);
        }
    }
}