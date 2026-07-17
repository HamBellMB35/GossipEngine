using System.Collections.Generic;
using UnityEngine;
using VContainer;
using Project.Services;

namespace Project.UI
{
    /// <summary>
    /// OPTIONAL visualization tool. Displays the player's General reputation and a dynamically
    /// created bar per faction, updating live as ReputationService reports changes. Not
    /// required by any other system — safe to add or remove from a scene freely.
    ///
    /// Setup: assign a General Row (a ReputationBarRow already placed in your UI hierarchy),
    /// a Faction Row Prefab (a ReputationBarRow prefab — same structure, just not placed in
    /// the scene yet), and a Faction Row Container (an empty RectTransform / layout group
    /// that new faction rows get parented under).
    /// </summary>
    // v2: BUG FIX — subscription/initial sync previously happened in Start(), which raced
    // against GameLifetimeScope's GameBootstrapper manually injecting this component (via
    // IObjectResolver.Inject()). Unity does not guarantee a regular MonoBehaviour's Start()
    // runs after a VContainer entry point's Start() — if ReputationBarUI.Start() ran FIRST,
    // _reputation was still null, the whole setup silently bailed out via the early-return
    // warning, and the bar was permanently stuck showing its generation-time default fill
    // (100%, since nothing ever explicitly set it), never updating again. Fixed by moving the
    // subscription/sync into Construct() itself, which fires at the exact, guaranteed moment
    // injection actually happens — no ordering assumption required.
    public class ReputationBarUI : MonoBehaviour
    {
        [Header("General Reputation")]
        [SerializeField] private ReputationBarRow _generalRow;

        [Header("Faction Reputation (one row created per faction automatically)")]
        [SerializeField] private ReputationBarRow _factionRowPrefab;
        [SerializeField] private Transform _factionRowContainer;

        private ReputationService _reputation;
        private readonly Dictionary<string, ReputationBarRow> _factionRows = new Dictionary<string, ReputationBarRow>();

        [Inject]
        public void Construct(ReputationService reputation)
        {
            _reputation = reputation;

            _reputation.OnGeneralReputationChanged += HandleGeneralReputationChanged;
            _reputation.OnFactionReputationChanged += HandleFactionReputationChanged;

            // Sync immediately in case reputation already changed before this UI was injected.
            HandleGeneralReputationChanged(_reputation.GetGeneralReputation());
            foreach (KeyValuePair<string, float> pair in _reputation.GetAllFactionReputations())
            {
                HandleFactionReputationChanged(pair.Key, pair.Value);
            }
        }

        private void Start()
        {
            if (_reputation == null)
            {
                Debug.LogWarning("<color=orange>[ReputationBarUI]</color> ReputationService was not injected — is this GameObject registered in GameLifetimeScope?", this);
            }
        }

        private void OnDestroy()
        {
            if (_reputation == null) return;
            _reputation.OnGeneralReputationChanged -= HandleGeneralReputationChanged;
            _reputation.OnFactionReputationChanged -= HandleFactionReputationChanged;
        }

        private void HandleGeneralReputationChanged(float newValue)
        {
            if (_generalRow != null)
            {
                _generalRow.SetValue("General", newValue);
            }
        }

        private void HandleFactionReputationChanged(string factionId, float newValue)
        {
            ReputationBarRow row = GetOrCreateFactionRow(factionId);
            row?.SetValue(factionId, newValue);
        }

        private ReputationBarRow GetOrCreateFactionRow(string factionId)
        {
            if (_factionRows.TryGetValue(factionId, out ReputationBarRow existingRow))
            {
                return existingRow;
            }

            if (_factionRowPrefab == null || _factionRowContainer == null)
            {
                Debug.LogWarning("<color=orange>[ReputationBarUI]</color> Faction Row Prefab or Faction Row Container not assigned — cannot display faction bars.", this);
                return null;
            }

            ReputationBarRow newRow = Instantiate(_factionRowPrefab, _factionRowContainer);
            _factionRows[factionId] = newRow;
            return newRow;
        }
    }
}