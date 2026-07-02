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