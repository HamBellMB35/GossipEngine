using UnityEngine;
using Project.Data;

namespace Project.Data
{
    // Determines how the player interacts with this specific rumor.
    public enum RumorTriggerMode { AutoProximity, ManualTalk }

    [CreateAssetMenu(fileName = "NewRumor", menuName = "Project/Gossip/Rumor")]
    public class RumorTemplate : ScriptableObject
    {
        public string RumorID;

        [Header("Presentation Content")]
        [Tooltip("If OFF, this rumor's text will never appear in the speech bubble — useful for audio-only mutters/grumbles, or rumors that should only trigger animation/reputation effects silently.")]
        public bool ShowTextBubble = true;

        [Tooltip("The text shown in the NPC's speech bubble when this rumor is presented (only if Show Text Bubble is ON).")]
        [TextArea(2, 4)]
        public string RumorDisplayText;

        [Tooltip("Optional pre-recorded voice line to play alongside the text. Leave empty for silent/text-only.")]
        public AudioClip VoiceLineAudio;

        [Header("Animation")]
        public GossipToneData AssociatedTone;

        [Header("Interaction Settings")]
        [Tooltip("How this rumor is triggered (Auto-proximity or Manual E-press).")]
        public RumorTriggerMode TriggerMode = RumorTriggerMode.AutoProximity;

        [Tooltip("Likelihood (0-100) of an NPC choosing to share this rumor with another NPC during a tick-based propagation pass.")]
        [Range(0, 100)] public int ShareLikelihood = 100;

        [Tooltip("Distance required to trigger this specific rumor.")]
        public float TriggerDistance = 3.0f;

        // v4: Added — this rumor can now represent an actual player deed with real
        // consequences, instead of being purely presentational data.
        [Header("Reputation Impact (applied ONCE, world-wide, when this deed is witnessed)")]
        [Tooltip("Change to general reputation when this deed is witnessed. Positive = good deed, negative = bad deed.")]
        public float GeneralReputationImpact = 0f;

        [Tooltip("Optional faction this deed affects. Leave empty to skip faction impact entirely.")]
        public string TargetFactionID;

        [Tooltip("Change to the Target Faction's reputation when this deed is witnessed. Ignored if Target Faction ID is empty.")]
        public float FactionReputationImpact = 0f;

        [Header("Personal Witness Impact (applied ONLY to NPCs who directly witness this deed)")]
        [Tooltip("Extra personal opinion shift applied to an NPC's own NPCReputationOpinion when they directly witness this deed. Typically larger in magnitude than the general impact, and decays over time.")]
        public float WitnessOpinionImpact = 0f;

        [Header("Tick Propagation")]
        [Tooltip("Credibility assigned to an NPC who learns this rumor secondhand via tick-based gossip (as opposed to a direct witness, who always gets full credibility).")]
        [Range(0f, 1f)] public float HearsayCredibility = 0.5f;
    }
}