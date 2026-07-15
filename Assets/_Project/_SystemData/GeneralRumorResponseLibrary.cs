using System.Collections.Generic;
using UnityEngine;

namespace Project.Data
{
    /// <summary>
    /// Shared, game-wide pools of generic reactions, used once a rumor's own SpecificResponses
    /// are exhausted. Which pool gets used is decided by the PLAYER'S CURRENT REPUTATION as
    /// seen by the reacting NPC — not by the triggering rumor's own Alignment. This is what
    /// gives NPCs a generic "ugh, you again" vs "oh, lovely to see you!" reaction once specific
    /// gossip content has gone stale.
    /// </summary>
    [CreateAssetMenu(fileName = "NewGeneralRumorResponseLibrary", menuName = "Project/Gossip/General Response Library")]
    public class GeneralRumorResponseLibrary : ScriptableObject
    {
        [Tooltip("Used when the reacting NPC currently views the player favorably.")]
        public List<RumorResponse> PositiveResponses = new List<RumorResponse>();

        [Tooltip("Used when the reacting NPC currently views the player unfavorably.")]
        public List<RumorResponse> NegativeResponses = new List<RumorResponse>();

        /// <summary>
        /// Returns a random response from the requested pool, or null if that pool is empty.
        /// </summary>
        public RumorResponse? GetRandomResponse(RumorAlignment poolToUse)
        {
            List<RumorResponse> pool = poolToUse == RumorAlignment.Positive ? PositiveResponses : NegativeResponses;

            if (pool == null || pool.Count == 0) return null;

            return pool[Random.Range(0, pool.Count)];
        }
    }
}