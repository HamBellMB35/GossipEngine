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

        [Tooltip("Likelihood (0-100) of the NPC choosing to share this rumor when triggered.")]
        [Range(0, 100)] public int ShareLikelihood = 100;

        [Tooltip("Distance required to trigger this specific rumor.")]
        public float TriggerDistance = 3.0f;
    }
}