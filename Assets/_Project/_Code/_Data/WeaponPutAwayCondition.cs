using UnityEngine;
using TownsPeople.Services;

namespace TownsPeople.Data
{
    /// <summary>Default return condition — ends flocking/fleeing the instant the player's weapon is no longer drawn.</summary>
    [CreateAssetMenu(fileName = "NewWeaponPutAwayCondition", menuName = "TownsPeople Creator/Locomotion/Flock Return Conditions/Weapon Put Away")]
    public class WeaponPutAwayCondition : FlockReturnCondition
    {
        public override bool ShouldReturnToNormal(PlayerCombatState combatState, Transform npcTransform, float timeSpentFlocking)
        {
            return combatState == null || !combatState.IsWeaponDrawn;
        }
    }
}