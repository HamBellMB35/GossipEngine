using UnityEngine;

namespace TownsPeople.Services
{
    /// <summary>
    /// Core service holding the player's current weapon/combat state — the integration point
    /// between this project's own player controller (whatever that ends up being — not built
    /// yet, no visibility into it from here) and anything that needs to react to it, currently
    /// the Locomotion add-on's Flocking/Fleeing system (NPCFlockingBehavior + FlockTriggerCondition/
    /// FlockReturnCondition assets).
    ///
    /// Registered as an injected singleton via GameLifetimeScope, same pattern as
    /// ReputationService/GossipManager. Your own player weapon and combat scripts should call
    /// SetWeaponDrawn() and NotifyCombatEngagement() into this — nothing calls them
    /// automatically, since this project has no player controller of its own yet.
    /// </summary>
    public class PlayerCombatState
    {
        [Tooltip("How long (seconds) after NotifyCombatEngagement() IsInCombat stays true, decaying on its own rather than needing an explicit 'combat ended' call from the player controller.")]
        private const float CombatActiveWindowSeconds = 3f;

        private Transform _playerTransform;
        private float _lastCombatEngagementTime = -Mathf.Infinity;

        /// <summary>True once SetWeaponDrawn(true) has been called, false again once SetWeaponDrawn(false) is.</summary>
        public bool IsWeaponDrawn { get; private set; }

        /// <summary>
        /// True for CombatActiveWindowSeconds after the most recent NotifyCombatEngagement()
        /// call, then automatically false again — self-decaying, no explicit "combat ended" call
        /// needed from the player controller (a single ongoing fight naturally re-triggers this
        /// window with each hit/engagement tick your own combat code should call).
        /// </summary>
        public bool IsInCombat => TimeSinceLastCombatEngagement() <= CombatActiveWindowSeconds;

        /// <summary>Optional — set once by whatever spawns/identifies the player, so triggers/conditions needing a position (e.g. flee-from-threat) don't each need their own "find the player" logic.</summary>
        public Transform PlayerTransform => _playerTransform;

        public void SetPlayerTransform(Transform playerTransform)
        {
            _playerTransform = playerTransform;
        }

        public void SetWeaponDrawn(bool isDrawn)
        {
            IsWeaponDrawn = isDrawn;
        }

        /// <summary>Call this from your own combat code every time the player engages in combat (lands/receives a hit, fires at an enemy, etc.) — resets the IsInCombat decay window.</summary>
        public void NotifyCombatEngagement()
        {
            _lastCombatEngagementTime = Time.time;
        }

        public float TimeSinceLastCombatEngagement()
        {
            return Time.time - _lastCombatEngagementTime;
        }
    }
}