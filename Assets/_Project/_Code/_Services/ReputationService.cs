using System;
using System.Collections.Generic;
using UnityEngine;
using Project.Data;

namespace Project.Services
{
    /// <summary>
    /// The single source of truth for the player's reputation. Registered once as a
    /// VContainer singleton (see GameLifetimeScope) — every system that needs to know
    /// "how does the world feel about the player" queries this same shared instance.
    ///
    /// This tracks the OBJECTIVE state of the player's standing: it should change once per
    /// deed, not once per NPC that eventually hears about that deed. Per-NPC personal
    /// reactions (a witness feeling more strongly than someone who heard secondhand) belong
    /// on NPCReputationOpinion instead, not here.
    /// </summary>
    // v2: Replaced the empty placeholder with a real implementation.
    public class ReputationService
    {
        private const float MinReputation = -100f;
        private const float MaxReputation = 100f;

        private float _generalReputation = 0f;
        private readonly Dictionary<string, float> _factionReputation = new Dictionary<string, float>();

        /// <summary>Fires whenever general reputation changes, with the new clamped value.</summary>
        public event Action<float> OnGeneralReputationChanged;

        /// <summary>Fires whenever a specific faction's reputation changes, with its id and new clamped value.</summary>
        public event Action<string, float> OnFactionReputationChanged;

        public float GetGeneralReputation() => _generalReputation;

        /// <summary>
        /// Applies a delta to general reputation, clamped to [-100, 100]. Positive delta = good
        /// deed, negative = bad deed. This should be called once per deed — not once per NPC.
        /// </summary>
        public void ModifyGeneralReputation(float delta)
        {
            _generalReputation = Mathf.Clamp(_generalReputation + delta, MinReputation, MaxReputation);
            OnGeneralReputationChanged?.Invoke(_generalReputation);
        }

        public float GetFactionReputation(string factionId)
        {
            if (string.IsNullOrEmpty(factionId)) return 0f;
            return _factionReputation.TryGetValue(factionId, out float value) ? value : 0f;
        }

        /// <summary>
        /// Applies a delta to a specific faction's reputation, clamped to [-100, 100].
        /// Unrecognized faction IDs are created on first use (no faction registry needed).
        /// </summary>
        public void ModifyFactionReputation(string factionId, float delta)
        {
            if (string.IsNullOrEmpty(factionId)) return;

            float updated = Mathf.Clamp(GetFactionReputation(factionId) + delta, MinReputation, MaxReputation);
            _factionReputation[factionId] = updated;
            OnFactionReputationChanged?.Invoke(factionId, updated);
        }

        public ReputationTier GetGeneralReputationTier() => GetTierForScore(_generalReputation);

        public ReputationTier GetFactionReputationTier(string factionId) => GetTierForScore(GetFactionReputation(factionId));

        /// <summary>
        /// Converts a raw score into a named tier. Thresholds are intentionally simple and
        /// centralized here so every consequence system (vendor pricing, guard aggression,
        /// greeting behavior) reads the world the same way.
        /// </summary>
        private ReputationTier GetTierForScore(float score)
        {
            if (score <= -60f) return ReputationTier.Hated;
            if (score <= -20f) return ReputationTier.Disliked;
            if (score < 20f) return ReputationTier.Neutral;
            if (score < 60f) return ReputationTier.Liked;
            return ReputationTier.Trusted;
        }
    }
}