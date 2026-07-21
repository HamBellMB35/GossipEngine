namespace TownsPeople.Data
{
    /// <summary>
    /// Named bands of a reputation score, used by consequence systems (vendor pricing,
    /// guard aggression, NPC greeting behavior, etc.) instead of comparing raw floats directly.
    /// </summary>
    public enum ReputationTier
    {
        Hated,
        Disliked,
        Neutral,
        Liked,
        Trusted
    }
}