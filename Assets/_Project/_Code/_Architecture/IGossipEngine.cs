namespace TownsPeople.Architecture
{
    // NOTE: This interface acts as a strict professional contract.
    // Any class that handles gossip logic must follow this layout.
    public interface IGossipEngine
    {
        // Kicks off our tracking arrays and data streams at startup
        void Initialize();

        // Standardized signature to transfer data records between two distinct simulation points
        void PropagateRumor(TownsPeople.Data.RumorTemplate rumor, string sourceNpcId, string targetNpcId);

        // v2: Added — runs one full tick-based propagation pass across every NPC currently
        // in the scene. Distance-agnostic by design (proximity only matters at the witness
        // step, not here) — see GossipTickDriver for what calls this and how often.
        void RunPropagationTick();
    }
}