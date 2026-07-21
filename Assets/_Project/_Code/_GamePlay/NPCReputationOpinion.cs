using UnityEngine;
using VContainer;
using TownsPeople.Services;
using TownsPeople.Data;


namespace TownsPeople.GamePlay
{
    /// <summary>
    /// Represents this specific NPC's personal, temporary opinion of the player — layered on
    /// top of the shared ReputationService's general/faction scores. Intended for witness-style
    /// reactions: an NPC who directly saw the player do something reacts more strongly (and
    /// more immediately) than someone who only heard about it later, and that extra reaction
    /// fades back to baseline over time rather than being a permanent world-state change.
    ///
    /// This component is intentionally decoupled from the rumor system for now — nothing calls
    /// ApplyWitnessModifier() automatically yet, since the actual "player did a deed → NPC
    /// witnessed it" detection doesn't exist. That future system is what should call this.
    /// </summary>
    public class NPCReputationOpinion : MonoBehaviour
    {
        [Tooltip("Optional faction this NPC belongs to, for faction-aware effective opinion. Leave empty to use only general reputation.")]
        [SerializeField] private string _factionId;

        [Tooltip("How many points per second the personal modifier decays back toward zero.")]
        [SerializeField] private float _decayRatePerSecond = 1f;

        private float _personalModifier = 0f;
        private ReputationService _reputation;

        // v2: Greet cooldown/boost. Reuses the existing decaying personal modifier — a greet
        // boost isn't a separate permanent stat, it's just a temporary bump that fades over
        // time like any other witness reaction, with a cooldown preventing spam-clicking it.
        [Header("Greet Cooldown")]
        [Tooltip("How long (in seconds) before this NPC can be greeted again for a reputation boost.")]
        [SerializeField] private float _greetCooldownSeconds = 300f;
        private float _lastGreetTime = -Mathf.Infinity;

        [Inject]
        public void Construct(ReputationService reputation)
        {
            _reputation = reputation;
        }

        private void Update()
        {
            if (_personalModifier == 0f) return;

            float decayStep = _decayRatePerSecond * Time.deltaTime;

            if (Mathf.Abs(_personalModifier) <= decayStep)
            {
                _personalModifier = 0f;
            }
            else
            {
                _personalModifier -= Mathf.Sign(_personalModifier) * decayStep;
            }
        }

        /// <summary>
        /// Adds to this NPC's personal modifier (positive = this NPC now feels more warmly
        /// toward the player than the general population; negative = more harshly). Intended
        /// to be called when this specific NPC witnesses a player deed directly.
        /// </summary>
        public void ApplyWitnessModifier(float amount)
        {
            _personalModifier += amount;
        }

        /// <summary>True if enough time has passed since this NPC was last greeted for the boost to apply again.</summary>
        public bool CanGreet() => Time.time - _lastGreetTime >= _greetCooldownSeconds;

        /// <summary>Seconds remaining before this NPC can be greeted again. 0 if already available.</summary>
        public float GetGreetCooldownRemaining() => Mathf.Max(0f, _greetCooldownSeconds - (Time.time - _lastGreetTime));

        /// <summary>
        /// Applies a small personal-opinion boost from being greeted, if the cooldown has
        /// elapsed. Returns false (and does nothing) if still on cooldown.
        /// </summary>
        public bool TryApplyGreetBoost(float boostAmount)
        {
            if (!CanGreet()) return false;

            ApplyWitnessModifier(boostAmount);
            _lastGreetTime = Time.time;
            return true;
        }

        /// <summary>
        /// This NPC's current personal modifier, before baseline reputation is added.
        /// </summary>
        public float GetPersonalModifier() => _personalModifier;

        /// <summary>
        /// This NPC's full effective opinion of the player right now: general reputation,
        /// plus this NPC's faction reputation (if assigned), plus this NPC's personal
        /// witness modifier.
        /// </summary>
        public float GetEffectiveReputation()
        {
            if (_reputation == null) return _personalModifier;

            float baseline = _reputation.GetGeneralReputation();
            if (!string.IsNullOrEmpty(_factionId))
            {
                baseline += _reputation.GetFactionReputation(_factionId);
            }

            return baseline + _personalModifier;
        }

        /// <summary>
        /// Convenience wrapper: converts GetEffectiveReputation() into a named tier using the
        /// same thresholds ReputationService uses for general/faction scores.
        /// </summary>
        public ReputationTier GetEffectiveReputationTier()
        {
            float score = GetEffectiveReputation();

            if (score <= -60f) return ReputationTier.Hated;
            if (score <= -20f) return ReputationTier.Disliked;
            if (score < 20f) return ReputationTier.Neutral;
            if (score < 60f) return ReputationTier.Liked;
            return ReputationTier.Trusted;
        }
    }
}