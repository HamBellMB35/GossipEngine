
namespace Project.Architecture
{
    /// <summary>
    /// This interface acts as a strict professional contract.
    /// Any class that handles gossip logic must follow this layout.
    /// By talking to this interface instead of a specific script, 
    /// our NPCs don't care how the gossip engine works under the hood.
    /// </summary>
    public interface IGossipEngine
    {
        // Kicks off our tracking arrays and data streams at startup
        void Initialize();

        // Standardized signature to transfer data records between two distinct simulation points
        void PropagateRumor(string rumorId, string sourceNpcId, string targetNpcId);
    }
}
