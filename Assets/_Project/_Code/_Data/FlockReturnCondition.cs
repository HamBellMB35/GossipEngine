using UnityEngine;
using TownsPeople.Services;

namespace TownsPeople.Data
{
    /// <summary>
    /// Abstract base for a condition that ends an NPC's Flocking/Fleeing behavior, returning it
    /// to whatever it was doing before. Create a new ScriptableObject subclass to add a custom
    /// return condition — NPCFlockingBehavior holds a list of these and checks each one every
    /// evaluation tick while currently flocking; ANY one returning true ends flocking.
    /// </summary>
    public abstract class FlockReturnCondition : ScriptableObject
    {
        /// <summary>Return true the instant this NPC should stop flocking/fleeing and resume normal behavior.</summary>
        public abstract bool ShouldReturnToNormal(PlayerCombatState combatState, Transform npcTransform, float timeSpentFlocking);
    }
}