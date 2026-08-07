using UnityEngine;
using TownsPeople.Services;

namespace TownsPeople.Data
{
    /// <summary>Default trigger — starts flocking/fleeing the instant the player has a weapon drawn (PlayerCombatState.SetWeaponDrawn(true)).</summary>
    [CreateAssetMenu(fileName = "NewPlayerWeaponDrawnTrigger", menuName = "Project/Locomotion/Flock Triggers/Player Weapon Drawn")]
    public class PlayerWeaponDrawnTrigger : FlockTriggerCondition
    {
        public override bool ShouldTrigger(PlayerCombatState combatState, Transform npcTransform)
        {
            return combatState != null && combatState.IsWeaponDrawn;
        }
    }
}