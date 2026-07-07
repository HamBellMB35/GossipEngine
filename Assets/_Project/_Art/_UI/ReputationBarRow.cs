using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Project.Services;

namespace Project.UI
{
    /// <summary>
    /// A single reputation bar: a fillable Image plus a label and a centered value readout.
    /// Reusable for both the General reputation bar and each dynamically-created faction bar.
    /// Purely a display component — has no knowledge of ReputationService itself.
    /// </summary>
    public class ReputationBarRow : MonoBehaviour
    {
        [Tooltip("The Image driving the fill visual. Must have Image Type set to 'Filled' (Horizontal recommended) in the Inspector.")]
        [SerializeField] private Image _fillImage;

        [Tooltip("Shows the bar's name (e.g. 'General' or a faction ID).")]
        [SerializeField] private TextMeshProUGUI _labelText;

        [Tooltip("Shows the raw numeric score, centered on the bar.")]
        [SerializeField] private TextMeshProUGUI _valueText;

        /// <summary>
        /// Updates this row's label, fill amount, and displayed value from a raw reputation
        /// score in the range [ReputationService.MinReputation, ReputationService.MaxReputation].
        /// </summary>
        public void SetValue(string label, float rawScore)
        {
            float normalized = Mathf.InverseLerp(ReputationService.MinReputation, ReputationService.MaxReputation, rawScore);

            if (_fillImage != null)
            {
                _fillImage.fillAmount = normalized;
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
    }
}