using System;
using System.Collections.Generic;
using UnityEngine;
using Project.Data;

namespace Project.Data
{
    // Determines how the player interacts with this specific rumor.
    public enum RumorTriggerMode { AutoProximity, ManualTalk }

    // v5: Added. Whether this rumor represents a good deed or a bad one. Drives the
    // DIRECTION of reputation change (see ReputationImpact fields below) — magnitudes are
    // always authored as positive numbers, and this single field decides the sign for all of
    // them, so a rumor can't accidentally have contradictory magnitude/direction combinations.
    public enum RumorAlignment { Positive, Negative }

    // v6: Added. Which gendered voice line variant an NPC uses when a response provides both.
    public enum VoiceGender { Male, Female }

    /// <summary>
    /// A single reaction: display text plus optional gendered voice lines. Reused both for a
    /// rumor's own SpecificResponses and for the general Positive/Negative fallback pools in
    /// GeneralRumorResponseLibrary. Only one RumorResponse list ever needs to exist per
    /// rumor/pool — gender selection happens per-playback via GetVoiceLine(), not by
    /// duplicating entire response lists per gender.
    /// </summary>
    [Serializable]
    public struct RumorResponse
    {
        [TextArea(2, 4)]
        public string ResponseText;

        [Tooltip("Optional. Played when the reacting NPC's Voice Gender is set to Male.")]
        public AudioClip MaleVoiceLine;

        [Tooltip("Optional. Played when the reacting NPC's Voice Gender is set to Female.")]
        public AudioClip FemaleVoiceLine;

        /// <summary>
        /// Returns the voice line matching the requested gender. If that one is empty but the
        /// other gender's clip is set, plays that instead (better than silence). Returns null
        /// only if neither is assigned.
        /// </summary>
        public AudioClip GetVoiceLine(VoiceGender gender)
        {
            AudioClip preferred = gender == VoiceGender.Male ? MaleVoiceLine : FemaleVoiceLine;
            if (preferred != null) return preferred;

            AudioClip fallback = gender == VoiceGender.Male ? FemaleVoiceLine : MaleVoiceLine;
            return fallback;
        }
    }

    [CreateAssetMenu(fileName = "NewRumor", menuName = "Project/Gossip/Rumor")]
    public class RumorTemplate : ScriptableObject
    {
        public string RumorID;

        // v5: Added — see RumorAlignment above.
        [Header("Alignment")]
        [Tooltip("Good deed (Positive) or bad deed (Negative). Drives the direction of every reputation impact below.")]
        public RumorAlignment Alignment = RumorAlignment.Positive;

        [Header("Presentation Fallback (used only if no Specific Response and no General pool entry is available)")]
        [Tooltip("If OFF, this rumor's text will never appear in the speech bubble — useful for audio-only mutters/grumbles, or rumors that should only trigger animation/reputation effects silently.")]
        public bool ShowTextBubble = true;

        [Tooltip("Fallback text shown only if neither a Specific Response nor a General pool response is available.")]
        [TextArea(2, 4)]
        public string RumorDisplayText;

        [Tooltip("Fallback voice line, same conditions as above.")]
        public AudioClip VoiceLineAudio;

        [Header("Specific Spread Responses")]
        [Tooltip("Unique reactions specific to THIS rumor. Used (rotating through the list, not repeating the same one back to back) the first N times this rumor is presented across ALL NPCs — the count is shared game-wide, not per-NPC. Add as many as you want.")]
        public List<RumorResponse> SpecificResponses = new List<RumorResponse>();

        [Tooltip("How many total presentations (across every NPC) use a Specific Response before falling back to the General Positive/Negative pool.")]
        public int SpecificResponseUsageLimit = 3;

        [Header("Animation")]
        public GossipToneData AssociatedTone;

        [Header("Interaction Settings")]
        [Tooltip("How this rumor is triggered (Auto-proximity or Manual E-press).")]
        public RumorTriggerMode TriggerMode = RumorTriggerMode.AutoProximity;

        [Tooltip("Likelihood (0-100) of an NPC choosing to share this rumor with another NPC during a tick-based propagation pass.")]
        [Range(0, 100)] public int ShareLikelihood = 100;

        [Tooltip("Distance required to trigger this specific rumor.")]
        public float TriggerDistance = 3.0f;

        [Header("Reputation Impact (applied ONCE, world-wide, when this deed is witnessed)")]
        [Tooltip("Magnitude of general reputation change — always a positive number, direction comes from Alignment above.")]
        public float GeneralReputationMagnitude = 10f;

        [Tooltip("Optional faction this deed affects. Leave empty to skip faction impact entirely. Faction reputation always changes SLOWER than general reputation (see ReputationService.FactionImpactRateMultiplier) — there is no separate magnitude to author here.")]
        public string TargetFactionID;

        [Header("Personal Witness Impact (applied ONLY to NPCs who directly witness this deed)")]
        [Tooltip("Magnitude of personal opinion shift for a direct witness — always a positive number, direction comes from Alignment above. Typically larger than General Reputation Magnitude, and decays over time via NPCReputationOpinion.")]
        public float WitnessOpinionMagnitude = 25f;

        [Header("Tick Propagation")]
        [Tooltip("Credibility assigned to an NPC who learns this rumor secondhand via tick-based gossip (as opposed to a direct witness, who always gets full credibility).")]
        [Range(0f, 1f)] public float HearsayCredibility = 0.5f;

        /// <summary>Convenience: General Reputation Magnitude with the correct sign applied.</summary>
        public float SignedGeneralReputationImpact =>
            Alignment == RumorAlignment.Positive ? GeneralReputationMagnitude : -GeneralReputationMagnitude;

        /// <summary>Convenience: Witness Opinion Magnitude with the correct sign applied.</summary>
        public float SignedWitnessOpinionImpact =>
            Alignment == RumorAlignment.Positive ? WitnessOpinionMagnitude : -WitnessOpinionMagnitude;
    }
}