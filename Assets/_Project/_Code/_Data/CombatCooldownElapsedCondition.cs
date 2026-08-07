using UnityEngine;
using TownsPeople.Services;

namespace TownsPeople.Data
{
    /// <summary>Default return condition — ends flocking/fleeing once EditableCooldownSeconds have passed since the player last engaged in combat (PlayerCombatState.NotifyCombatEngagement()).</summary>
    [CreateAssetMenu(fileName = "NewCombatCooldownElapsedCondition", menuName = "Project/Locomotion/Flock Return Conditions/Combat Cooldown Elapsed")]
    public class CombatCooldownElapsedCondition : FlockReturnCondition
    {
        [Tooltip("How long (seconds) since the player's last combat engagement before this NPC stops fleeing and returns to normal.")]
        [SerializeField] private float _cooldownSeconds = 8f;

        public override bool ShouldReturnToNormal(PlayerCombatState combatState, Transform npcTransform, float timeSpentFlocking)
        {
            if (combatState == null) return true;
            return combatState.TimeSinceLastCombatEngagement() >= _cooldownSeconds;
        }
    }
}