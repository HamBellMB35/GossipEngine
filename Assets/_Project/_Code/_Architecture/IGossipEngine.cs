namespace Project.Architecture
{
    // NOTE: This interface acts as a strict professional contract.
    // Any class that handles gossip logic must follow this layout.
    public interface IGossipEngine
    {
        // Kicks off our tracking arrays and data streams at startup
        void Initialize();

        // Standardized signature to transfer data records between two distinct simulation points
        void PropagateRumor(Project.Data.RumorTemplate rumor, string sourceNpcId, string targetNpcId);
    }
}