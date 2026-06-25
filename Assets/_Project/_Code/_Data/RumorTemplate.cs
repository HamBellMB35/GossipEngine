using UnityEngine;

namespace Project.Data
{
    /// <summary>
    /// This ScriptableObject is the data template for a single rumor.
    /// It is immutable, meaning NPCs only read from it; they never write to it.
    /// </summary>
    [CreateAssetMenu(menuName = "Gossip Engine/ Rumor Template/Rumor Template")]
    public class RumorTemplate : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Unique system ID used by tge codebase to track this specific rumor.")]
        public string RumorID;

        [Header("Narrative Context")]
        [Tooltip("The ID of the NPC or Player that this rumor is originally about.")]
        public string TargetSubjectID;

        [TextArea(3, 6)]
        [Tooltip("The core text description of what allegedly happened")]
        public string CoreFact;

        [Header("System Default")]
        [Tooltip("How shocking/scandalous this rumor is. Higher values spread faster.")]
        public float BaseSpiciness = 0.05f;

    }
}
