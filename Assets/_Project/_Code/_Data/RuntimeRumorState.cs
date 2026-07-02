using System;

namespace Project.Data
{
    // NOTE: This class is not a ScriptableObject, but a regular C# class. It is used to store the runtime state of a rumor for a specific NPC.

    /// <summary>
    /// This class tracks an NPC's unique relationship with a specific rumor.
    /// It lives purely in memory while the game is running.
    /// </summary>
    public class RuntimeRumorState
    {
        // A link bacl to our original immutable asset file
        public RumorTemplate SourceTemplate { get; private set; }

        // How strongly this specific NPC belives this rumor. 0 = doesn't believe it at all, 1 = fully believes it.
        public float PersonalCredibilityScore { get; set; }

        // Tracks the real-world timestamp when this NPC last shared or heard this rumor.
        public DateTime LastInteractionTime { get; set; }

        // Constructor: Runs when an NPC hears a peice of news for the very first time.
        public RuntimeRumorState(RumorTemplate template, float initialCredibility)
        {
            SourceTemplate = template;
            PersonalCredibilityScore = initialCredibility;
            LastInteractionTime = DateTime.Now;
        }
    }
}