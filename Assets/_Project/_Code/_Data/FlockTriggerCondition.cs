using UnityEngine;
using TownsPeople.Services;

namespace TownsPeople.Data
{
    /// <summary>
    /// Abstract base for a condition that starts an NPC's Flocking/Fleeing behavior. Create a
    /// new ScriptableObject subclass to add a custom trigger — nothing else needs to change,
    /// NPCFlockingBehavior just holds a list of these and checks each one every evaluation tick.
    /// Same data-driven pattern as RumorTemplate/GossipToneData elsewhere in this project.
    /// </summary>
    public abstract class FlockTriggerCondition : ScriptableObject
    {
        /// <summary>Return true the instant this NPC should start flocking/fleeing.</summary>
        public abstract bool ShouldTrigger(PlayerCombatState combatState, Transform npcTransform);
    }
}