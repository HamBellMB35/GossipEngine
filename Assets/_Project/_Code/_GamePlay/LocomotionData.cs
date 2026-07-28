using System;
using UnityEngine;

namespace TownsPeople.GamePlay
{
    /// <summary>
    /// Symbolic speed tier for a waypoint leg. The actual numeric speed each tier maps to is
    /// defined PER NPC on that NPC's own LocomotionAgent — not here — so the same route can be
    /// walked briskly by one NPC and leisurely by another.
    /// </summary>
    public enum LocomotionSpeedTier
    {
        Walk,
        Run
    }

    /// <summary>
    /// One stop along a LocomotionRoute. Pure data — authored via LocomotionRouteEditor's
    /// Scene view handles and Inspector list, consumed by whichever behavior component
    /// (WandererBehavior, VendorScheduleBehavior, BuskerBehavior — Phase 2) is walking this
    /// route.
    /// </summary>
    [Serializable]
    public class LocomotionWaypoint
    {
        [Tooltip("World-space position of this waypoint.")]
        public Vector3 Position;

        [Tooltip("Which speed tier the NPC should be at by the time it arrives here. The actual Walk/Run speed values are configured per-NPC on LocomotionAgent, not here.")]
        public LocomotionSpeedTier ArrivalSpeedTier = LocomotionSpeedTier.Walk;

        [Tooltip("How long (seconds) a behavior may choose to linger here before moving on. Not all behaviors use this the same way — a simple wandering behavior can treat it as a base linger time; a vendor or busker behavior would typically use its own dedicated duration field instead and may ignore this.")]
        public float LingerDuration = 2f;

        public LocomotionWaypoint(Vector3 position)
        {
            Position = position;
        }
    }
}