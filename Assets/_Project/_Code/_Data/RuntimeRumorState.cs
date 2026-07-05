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
        // A link back to our original immutable asset file
        public RumorTemplate SourceTemplate { get; private set; }

        // How strongly this specific NPC belives this rumor. 0 = doesn't believe it at all, 1 = fully believes it.
        public float PersonalCredibilityScore { get; set; }

        // Tracks the real-world timestamp when this NPC last shared or heard this rumor.
        public DateTime LastInteractionTime { get; set; }

        // v2: Added. Tracks whether this rumor has already been auto-presented once.
        // Only relevant for AutoProximity rumors — ManualTalk rumors intentionally ignore
        // this flag, since re-triggering on every [E] press is expected player-driven behavior.
        public bool HasBeenPresented { get; set; } = false;

        // Constructor: Runs when an NPC hears a peice of news for the very first time.
        public RuntimeRumorState(RumorTemplate template, float initialCredibility)
        {
            SourceTemplate = template;
            PersonalCredibilityScore = initialCredibility;
            LastInteractionTime = DateTime.Now;
        }
    }
}