using System.Collections;
using UnityEngine;

namespace Project.UI
{
    /// <summary>
    /// Reusable fade controller for any CanvasGroup. Handles interrupting an in-progress fade
    /// cleanly (starts the new fade from the current alpha rather than snapping), and toggles
    /// interactable/blocksRaycasts appropriately so faded-out UI doesn't block raycasts or
    /// intercept input while invisible.
    /// </summary>
    // v2: BUG FIX — _canvasGroup was only ever assigned in this component's own Awake(). If
    // another component on the same GameObject (e.g. DialogueMenuUI) called Show()/Hide()/
    // SetInstant() from ITS OWN Awake(), Unity does not guarantee this component's Awake() has
    // already run — cross-component Awake() ordering is not guaranteed even on the same
    // GameObject. Fixed by resolving CanvasGroup lazily via a property on first use, which
    // works correctly regardless of Awake() call order (GetComponent works immediately, before
    // any Awake() has run, since it's just a lookup on the GameObject's existing components).
    [RequireComponent(typeof(CanvasGroup))]
    public class CanvasGroupFader : MonoBehaviour
    {
        [Header("Fade Timing (Seconds)")]
        [Tooltip("How long it takes to fade fully in.")]
        [Range(0f, 2f)][SerializeField] private float _fadeInDuration = 0.2f;

        [Tooltip("How long it takes to fade fully out.")]
        [Range(0f, 2f)][SerializeField] private float _fadeOutDuration = 0.2f;

        private CanvasGroup _canvasGroup;
        private Coroutine _activeFade;

        /// <summary>Lazily resolves the CanvasGroup on first access, regardless of whether Awake() has run yet.</summary>
        private CanvasGroup Group
        {
            get
            {
                if (_canvasGroup == null)
                {
                    _canvasGroup = GetComponent<CanvasGroup>();
                }
                return _canvasGroup;
            }
        }

        private void Awake()
        {
            SetInstant(false);
        }

        /// <summary>Fades this CanvasGroup fully visible over Fade In Duration.</summary>
        public void Show()
        {
            if (_activeFade != null) StopCoroutine(_activeFade);

            Group.interactable = true;
            Group.blocksRaycasts = true;
            _activeFade = StartCoroutine(FadeTo(1f, _fadeInDuration));
        }

        /// <summary>Fades this CanvasGroup fully invisible over Fade Out Duration.</summary>
        public void Hide()
        {
            if (_activeFade != null) StopCoroutine(_activeFade);
            _activeFade = StartCoroutine(FadeOutRoutine());
        }

        /// <summary>Snaps to fully visible or fully invisible with no fade — used for initial setup.</summary>
        public void SetInstant(bool visible)
        {
            if (_activeFade != null)
            {
                StopCoroutine(_activeFade);
                _activeFade = null;
            }

            Group.alpha = visible ? 1f : 0f;
            Group.interactable = visible;
            Group.blocksRaycasts = visible;
        }

        private IEnumerator FadeTo(float target, float duration)
        {
            if (duration <= 0f)
            {
                Group.alpha = target;
                yield break;
            }

            float start = Group.alpha;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                Group.alpha = Mathf.Lerp(start, target, elapsed / duration);
                yield return null;
            }

            Group.alpha = target;
        }

        private IEnumerator FadeOutRoutine()
        {
            yield return FadeTo(0f, _fadeOutDuration);
            Group.interactable = false;
            Group.blocksRaycasts = false;
        }
    }
}