using System.Collections.Generic;
using UnityEngine;
using VContainer;
using Project.Services;
using Project.Data;

namespace Project.GamePlay
{
    /// <summary>
    /// Represents the WITNESS step of the gossip pipeline. Call BroadcastDeed() whenever the
    /// player performs something witnessable (a good or bad deed). This is the only place
    /// proximity matters in the whole propagation pipeline — everything after this point
    /// (tick-based NPC-to-NPC spread) is distance-agnostic by design.
    /// </summary>
    // v2: Updated to use RumorTemplate's Alignment-driven signed impacts (SignedGeneralReputationImpact,
    // SignedWitnessOpinionImpact) instead of the old separately-authored GeneralReputationImpact/
    // FactionReputationImpact/WitnessOpinionImpact fields. Faction impact is now automatically
    // derived at ReputationService.FactionImpactRateMultiplier of the general impact, rather
    // than being a second number the designer has to keep in sync by hand.
    public class PlayerDeedBroadcaster : MonoBehaviour
    {
        [Tooltip("How far from the player an NPC can witness a deed.")]
        [SerializeField] private float _witnessRadius = 10f;

        [Tooltip("Which layers count as NPCs for witness detection. Restrict this to your NPC layer for performance once you have one set up.")]
        [SerializeField] private LayerMask _npcLayerMask = ~0;

        private ReputationService _reputation;

        [Inject]
        public void Construct(ReputationService reputation)
        {
            _reputation = reputation;
        }

        public void BroadcastDeed(RumorTemplate deedRumor)
        {
            if (deedRumor == null)
            {
                Debug.LogWarning("<color=orange>[PlayerDeedBroadcaster]</color> Tried to broadcast a null deed rumor.", this);
                return;
            }

            ApplyWorldReputationImpact(deedRumor);

            Collider[] hits = Physics.OverlapSphere(transform.position, _witnessRadius, _npcLayerMask);
            HashSet<NPCGossipMemory> witnessed = new HashSet<NPCGossipMemory>();

            foreach (Collider hit in hits)
            {
                NPCGossipMemory memory = hit.GetComponentInParent<NPCGossipMemory>();
                if (memory == null || !witnessed.Add(memory)) continue;

                NotifyWitness(memory, deedRumor);
            }

            Debug.Log($"<color=green>[PlayerDeedBroadcaster]</color> Deed '{deedRumor.RumorID}' ({deedRumor.Alignment}) witnessed by {witnessed.Count} NPC(s) within {_witnessRadius}m.");
        }

        /// <summary>
        /// Applies the deed's general/faction reputation impact exactly once, regardless of
        /// how many NPCs witnessed it. Faction impact is always General impact scaled by
        /// ReputationService.FactionImpactRateMultiplier — moving deliberately slower.
        /// </summary>
        private void ApplyWorldReputationImpact(RumorTemplate deedRumor)
        {
            if (_reputation == null) return;

            float signedGeneralImpact = deedRumor.SignedGeneralReputationImpact;
            _reputation.ModifyGeneralReputation(signedGeneralImpact);

            if (!string.IsNullOrEmpty(deedRumor.TargetFactionID))
            {
                float signedFactionImpact = signedGeneralImpact * ReputationService.FactionImpactRateMultiplier;
                _reputation.ModifyFactionReputation(deedRumor.TargetFactionID, signedFactionImpact);
            }
        }

        private void NotifyWitness(NPCGossipMemory memory, RumorTemplate deedRumor)
        {
            memory.LearnRumor(deedRumor, credibility: 1f);

            NPCReputationOpinion opinion = memory.GetComponent<NPCReputationOpinion>();
            if (opinion != null)
            {
                opinion.ApplyWitnessModifier(deedRumor.SignedWitnessOpinionImpact);
            }

            memory.PresentRumor(deedRumor);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0f, 1f, 0f, 0.25f);
            Gizmos.DrawWireSphere(transform.position, _witnessRadius);
        }
#endif
    }
}