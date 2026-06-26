using UnityEngine;

namespace Project.Data
{
    /// <summary>
    /// An expanded data passport holding all live game context, 
    /// identity traits, and vocal properties required by localized AI models.
    /// This updated data passport holds your structural game context alongside the vocal identity traits.
    /// This is what we pass to the local AI so it knows exactly who is speaking, 
    /// how they sound, and who they are talking to (including the passing Player).
    /// </summary>


    public class AiPromptContext
    {
        // Social Context
        public string SpeakerID { get; set; }
        public string ListenerID { get; set; } // Can be specific NPC or player
        public string CurrentToneName { get; set; } // e.g., "Whispering", "Terrified" 
        public string RumorCoreFact { get; set; } // The core fact of the rumor being discussed
        public float PersonalCredibilityScore { get; set; }

        // Identity and vocal profile traits
        public string Gender { get; set; }
        public string VocalStyle { get; set; } // e.g., "Gravelly and deep", "Raspy elder", "Aggressive"
        public string AudioVoiceProfileID { get; set; } // Reference to a specific voice profile in the audio system and Hook identifier for local Text-To-Speech models
    }


}

