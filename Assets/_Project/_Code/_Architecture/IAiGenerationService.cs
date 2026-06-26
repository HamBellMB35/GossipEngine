using System.Threading.Tasks;
using Project.Data;

namespace Project.Architecture
{
    /// <summary>
    /// Architectural contract for generating procedural text.
    /// Uses async Tasks so the local AI can process background thoughts without dropping game frames.
    /// With this we establish our interface contract. This keeps your system entirely decoupled. 
    /// Whether we hook this up to a local web client or Unity's native offline Sentis neural network framework later,
    /// gameplay scripts remain completely untouched.
    /// </summary>

    public interface IAiGenerationService
    {
        /// <summary>
        /// Sends the contextual game parameters to the local engine and returns the generated dialogue.
        /// </summary>
        Task<string> GenerateGossipDialogueAsync(AiPromptContext context);
    }
}

