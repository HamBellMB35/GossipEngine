
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

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            SetInstant(false);
        }

        /// <summary>Fades this CanvasGroup fully visible over Fade In Duration.</summary>
        public void Show()
        {
            if (_activeFade != null) StopCoroutine(_activeFade);

            _canvasGroup.interactable = true;
            _canvasGroup.blocksRaycasts = true;
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

            _canvasGroup.alpha = visible ? 1f : 0f;
            _canvasGroup.interactable = visible;
            _canvasGroup.blocksRaycasts = visible;
        }

        private IEnumerator FadeTo(float target, float duration)
        {
            if (duration <= 0f)
            {
                _canvasGroup.alpha = target;
                yield break;
            }

            float start = _canvasGroup.alpha;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                _canvasGroup.alpha = Mathf.Lerp(start, target, elapsed / duration);
                yield return null;
            }

            _canvasGroup.alpha = target;
        }

        private IEnumerator FadeOutRoutine()
        {
            yield return FadeTo(0f, _fadeOutDuration);
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
        }
    }
}