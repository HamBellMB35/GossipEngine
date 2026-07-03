namespace Project.GamePlay
{
    /// <summary>
    /// Marker contract every modular NPC add-on implements (Vendor, Quest Giver, Locomotion, etc.).
    /// This is intentionally minimal — it exists purely so NpcAddonRegistry can discover
    /// "everything attached to this NPC" without needing to know concrete add-on types.
    ///
    /// Individual capabilities (interaction hijacking, price modification, flee behavior, etc.)
    /// are expressed as separate, more specific interfaces (e.g. IInteractionExtension) that an
    /// add-on can implement alongside this one.
    /// </summary>
    public interface INPCAddon
    {
        /// <summary>
        /// Human-readable name for this add-on, used in editor tooling and debug logs
        /// (e.g. "Vendor Add-on", "Quest Giver Add-on").
        /// </summary>
        string AddonDisplayName { get; }
    }
}