using UnityEngine;

namespace TownsPeople.UI
{
    /// <summary>
    /// Shows/hides this NPC's nameplate based on the player's distance, with a fade
    /// transition. By default, matches the same range as this NPC's [E] interaction prompt
    /// (its SphereCollider radius) — disable "Match Prompt Range" to set an independent value.
    /// The actual name text itself is just a normal TextMeshProUGUI (wired by the wizard, or
    /// set manually) — this component only controls WHEN it's visible, so it works uniformly
    /// across every NPC variant (Common, Vendor, Quest Giver, Non-Dialogue) without depending
    /// on any of their other components.
    /// </summary>
    public class NPCNameplate : MonoBehaviour
    {
        [Tooltip("If disabled, the nameplate never shows for this NPC.")]
        [SerializeField] private bool _showNameplate = true;

        [Tooltip("If enabled, uses the same range as this NPC's [E] interaction trigger (its SphereCollider radius) automatically. Disable to set an independent range below.")]
        [SerializeField] private bool _matchPromptRange = true;

        [Tooltip("How close the player needs to be for the nameplate to show. Only used if Match Prompt Range is disabled.")]
        [SerializeField] private float _visibilityRange = 4f;

        [SerializeField] private CanvasGroupFader _fader;

        [Header("Placement")]
        [Tooltip("Local position of the nameplate text relative to its parent (the NPC's worldspace UI canvas). Adjust anytime to move it — updates live in the Editor, no need to enter Play mode.")]
        [SerializeField] private Vector3 _positionOffset = new Vector3(0f, 120f, 0f);

        private RectTransform _nameplateRect;
        private Transform _playerTransform;
        private SphereCollider _promptCollider;
        private bool _isCurrentlyVisible;
        private bool _isSuppressed;

        private void Awake()
        {
            _promptCollider = GetComponent<SphereCollider>();
            _fader?.SetInstant(false);

            if (_fader != null)
            {
                _nameplateRect = _fader.GetComponent<RectTransform>();
            }

            ApplyPositionOffset();
        }

        /// <summary>
        /// v3: Forces the nameplate hidden (or resumes normal distance-based behavior),
        /// overriding the per-frame distance check entirely. Used by NPCProximityGossip to
        /// hide the nameplate for the duration of an interaction, since a one-off hide alone
        /// would just pop back visible next frame while the player is still in range.
        /// </summary>
        public void SetSuppressed(bool suppressed)
        {
            _isSuppressed = suppressed;

            if (suppressed && _isCurrentlyVisible)
            {
                _isCurrentlyVisible = false;
                _fader?.Hide();
            }
        }

        /// <summary>
        /// Live-updates the nameplate's position the moment _positionOffset changes in the
        /// Inspector — works in Edit mode too, no need to enter Play mode to see it move.
        /// </summary>
        private void OnValidate()
        {
            ApplyPositionOffset();
        }

        private void ApplyPositionOffset()
        {
            if (_nameplateRect == null && _fader != null)
            {
                _nameplateRect = _fader.GetComponent<RectTransform>();
            }

            if (_nameplateRect != null)
            {
                _nameplateRect.localPosition = _positionOffset;
            }
        }

        private void Update()
        {
            if (_fader == null) return;
            if (_isSuppressed) return; // v3: Fully suppressed — skip all distance-based logic.

            if (!_showNameplate)
            {
                if (_isCurrentlyVisible)
                {
                    _isCurrentlyVisible = false;
                    _fader.Hide();
                }
                return;
            }

            if (_playerTransform == null)
            {
                GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
                if (playerObj != null)
                {
                    _playerTransform = playerObj.transform;
                }
                else
                {
                    return; // No player in the scene yet — nothing to measure distance against.
                }
            }

            float range = (_matchPromptRange && _promptCollider != null) ? _promptCollider.radius : _visibilityRange;
            float distance = Vector3.Distance(transform.position, _playerTransform.position);
            bool shouldBeVisible = distance <= range;

            if (shouldBeVisible != _isCurrentlyVisible)
            {
                _isCurrentlyVisible = shouldBeVisible;
                if (shouldBeVisible) _fader.Show();
                else _fader.Hide();
            }
        }
    }
}