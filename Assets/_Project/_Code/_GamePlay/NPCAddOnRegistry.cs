using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TownsPeople.GamePlay
{
    /// <summary>
    /// Central lookup point for "what add-ons does this NPC have."
    ///
    /// Add this to the same GameObject as your add-on components (Vendor, Quest Giver, etc.).
    /// The NPC Creator wizard adds this automatically on generated prefabs.
    ///
    /// Other systems (interaction, dialogue, reputation consequences) should query THIS component
    /// via TryGetAddon&lt;T&gt;() rather than calling GetComponent directly, so add-on lookups stay
    /// centralized, cached, and consistent across the project.
    /// </summary>
    public class NpcAddonRegistry : MonoBehaviour
    {
        private readonly Dictionary<System.Type, INpcAddon> _addonsByType = new Dictionary<System.Type, INpcAddon>();
        private IInteractionExtension _activeInteractionExtension;

        private void Awake()
        {
            CacheAddons();
        }

        /// <summary>
        /// Scans this GameObject for every component implementing INpcAddon and caches it.
        /// Also resolves which IInteractionExtension (if any) should handle interaction,
        /// based on InteractionPriority.
        /// </summary>
        private void CacheAddons()
        {
            _addonsByType.Clear();

            INpcAddon[] foundAddons = GetComponents<INpcAddon>();
            foreach (INpcAddon addon in foundAddons)
            {
                System.Type concreteType = addon.GetType();
                if (!_addonsByType.ContainsKey(concreteType))
                {
                    _addonsByType.Add(concreteType, addon);
                }
                else
                {
                    Debug.LogWarning($"<color=orange>[NpcAddonRegistry]</color> '{gameObject.name}' has more than one component of type '{concreteType.Name}'. Only the first was registered.", this);
                }
            }

            // Resolve the winning interaction extension up front (highest InteractionPriority wins).
            IInteractionExtension[] interactionExtensions = GetComponents<IInteractionExtension>();
            if (interactionExtensions.Length > 1)
            {
                Debug.LogWarning($"<color=orange>[NpcAddonRegistry]</color> '{gameObject.name}' has {interactionExtensions.Length} components implementing IInteractionExtension. " +
                                  $"Resolving by InteractionPriority (highest wins).", this);
            }

            _activeInteractionExtension = interactionExtensions
                .OrderByDescending(ext => ext.InteractionPriority)
                .FirstOrDefault();
        }

        /// <summary>
        /// Attempts to find an add-on of a specific type (interface or concrete class) attached to this NPC.
        /// Use this instead of GetComponent&lt;T&gt;() so add-on lookups stay centralized.
        /// </summary>
        public bool TryGetAddon<T>(out T addon) where T : class
        {
            foreach (INpcAddon cached in _addonsByType.Values)
            {
                if (cached is T match)
                {
                    addon = match;
                    return true;
                }
            }

            addon = null;
            return false;
        }

        /// <summary>
        /// Returns true if this NPC has an add-on of the given type attached, without needing the instance.
        /// Useful for editor tooling and quick capability checks.
        /// </summary>
        public bool HasAddon<T>() where T : class
        {
            return TryGetAddon<T>(out _);
        }

        /// <summary>
        /// Returns the single IInteractionExtension that should handle this NPC's interaction,
        /// already resolved by priority if multiple are present. Null if none are attached.
        /// </summary>
        public IInteractionExtension GetActiveInteractionExtension()
        {
            return _activeInteractionExtension;
        }

        /// <summary>
        /// All add-ons currently attached to this NPC, for editor/debug display.
        /// </summary>
        public IEnumerable<INpcAddon> GetAllAddons() => _addonsByType.Values;
    }
}