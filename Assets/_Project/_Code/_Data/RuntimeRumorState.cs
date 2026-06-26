using System;
using UnityEngine;

namespace Project.Data
{
    // NOTE: This class is not a ScriptableObject, but a regular C# class. It is used to store the runtime state of a rumor for a specific NPC.
    // Because a ScriptableObject asset is a permanent file on your hard drive, we must never let NPCs change its variables during gameplay
    // (otherwise, if NPC A stops believing a rumor, it would accidentally change the file for all NPCs!).
    // Instead, we create a regular, lightweight C# class that acts as an NPC's personal "sticky note" about that rumor.


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

        // Tracks the real-world timestamp when this NPC last shared or heard this rumor. This is used to prevent NPCs from spamming the same rumor over and over again.
        public DateTime LastInteractionTime { get; set; }

        // Constructor: Runs when an NPC hears a peice of news for the very first time. It creates a new RuntimeRumorState for that NPC and links it to the immutable RumorTemplate.
        public RuntimeRumorState(RumorTemplate template, float initialCredibility)
        {
            SourceTemplate = template;
            PersonalCredibilityScore = 0f; // Start with no belief in the rumor
            LastInteractionTime = DateTime.Now; // Set the last interaction time to now

        }
    }
}