namespace TownsPeople.GamePlay
{
    /// <summary>
    /// Core-defined contract for any add-on behavior that should take absolute priority over
    /// every normal reactive behavior (rumor presentation, witness reactions, ambient greetings,
    /// the [E] interaction system, etc.) while it's active. Currently implemented only by the
    /// Locomotion add-on's NPCFlockingBehavior.
    ///
    /// Same directional pattern as INpcMovementController — Core resolves this via plain
    /// GetComponent&lt;T&gt;(), no reflection needed, since the interface itself lives in Core.
    /// If no component implementing this interface is present, core code behaves exactly as it
    /// always has.
    ///
    /// Reactive systems that check this should still perform any underlying DATA recording they
    /// were already going to do (learning a rumor, adjusting reputation) — only the
    /// presentation/animation side effect is skipped. See PlayerDeedBroadcaster.NotifyWitness()
    /// for the reference example of this split.
    /// </summary>
    public interface IPriorityBehaviorState
    {
        /// <summary>True while this NPC is in an active priority behavior that should suppress every other reactive side effect.</summary>
        bool IsActive { get; }
    }
}