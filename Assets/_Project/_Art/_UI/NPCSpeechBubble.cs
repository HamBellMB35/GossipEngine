using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TownsPeople.UI
{
    // NOTE: This updated class provides absolute direct editor slider adjustments
    // for fade transitions. It completely eliminates frame latency by resetting 
    // and initiating the visibility interpolation loop the instant the interaction is called.

    /// <summary>
    /// Manages the worldspace dialogue canvas text population with fully custom editor fade parameters.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class NPCSpeechBubble : MonoBehaviour
    {
        private CanvasGroup _canvasGroup;
        private TextMeshProUGUI _dialogueText;
        private Coroutine _activeDisplayWorker;
        private bool _isInitialized = false;

        [Header("Timing Sliders (Seconds)")]
        [Tooltip("How fast the text panel fades into full visibility.")]
        [Range(0.05f, 2f)][SerializeField] private float fadeInDuration = 0.2f;

        [Tooltip("How long the speech text stays completely visible on screen.")]
        [SerializeField] private float visibleHoldDuration = 13f;

        [Tooltip("How fast the text panel fades back down to completely invisible.")]
        [Range(0.05f, 4f)][SerializeField] private float fadeOutDuration = 0.75f;

        [Header("Enable / Disable")]
        [Tooltip("If disabled, this NPC's speech bubble never displays anything, regardless of what tries to show text (rumors, greetings, dialogue menu, the old scripted-dialogue fallback, etc.). Audio and animation are unaffected.")]
        [SerializeField] private bool _useSpeechBubble = true;

        [Header("Placement")]
        [Tooltip("Local position of this speech bubble relative to its parent (the NPC's worldspace UI canvas). Adjust anytime to move the bubble — updates live in the Editor, no need to enter Play mode.")]
        [SerializeField] private Vector3 _positionOffset = new Vector3(0f, 80f, 0f);

        private RectTransform _rectTransform;

        private void Awake()
        {
            InitializeComponents();
            ApplyPositionOffset();
        }

        /// <summary>
        /// Live-updates the bubble's position the moment _positionOffset changes in the
        /// Inspector — works in Edit mode too, no need to enter Play mode to see it move.
        /// </summary>
        private void OnValidate()
        {
            ApplyPositionOffset();
        }

        private void ApplyPositionOffset()
        {
            if (_rectTransform == null)
            {
                _rectTransform = GetComponent<RectTransform>();
            }

            if (_rectTransform != null)
            {
                _rectTransform.localPosition = _positionOffset;
            }
        }

        /// <summary>
        /// Explicit initialization pass targeting local layers.
        /// </summary>
        public void InitializeComponents()
        {
            if (_isInitialized) return;

            _canvasGroup = GetComponent<CanvasGroup>();
            _dialogueText = GetComponentInChildren<TextMeshProUGUI>();

            if (_canvasGroup == null || _dialogueText == null)
            {
                Debug.LogError($"<color=red>[UI Error]</color> {gameObject.name} is missing critical canvas layout components!", this);
                return;
            }

            // Clean default dormant states
            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;

            _isInitialized = true;
        }

        /// <summary>
        /// Populates the text panel string and instantly kicks off the custom animation fade timeline loop.
        /// </summary>
        public void DisplayText(string message)
        {
            if (!_useSpeechBubble) return; // v3: Global per-NPC toggle — this NPC never shows bubble text at all.

            if (!_isInitialized) InitializeComponents();
            if (_dialogueText == null || _canvasGroup == null) return;

            // Kill any currently active fade or wait routines to prevent overlap lag
            if (_activeDisplayWorker != null)
            {
                StopCoroutine(_activeDisplayWorker);
            }

            // Push the text statement to the renderer immediately on frame zero
            _dialogueText.text = message;

            // Start the snappy fade sequence pipeline right now
            _activeDisplayWorker = StartCoroutine(AnimateBubbleSequence());
        }

        /// <summary>
        /// v2: Interrupts whatever this bubble is currently doing (fading in, holding, or
        /// already fading out) and fades to fully invisible right away, ignoring the normal
        /// Visible Hold Duration. Used when the player walks away or ends the conversation —
        /// previously the bubble would linger until its own internal timer ran out regardless.
        /// </summary>
        public void HideImmediately()
        {
            if (!_isInitialized) return;
            if (_canvasGroup == null) return;

            if (_activeDisplayWorker != null)
            {
                StopCoroutine(_activeDisplayWorker);
            }

            _activeDisplayWorker = StartCoroutine(FadeOutImmediateRoutine());
        }

        /// <summary>
        /// Advanced time-delta animation pipeline running entirely independent of frame updates.
        /// </summary>
        private IEnumerator AnimateBubbleSequence()
        {
            _canvasGroup.interactable = true;
            _canvasGroup.blocksRaycasts = true;

            // --- 1. INSTANT FADE-IN PHASE ---
            float timeElapsed = 0f;
            float initialAlpha = _canvasGroup.alpha; // Track current point if interrupting an old fade

            while (timeElapsed < fadeInDuration)
            {
                timeElapsed += Time.deltaTime;
                // Perfect smooth linear interpolation profile scaling up
                _canvasGroup.alpha = Mathf.Lerp(initialAlpha, 1f, timeElapsed / fadeInDuration);
                yield return null;
            }
            _canvasGroup.alpha = 1f;

            // --- 2. THE VISIBLE SUSTAIN PHASE (Locked down to your 13 seconds) ---
            yield return new WaitForSeconds(visibleHoldDuration);

            // --- 3. THE FADE-OUT PHASE ---
            timeElapsed = 0f;
            while (timeElapsed < fadeOutDuration)
            {
                timeElapsed += Time.deltaTime;
                _canvasGroup.alpha = Mathf.Lerp(1f, 0f, timeElapsed / fadeOutDuration);
                yield return null;
            }
            _canvasGroup.alpha = 0f;

            // Lock structural layers back down cleanly
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
        }

        /// <summary>
        /// Same fade-out easing as the normal sequence's final phase, but starts immediately
        /// from whatever the current alpha is, skipping the hold entirely.
        /// </summary>
        private IEnumerator FadeOutImmediateRoutine()
        {
            float timeElapsed = 0f;
            float initialAlpha = _canvasGroup.alpha;

            while (timeElapsed < fadeOutDuration)
            {
                timeElapsed += Time.deltaTime;
                _canvasGroup.alpha = Mathf.Lerp(initialAlpha, 0f, timeElapsed / fadeOutDuration);
                yield return null;
            }
            _canvasGroup.alpha = 0f;

            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
        }
    }
}