using System.Collections.Generic;
using TownsPeople.Data;
using UnityEngine;

namespace TownsPeople.Data
{
    /// <summary>
    /// Shared, game-wide pools of generic reactions, used once a rumor's own SpecificResponses
    /// are exhausted, and by NPCGreetingResponder for its default greeting. Which pool gets
    /// used is decided by the PLAYER'S CURRENT REPUTATION as seen by the reacting NPC — not by
    /// the triggering rumor's own Alignment.
    /// </summary>
    [CreateAssetMenu(fileName = "NewGeneralRumorResponseLibrary", menuName = "Project/Gossip/General Response Library")]
    public class GeneralRumorResponseLibrary : ScriptableObject
    {
        [Tooltip("If disabled, text from THIS library is never shown in the speech bubble — covers both the greeting (PlayGreeting) and the General-pool fallback tier inside PresentRumor. Audio and animation are unaffected. Rumor-specific text (RumorTemplate.ShowTextBubble) is controlled separately.")]
        public bool ShowTextBubble = true;

        [Tooltip("Used when the reacting NPC currently views the player favorably.")]
        public List<RumorResponse> PositiveResponses = new List<RumorResponse>();

        [Tooltip("Used when the reacting NPC currently views the player unfavorably.")]
        public List<RumorResponse> NegativeResponses = new List<RumorResponse>();

        /// <summary>
        /// Returns a random response from the requested pool, or null if that pool is empty.
        /// Pure random — does not avoid repeats. Use the ref-int overload below if you want to
        /// avoid picking the same entry twice in a row for a given NPC.
        /// </summary>
        public RumorResponse? GetRandomResponse(RumorAlignment poolToUse)
        {
            List<RumorResponse> pool = poolToUse == RumorAlignment.Positive ? PositiveResponses : NegativeResponses;

            if (pool == null || pool.Count == 0) return null;

            return pool[Random.Range(0, pool.Count)];
        }

        /// <summary>
        /// Returns a random response from the requested pool, avoiding the entry at
        /// lastUsedIndex if the pool has more than one entry. lastUsedIndex is caller-owned
        /// (e.g. a field on the NPC calling this) — this library stays stateless itself, since
        /// "what did THIS NPC say last" must be tracked per-NPC, not shared game-wide.
        /// </summary>
        public RumorResponse? GetRandomResponse(RumorAlignment poolToUse, ref int lastUsedIndex)
        {
            List<RumorResponse> pool = poolToUse == RumorAlignment.Positive ? PositiveResponses : NegativeResponses;

            if (pool == null || pool.Count == 0)
            {
                lastUsedIndex = -1;
                return null;
            }

            if (pool.Count == 1)
            {
                // Only one option exists — can't avoid a "repeat" without going silent instead.
                lastUsedIndex = 0;
                return pool[0];
            }

            int newIndex;
            do
            {
                newIndex = Random.Range(0, pool.Count);
            }
            while (newIndex == lastUsedIndex);

            lastUsedIndex = newIndex;
            return pool[newIndex];
        }
    }
}