namespace Project.GamePlay
{
    /// <summary>
    /// Foundational extension contract interface. Any premium add-on component (Vendors, Quest Givers) 
    /// inherits from this to plug cleanly into the core NPC Creator interaction pipeline.
    /// </summary>
    public interface IInteractionExtension
    {
        /// <summary>
        /// Fires when the player interacts with the NPC. 
        /// Returns TRUE if the add-on is hijacking the conversation thread (e.g., opening a store UI menu).
        /// </summary>
        bool OnExtendInteraction();
    }
}
