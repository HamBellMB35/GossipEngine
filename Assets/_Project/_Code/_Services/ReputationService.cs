using System;
using System.Collections.Generic;
using UnityEngine;
using TownsPeople.Data;

namespace TownsPeople.Services
{
    /// <summary>
    /// The single source of truth for the player's reputation. Registered once as a
    /// VContainer singleton (see GameLifetimeScope) — every system that needs to know
    /// "how does the world feel about the player" queries this same shared instance.
    /// </summary>
    public class ReputationService
    {
        // v3: Made public so consumers (like ReputationBarUI) can normalize scores correctly
        // without duplicating these numbers themselves.
        public const float MinReputation = -100f;
        public const float MaxReputation = 100f;

        // v4: Added. The single source of truth for "faction reputation always changes slower
        // than general reputation" — callers (PlayerDeedBroadcaster) multiply a deed's signed
        // general impact by this to get the faction impact, rather than authoring two
        // independent numbers per rumor that could drift out of the intended ratio.
        public static float FactionImpactRateMultiplier = 0.5f;

        private float _generalReputation = 0f;
        private readonly Dictionary<string, float> _factionReputation = new Dictionary<string, float>();

        public event Action<float> OnGeneralReputationChanged;
        public event Action<string, float> OnFactionReputationChanged;

        public float GetGeneralReputation() => _generalReputation;

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

        public void ModifyFactionReputation(string factionId, float delta)
        {
            if (string.IsNullOrEmpty(factionId)) return;

            float updated = Mathf.Clamp(GetFactionReputation(factionId) + delta, MinReputation, MaxReputation);
            _factionReputation[factionId] = updated;
            OnFactionReputationChanged?.Invoke(factionId, updated);
        }

        /// <summary>
        /// v3: Added — lets a UI (or any other consumer) enumerate every faction that has been
        /// touched so far, e.g. to build one bar per faction on startup rather than only
        /// reacting to future changes.
        /// </summary>
        public IReadOnlyDictionary<string, float> GetAllFactionReputations() => _factionReputation;

        public ReputationTier GetGeneralReputationTier() => GetTierForScore(_generalReputation);

        public ReputationTier GetFactionReputationTier(string factionId) => GetTierForScore(GetFactionReputation(factionId));

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