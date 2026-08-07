using UnityEngine;
using TownsPeople.Services;

namespace TownsPeople.Data
{
    /// <summary>Default trigger — starts flocking/fleeing while the player is actively fighting enemies (PlayerCombatState.IsInCombat, driven by NotifyCombatEngagement() calls from your own combat code).</summary>
    [CreateAssetMenu(fileName = "NewPlayerInCombatTrigger", menuName = "Project/Locomotion/Flock Triggers/Player In Combat")]
    public class PlayerInCombatTrigger : FlockTriggerCondition
    {
        public override bool ShouldTrigger(PlayerCombatState combatState, Transform npcTransform)
        {
            return combatState != null && combatState.IsInCombat;
        }
    }
}