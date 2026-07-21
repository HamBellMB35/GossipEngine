using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TownsPeople.Services;

namespace TownsPeople.UI
{
    /// <summary>
    /// A single reputation bar: a fillable Image plus a label and a centered value readout.
    /// Reusable for both the General reputation bar and each dynamically-created faction bar.
    /// Purely a display component — has no knowledge of ReputationService itself.
    /// </summary>
    // v2: Added a smooth fill animation instead of instantly snapping fillAmount to the new
    // value. Also explicitly initializes fillAmount on Awake — previously a freshly-added
    // Image component keeps Unity's own default (100%) until SetValue is first called, which
    // combined with the ReputationBarUI injection-timing bug meant the bar could appear
    // permanently "full" if that first call never happened.
    public class ReputationBarRow : MonoBehaviour
    {
        [Tooltip("The Image driving the fill visual. Must have Image Type set to 'Filled' (Horizontal recommended) in the Inspector.")]
        [SerializeField] private Image _fillImage;

        [Tooltip("Shows the bar's name (e.g. 'General' or a faction ID).")]
        [SerializeField] private TextMeshProUGUI _labelText;

        [Tooltip("Shows the raw numeric score, centered on the bar.")]
        [SerializeField] private TextMeshProUGUI _valueText;

        [Header("Fill Animation")]
        [Tooltip("How long the bar takes to animate to a new value, instead of snapping instantly.")]
        [SerializeField] private float _fillAnimationDuration = 0.5f;

        private Coroutine _fillAnimation;

        private void Awake()
        {
            if (_fillImage != null)
            {
                // Sensible neutral starting point (matches a reputation score of 0) instead of
                // leaving Unity's own default fillAmount (100%) visible before the first real
                // SetValue call arrives.
                _fillImage.fillAmount = 0.5f;
            }
        }

        /// <summary>
        /// Updates this row's label, fill amount (animated), and displayed value from a raw
        /// reputation score in the range [ReputationService.MinReputation, ReputationService.MaxReputation].
        /// </summary>
        public void SetValue(string label, float rawScore)
        {
            float normalized = Mathf.InverseLerp(ReputationService.MinReputation, ReputationService.MaxReputation, rawScore);

            if (_fillImage != null)
            {
                if (_fillAnimation != null)
                {
                    StopCoroutine(_fillAnimation);
                }
                _fillAnimation = StartCoroutine(AnimateFill(normalized));
            }

            if (_labelText != null)
            {
                _labelText.text = label;
            }

            if (_valueText != null)
            {
                _valueText.text = rawScore >= 0 ? $"+{rawScore:0}" : $"{rawScore:0}";
            }
        }

        private IEnumerator AnimateFill(float target)
        {
            float start = _fillImage.fillAmount;
            float elapsed = 0f;

            if (_fillAnimationDuration <= 0f)
            {
                _fillImage.fillAmount = target;
                yield break;
            }

            while (elapsed < _fillAnimationDuration)
            {
                elapsed += Time.deltaTime;
                _fillImage.fillAmount = Mathf.Lerp(start, target, elapsed / _fillAnimationDuration);
                yield return null;
            }

            _fillImage.fillAmount = target;
        }
    }
}