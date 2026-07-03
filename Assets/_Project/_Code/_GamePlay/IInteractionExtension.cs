namespace Project.GamePlay
{
    /// <summary>
    /// Foundational extension contract interface. Any premium add-on component (Vendors, Quest Givers) 
    /// inherits from this to plug cleanly into the core NPC Creator interaction pipeline.
    /// </summary>
    // v2: Added InteractionPriority. Previously, if an NPC had more than one IInteractionExtension
    // (e.g. Vendor + Quest Giver on the same NPC), whichever one GetComponent<T>() happened to find
    // first would silently win. Priority makes that outcome explicit and designer-controlled instead.
    public interface IInteractionExtension
    {
        /// <summary>
        /// Determines which add-on wins if more than one is attached to the same NPC and both
        /// want to hijack the interaction. Higher value wins. Recommended convention:
        /// Vendor = 0, Quest Giver = 10 (quests generally take priority over shopping).
        /// </summary>
        int InteractionPriority { get; }

        /// <summary>
        /// Fires when the player interacts with the NPC. 
        /// Returns TRUE if the add-on is hijacking the conversation thread (e.g., opening a store UI menu).
        /// </summary>
        bool OnExtendInteraction();
    }
}