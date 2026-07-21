using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;
using TownsPeople.Services;
using TownsPeople.GamePlay;

namespace TownsPeople.Testing
{
    /// <summary>
    /// Manual test harness for the Reputation System, since the real "player deed -> NPC
    /// witnesses it" pipeline doesn't exist yet. Lets you simulate general reputation shifts,
    /// faction reputation shifts, and a specific NPC's personal witness modifier independently,
    /// each on its own key.
    /// </summary>
    public class ReputationTester : MonoBehaviour
    {
        [Header("General Reputation Test")]
        [SerializeField] private float _generalReputationDelta = 10f;
        [SerializeField] private Key _generalReputationKey = Key.R;

        [Header("Faction Reputation Test")]
        [SerializeField] private string _factionId = "TownGuard";
        [SerializeField] private float _factionReputationDelta = 10f;
        [SerializeField] private Key _factionReputationKey = Key.T;

        [Header("Per-NPC Witness Modifier Test")]
        [Tooltip("The NPC whose personal opinion this key should affect.")]
        [SerializeField] private NPCReputationOpinion _targetNpcOpinion;
        [SerializeField] private float _witnessModifierDelta = -20f;
        [SerializeField] private Key _witnessModifierKey = Key.Y;

        private ReputationService _reputation;

        [Inject]
        public void Construct(ReputationService reputation) => _reputation = reputation;

        private void Update()
        {
            if (Keyboard.current == null) return;

            if (Keyboard.current[_generalReputationKey].wasPressedThisFrame)
            {
                TestGeneralReputation();
            }

            if (Keyboard.current[_factionReputationKey].wasPressedThisFrame)
            {
                TestFactionReputation();
            }

            if (Keyboard.current[_witnessModifierKey].wasPressedThisFrame)
            {
                TestWitnessModifier();
            }
        }

        private void TestGeneralReputation()
        {
            if (_reputation == null) return;

            _reputation.ModifyGeneralReputation(_generalReputationDelta);
            Debug.Log($"<color=magenta>[ReputationTester]</color> General reputation: {_reputation.GetGeneralReputation()} ({_reputation.GetGeneralReputationTier()})");
        }

        private void TestFactionReputation()
        {
            if (_reputation == null) return;

            _reputation.ModifyFactionReputation(_factionId, _factionReputationDelta);
            Debug.Log($"<color=magenta>[ReputationTester]</color> Faction '{_factionId}' reputation: {_reputation.GetFactionReputation(_factionId)} ({_reputation.GetFactionReputationTier(_factionId)})");
        }

        private void TestWitnessModifier()
        {
            if (_targetNpcOpinion == null)
            {
                Debug.LogWarning("<color=orange>[ReputationTester]</color> No Target NPC Opinion assigned.", this);
                return;
            }

            _targetNpcOpinion.ApplyWitnessModifier(_witnessModifierDelta);
            Debug.Log($"<color=magenta>[ReputationTester]</color> '{_targetNpcOpinion.gameObject.name}' personal modifier: {_targetNpcOpinion.GetPersonalModifier()} | Effective reputation: {_targetNpcOpinion.GetEffectiveReputation()} ({_targetNpcOpinion.GetEffectiveReputationTier()})");
        }
    }
}