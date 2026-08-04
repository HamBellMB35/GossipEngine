namespace TownsPeople.GamePlay
{
    /// <summary>
    /// Core-defined contract for any per-NPC movement system. Currently implemented only by
    /// the optional Locomotion add-on's LocomotionAgent, but written generically so any future
    /// movement add-on (e.g. a swimming or climbing system) can implement it too.
    ///
    /// This lets core interaction code (NPCProximityGossip) query and control an NPC's movement
    /// WITHOUT taking a compile-time dependency on any specific movement add-on — the add-on
    /// depends on Core by implementing this interface, never the other way around, same
    /// directional pattern already used by IInteractionExtension/INpcAddon for Vendor/Quest
    /// add-ons. No reflection is needed for this check (unlike the Type.GetType() pattern used
    /// elsewhere in this project) because the interface itself lives in Core — a component
    /// either implements it or it doesn't, discoverable with a plain GetComponent&lt;T&gt;().
    ///
    /// If no component implementing this interface is present on an NPC, core code treats that
    /// NPC as stationary and always interactable — identical to its behavior before this
    /// interface existed.
    /// </summary>
    public interface INpcMovementController
    {
        /// <summary>
        /// True only while this NPC is both actively moving AND currently at its Run speed
        /// tier. False while walking, idle, arrived, or paused.
        /// </summary>
        bool IsRunning { get; }

        /// <summary>
        /// Called by core interaction code the moment the player begins interacting with this
        /// NPC (dialogue menu opening, vendor/quest hijack, ambient greeting — any path).
        /// Implementations should halt movement immediately so the NPC stands still for the
        /// duration of the interaction. Never called while IsRunning is true — running NPCs are
        /// not interactable at all, so this method does not need to guard against that case.
        /// </summary>
        void PauseForInteraction();

        /// <summary>
        /// Called by core interaction code the moment an interaction ends — the dialogue menu
        /// closes, or the player walks out of range mid-conversation. Implementations should
        /// resume normal movement toward wherever the NPC was already headed.
        /// </summary>
        void ResumeAfterInteraction();
    }
}