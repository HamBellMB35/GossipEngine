using System.Collections.Generic;
using UnityEngine;
using Project.Data;

namespace Project.Services
{
    // v2: Added real functionality. Tracks, per RumorID, how many times a Specific Response
    // has been used ACROSS ALL NPCs (not per-NPC) — this is what makes a rumor's unique
    // reactions eventually "go stale" and fall back to the general Positive/Negative pool.
    public class GossipManager
    {
        private readonly Dictionary<string, int> _specificResponseUsageCounts = new Dictionary<string, int>();

        // Kept for backward compatibility with existing callers (e.g. GossipTester).
        public void UpdateRumor(int rumorId)
        {
            Debug.Log($"<color=blue>[Gossip]</color> Rumor {rumorId} processed.");
        }

        /// <summary>
        /// v3: Non-consuming version of GetSpecificResponse — reads what WOULD be returned
        /// without advancing the shared usage counter. Used for preview purposes (e.g. the
        /// dialogue menu's rumor list labels), where actually consuming a specific-response
        /// slot just for a preview would unfairly burn through the shared quota before the
        /// player ever clicks anything.
        /// </summary>
        public RumorResponse? PeekSpecificResponse(RumorTemplate rumor)
        {
            if (rumor == null || rumor.SpecificResponses == null || rumor.SpecificResponses.Count == 0)
            {
                return null;
            }

            int currentCount = _specificResponseUsageCounts.TryGetValue(rumor.RumorID, out int existing) ? existing : 0;

            if (currentCount >= rumor.SpecificResponseUsageLimit)
            {
                return null;
            }

            return rumor.SpecificResponses[currentCount % rumor.SpecificResponses.Count];
        }

        /// <summary>
        /// Returns the next Specific Response to use for this rumor (rotating through its
        /// list, not repeating the same one back to back), or null if the rumor's
        /// SpecificResponseUsageLimit has already been reached — signaling the caller to fall
        /// back to the general Positive/Negative pool instead. Increments the shared usage
        /// counter for this rumor each time a specific response is actually used.
        /// </summary>
        public RumorResponse? GetSpecificResponse(RumorTemplate rumor)
        {
            if (rumor == null || rumor.SpecificResponses == null || rumor.SpecificResponses.Count == 0)
            {
                return null;
            }

            int currentCount = _specificResponseUsageCounts.TryGetValue(rumor.RumorID, out int existing) ? existing : 0;

            if (currentCount >= rumor.SpecificResponseUsageLimit)
            {
                return null;
            }

            RumorResponse response = rumor.SpecificResponses[currentCount % rumor.SpecificResponses.Count];
            _specificResponseUsageCounts[rumor.RumorID] = currentCount + 1;

            return response;
        }
    }
}