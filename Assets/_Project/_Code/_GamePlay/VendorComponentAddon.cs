using UnityEngine;
using Project.Data;

namespace Project.GamePlay
{
    // NOTE: This component serves as a mock simulation blueprint for your premium Asset Store Vendor Addon Pack.
    // By inheriting from 'IInteractionExtension', it hooks directly into the core NPC Creator pipeline.
    // The core 'NPCProximityGossip' script detects this script at runtime and yields interaction priority to it.

    /// <summary>
    /// Premium extension component that intercepts normal conversation events to trigger shop interfaces completely offline.
    /// </summary>
    public class VendorComponentAddon : MonoBehaviour, IInteractionExtension
    {
        [Header("Vendor Shop Properties")]
        public string ShopName = "General Store";

        [Tooltip("The unique currency identifier token checked by this merchant asset.")]
        public string CurrencyType = "Gold_Coins";

        [Header("Mock Store Inventory Pool")]
        [Tooltip("A quick test array listing item names available for sale in this premium module mockup.")]
        [SerializeField] private string[] shopInventoryMock = { "Iron Sword", "Health Potion", "Leather Boots" };

        /// <summary>
        /// Interaction contract implementation pass. Called automatically by the core proximity detection system.
        /// Returns TRUE if this component successfully hijacks the talk string loop.
        /// </summary>
        public bool OnExtendInteraction()
        {
            // Debug Tracking Step 1: Log our successful architectural pipeline hijack to the Unity console panel
            Debug.Log($"<color=green>[Premium Module Intercept]</color> {gameObject.name} successfully stopped gossip threads! Opening Vendor Shop: '{ShopName}'");

            // Simulation Step 2: Loop through and print our localized inventory data matrix completely offline
            Debug.Log($"<color=yellow>--- {ShopName} Inventory Marketplace ---</color>");
            foreach (string item in shopInventoryMock)
            {
                Debug.Log($"<color=lime>[Inventory Item]</color> For Sale: {item} (Accepts: {CurrencyType})");
            }
            Debug.Log("<color=yellow>--------------------------------------</color>");

            // HOOK POINT: Trigger your premium shop canvas UI windows or screen inventory overlays here later!

            // Return TRUE to signal the core proximity script that we have fully processed the event 
            // and that it should completely block all standard AI and random scripted dialogue streams.
            return true;
        }
    }
}