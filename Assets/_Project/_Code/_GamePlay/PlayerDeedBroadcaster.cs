using System.Collections.Generic;
using UnityEngine;
using VContainer;
using TownsPeople.Services;
using TownsPeople.Data;


namespace TownsPeople.GamePlay
{
    /// <summary>
    /// Represents the WITNESS step of the gossip pipeline. Call BroadcastDeed() whenever the
    /// player performs something witnessable (a good or bad deed). This is the only place
    /// proximity matters in the whole propagation pipeline � everything after this point
    /// (tick-based NPC-to-NPC spread) is distance-agnostic by design.
    /// </summary>
    // v2: Updated to use RumorTemplate's Alignment-driven signed impacts (SignedGeneralReputationImpact,
    // SignedWitnessOpinionImpact) instead of the old separately-authored GeneralReputationImpact/
    // FactionReputationImpact/WitnessOpinionImpact fields. Faction impact is now automatically
    // derived at ReputationService.FactionImpactRateMultiplier of the general impact, rather
    // than being a second number the designer has to keep in sync by hand.
    // v3: Added a global WitnessReactionMode + player-side animation trigger. REVERTED in v4.
    // v4: v3's approach was wrong � witness reaction is a per-NPC choice, not one global
    // player-side setting. Reverted those fields entirely. Each witnessing NPC now decides for
    // itself via an OPTIONAL NPCWitnessReaction component: if present and set to PlayAnimation,
    // that NPC plays its own configured reaction animation/audio instead of presenting the
    // rumor normally. If absent, or left at PresentRumor, behavior is identical to v2 � this
    // component is purely additive. Learning the rumor and the personal opinion adjustment
    // remain unconditional either way (game state, not presentation).
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
        /// ReputationService.FactionImpactRateMultiplier � moving deliberately slower.
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

        /// <summary>
        /// v4: LearnRumor and the personal-opinion witness modifier remain unconditional (game
        /// state). The presentation step now checks THIS SPECIFIC NPC's own OPTIONAL
        /// NPCWitnessReaction component: if present and set to PlayAnimation, that NPC plays
        /// its own configured reaction instead of the normal rumor presentation. If the
        /// component is absent, or left at PresentRumor, behavior is identical to before this
        /// component existed � each NPC decides independently, so a mixed group of witnesses
        /// can react in different ways to the same deed.
        /// </summary>
        private void NotifyWitness(NPCGossipMemory memory, RumorTemplate deedRumor)
        {
            memory.LearnRumor(deedRumor, credibility: 1f);

            NPCReputationOpinion opinion = memory.GetComponent<NPCReputationOpinion>();
            if (opinion != null)
            {
                opinion.ApplyWitnessModifier(deedRumor.SignedWitnessOpinionImpact);
            }

            // v5: A higher-priority behavior (currently only Flocking/Fleeing) is active on
            // this NPC � everything above already ran; presentation of any kind (either
            // branch below) is skipped entirely.
            IPriorityBehaviorState priorityBehavior = memory.GetComponent<IPriorityBehaviorState>();
            // v6: Also checks INpcMovementController.IsRunning — a plain running NPC (no
            // flocking involved at all, just a Locomotion route at Run speed) was never covered
            // by the v5 flocking-only check above, so it could still have its movement paused
            // for an ambient reaction while running. Now consistent with NPCProximityGossip's
            // own "a running NPC is never interactable" rule — running always skips
            // presentation, flocking or not; learning and the opinion adjustment above are
            // still completely unaffected either way.
            INpcMovementController movementController = memory.GetComponent<INpcMovementController>();
            bool isUninterruptible = (movementController != null && movementController.IsRunning)
                || (priorityBehavior != null && priorityBehavior.IsActive);

            if (isUninterruptible)
            {
                return;
            }

            NPCWitnessReaction reaction = memory.GetComponent<NPCWitnessReaction>();
            if (reaction != null && reaction.Mode == NPCWitnessReaction.ReactionMode.PlayAnimation)
            {
                reaction.PlayWitnessReaction();
            }
            else
            {
                memory.PresentRumor(deedRumor);
            }
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