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
    /// v2: How a Point of Interest waypoint decides whether to actually stop on any given visit.
    /// Only meaningful when LocomotionWaypoint.IsPointOfInterest is enabled — a plain waypoint
    /// never stops regardless of this value.
    /// </summary>
    public enum WaypointStopBehavior
    {
        // Stops here every single time this NPC passes through.
        AlwaysStop,

        // Rolls StopChance fresh on every visit — otherwise flows through like a plain waypoint.
        RandomChance
    }

    /// <summary>
    /// One stop along a LocomotionRoute. Pure data — authored via LocomotionRouteEditor's
    /// Scene view handles and Inspector list, consumed by whichever behavior component
    /// (WandererBehavior, VendorScheduleBehavior, BuskerBehavior — Phase 2) is walking this
    /// route.
    /// </summary>
    // v2: Added Point of Interest fields. By default (IsPointOfInterest = false), a waypoint is
    // a pure pass-through path corner — LocomotionAgent flows through it at full speed with no
    // deceleration or stop, exactly as it always has. Only a waypoint explicitly marked as a
    // Point of Interest triggers LocomotionAgent's smooth arrival deceleration and (if
    // configured) its Stop animation flourish. This is intentionally scoped to JUST the
    // stop-or-don't-stop decision for now — richer POI behavior (lingering, facing a specific
    // direction, playing a dedicated idle, etc.) is a separate, future mechanic that will build
    // on top of these same fields rather than needing new ones.
    [Serializable]
    public class LocomotionWaypoint
    {
        [Tooltip("World-space position of this waypoint.")]
        public Vector3 Position;

        [Tooltip("Which speed tier the NPC should be at by the time it arrives here. The actual Walk/Run speed values are configured per-NPC on LocomotionAgent, not here.")]
        public LocomotionSpeedTier ArrivalSpeedTier = LocomotionSpeedTier.Walk;

        [Tooltip("How long (seconds) a behavior may choose to linger here before moving on. Not all behaviors use this the same way — a simple wandering behavior can treat it as a base linger time; a vendor or busker behavior would typically use its own dedicated duration field instead and may ignore this.")]
        public float LingerDuration = 2f;

        [Header("Point of Interest")]
        [Tooltip("If disabled (the default), this is a plain pass-through path corner — LocomotionAgent flows through it at full speed, with no deceleration and no Stop animation. Enable this to mark it as a destination worth actually stopping at.")]
        public bool IsPointOfInterest = false;

        [Tooltip("Only relevant if Is Point Of Interest is enabled. Always Stop: stops here every visit. Random Chance: rolls Stop Chance fresh on every visit instead.")]
        public WaypointStopBehavior StopBehavior = WaypointStopBehavior.AlwaysStop;

        [Range(0f, 1f)]
        [Tooltip("Only relevant if Stop Behavior is Random Chance. Chance (0-1) this NPC stops here on any given visit, instead of flowing through like a plain waypoint.")]
        public float StopChance = 0.5f;

        public LocomotionWaypoint(Vector3 position)
        {
            Position = position;
        }
    }
}