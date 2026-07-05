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
    ///
    /// Responsibilities:
    /// - Applies the deed's reputation impact to ReputationService exactly ONCE, regardless
    ///   of how many NPCs witness it (world state changes once per deed, not once per witness).
    /// - Finds every NPC within range and has each directly witness it: full credibility,
    ///   an immediate personal reaction on their NPCReputationOpinion (if present), and an
    ///   immediate presentation (they saw it happen, so they react right there and then).
    /// </summary>
    public class PlayerDeedBroadcaster : MonoBehaviour
    {
        [Tooltip("How far from the player an NPC can witness a deed.")]
        [SerializeField] private float _witnessRadius = 10f;

        [Tooltip("Which layers count as NPCs for witness detection. Restrict this to your NPC layer for performance once you have one set up.")]
        [SerializeField] private LayerMask _npcLayerMask = ~0; // Defaults to Everything.

        private ReputationService _reputation;

        [Inject]
        public void Construct(ReputationService reputation)
        {
            _reputation = reputation;
        }

        /// <summary>
        /// Broadcasts a player deed: applies its reputation impact once, then notifies every
        /// NPC currently within witness range.
        /// </summary>
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

            Debug.Log($"<color=green>[PlayerDeedBroadcaster]</color> Deed '{deedRumor.RumorID}' witnessed by {witnessed.Count} NPC(s) within {_witnessRadius}m.");
        }

        /// <summary>
        /// Applies the deed's general/faction reputation impact exactly once. This happens
        /// regardless of whether any NPC actually witnessed it — the deed itself is what
        /// changes the world, not the witnessing.
        /// </summary>
        private void ApplyWorldReputationImpact(RumorTemplate deedRumor)
        {
            if (_reputation == null) return;

            if (deedRumor.GeneralReputationImpact != 0f)
            {
                _reputation.ModifyGeneralReputation(deedRumor.GeneralReputationImpact);
            }

            if (!string.IsNullOrEmpty(deedRumor.TargetFactionID) && deedRumor.FactionReputationImpact != 0f)
            {
                _reputation.ModifyFactionReputation(deedRumor.TargetFactionID, deedRumor.FactionReputationImpact);
            }
        }

        private void NotifyWitness(NPCGossipMemory memory, RumorTemplate deedRumor)
        {
            // Direct witness = full credibility, unlike hearsay learned via tick propagation.
            memory.LearnRumor(deedRumor, credibility: 1f);

            NPCReputationOpinion opinion = memory.GetComponent<NPCReputationOpinion>();
            if (opinion != null && deedRumor.WitnessOpinionImpact != 0f)
            {
                opinion.ApplyWitnessModifier(deedRumor.WitnessOpinionImpact);
            }

            // A direct witness reacts immediately — they don't wait for a later proximity
            // check or an [E] press, since witnessing IS the trigger.
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