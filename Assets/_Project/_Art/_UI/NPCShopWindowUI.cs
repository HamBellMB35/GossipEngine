using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Project.UI
{
    // NOTE: This component manages the screenspace overlay panel for transactions.
    // It is automatically instantiated and auto-wired by the Creator Wizard Window.
    // It locks/unlocks the player's hardware mouse cursor depending on visibility states.

    /// <summary>
    /// Controls canvas group alpha states, title rendering, and input focus for premium vendor add-ons.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class NPCShopWindowUI : MonoBehaviour
    {
        // --- Internal UI Tracking Layers ---
        private CanvasGroup _shopCanvasGroup;
        private TextMeshProUGUI _shopTitleText;

        /// <summary>
        /// Automatically caches native canvas group configurations on startup frame.
        /// </summary>
        private void Awake()
        {
            _shopCanvasGroup = GetComponent<CanvasGroup>();
            _shopTitleText = GetComponentInChildren<TextMeshProUGUI>();

            // Lock the merchant storefront interface to completely invisible when the game boots up
            HideStorefront();
        }

        /// <summary>
        /// Forces the market menu overlay onto the screen and unlocks the hardware mouse cursor.
        /// </summary>
        /// <param name="storeName">The custom display string header assigned to this shop panel view.</param>
        public void DisplayStorefront(string storeName)
        {
            if (_shopCanvasGroup == null) return;

            // Dynamically rewrite the storefront banner string inside the layout
            if (_shopTitleText != null)
            {
                _shopTitleText.text = $"=== {storeName} ===";
            }

            // Bring the full screenspace graphic panel opacity up to full visibility
            _shopCanvasGroup.alpha = 1f;
            _shopCanvasGroup.interactable = true;
            _shopCanvasGroup.blocksRaycasts = true;

            // UNLOCK MOUSE: Allow the player to cleanly look through and select items with their cursor
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        /// <summary>
        /// Completely hides the trading UI backdrop and yields cursor focus back to standard gameplay camera loops.
        /// </summary>
        public void HideStorefront()
        {
            if (_shopCanvasGroup == null) return;

            // Fade the layout completely out of rendering passes
            _shopCanvasGroup.alpha = 0f;
            _shopCanvasGroup.interactable = false;
            _shopCanvasGroup.blocksRaycasts = false;

            // RE-LOCK MOUSE: Lock the cursor back down into the center of the viewport for standard 3D motion controls
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}