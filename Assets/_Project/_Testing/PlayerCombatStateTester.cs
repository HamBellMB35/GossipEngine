using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;
using TownsPeople.Services;

namespace TownsPeople.Testing
{
    /// <summary>
    /// Manual test harness for PlayerCombatState, since a real player weapon/combat system
    /// doesn't exist in this project yet. Also sets ITSELF as PlayerCombatState.PlayerTransform
    /// — put this on a temporary GameObject you can freely move around the scene, and any
    /// currently-fleeing NPC (NPCFlockingBehavior) will flee from wherever THIS object is,
    /// standing in for a real player position until one exists.
    ///
    /// Delete this once a real player controller exists and wires SetWeaponDrawn()/
    /// NotifyCombatEngagement()/SetPlayerTransform() into PlayerCombatState itself.
    /// </summary>
    public class PlayerCombatStateTester : MonoBehaviour
    {
        [Header("Weapon Drawn Toggle")]
        [Tooltip("Press to toggle PlayerCombatState.IsWeaponDrawn on/off.")]
        [SerializeField] private Key _toggleWeaponKey = Key.F;

        [Header("Combat Engagement")]
        [Tooltip("Press to call PlayerCombatState.NotifyCombatEngagement() — simulates 'the player just engaged in combat' for the IsInCombat trigger and the Combat Cooldown Elapsed return condition.")]
        [SerializeField] private Key _triggerCombatKey = Key.C;

        private PlayerCombatState _combatState;
        private bool _isWeaponDrawn;

        [Inject]
        public void Construct(PlayerCombatState combatState)
        {
            _combatState = combatState;
            // Stand-in player position — move this GameObject around the Scene view in Play
            // mode to simulate the player's position for flee-direction purposes.
            _combatState.SetPlayerTransform(transform);
        }

        private void Update()
        {
            if (Keyboard.current == null || _combatState == null) return;

            if (Keyboard.current[_toggleWeaponKey].wasPressedThisFrame)
            {
                _isWeaponDrawn = !_isWeaponDrawn;
                _combatState.SetWeaponDrawn(_isWeaponDrawn);
                Debug.Log($"<color=magenta>[PlayerCombatStateTester]</color> Weapon Drawn: {_isWeaponDrawn}");
            }

            if (Keyboard.current[_triggerCombatKey].wasPressedThisFrame)
            {
                _combatState.NotifyCombatEngagement();
                Debug.Log($"<color=magenta>[PlayerCombatStateTester]</color> Combat engagement triggered. IsInCombat: {_combatState.IsInCombat}");
            }
        }
    }
}