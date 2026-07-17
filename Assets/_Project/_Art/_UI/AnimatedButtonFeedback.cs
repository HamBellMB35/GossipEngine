using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Project.UI
{
    /// <summary>
    /// Smooth hover/press feedback for a UI button: tints and scales the button's Image over a
    /// short transition. Colors, scale factors, and timing are all editable in the Inspector.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class AnimatedButtonFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        [Header("Colors")]
        [SerializeField] private Color _normalColor = Color.white;
        [SerializeField] private Color _hoverColor = new Color(1.15f, 1.15f, 1.15f, 1f);
        [SerializeField] private Color _pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);

        [Header("Scale")]
        [SerializeField] private float _hoverScale = 1.03f;
        [SerializeField] private float _pressedScale = 0.97f;

        [Header("Timing")]
        [SerializeField] private float _transitionDuration = 0.12f;

        private Image _image;
        private RectTransform _rectTransform;
        private Vector3 _baseScale;
        private Coroutine _colorRoutine;
        private Coroutine _scaleRoutine;

        private void Awake()
        {
            _image = GetComponent<Image>();
            _rectTransform = GetComponent<RectTransform>();
            _baseScale = _rectTransform.localScale;
            _image.color = _normalColor;
        }

        public void OnPointerEnter(PointerEventData eventData) => AnimateTo(_hoverColor, _hoverScale);
        public void OnPointerExit(PointerEventData eventData) => AnimateTo(_normalColor, 1f);
        public void OnPointerDown(PointerEventData eventData) => AnimateTo(_pressedColor, _pressedScale);
        public void OnPointerUp(PointerEventData eventData) => AnimateTo(_hoverColor, _hoverScale);

        private void AnimateTo(Color targetColor, float targetScaleMultiplier)
        {
            if (_colorRoutine != null) StopCoroutine(_colorRoutine);
            if (_scaleRoutine != null) StopCoroutine(_scaleRoutine);

            _colorRoutine = StartCoroutine(AnimateColor(targetColor));
            _scaleRoutine = StartCoroutine(AnimateScale(_baseScale * targetScaleMultiplier));
        }

        private IEnumerator AnimateColor(Color target)
        {
            Color start = _image.color;
            float t = 0f;

            while (t < _transitionDuration)
            {
                t += Time.unscaledDeltaTime;
                _image.color = Color.Lerp(start, target, t / _transitionDuration);
                yield return null;
            }

            _image.color = target;
        }

        private IEnumerator AnimateScale(Vector3 target)
        {
            Vector3 start = _rectTransform.localScale;
            float t = 0f;

            while (t < _transitionDuration)
            {
                t += Time.unscaledDeltaTime;
                _rectTransform.localScale = Vector3.Lerp(start, target, t / _transitionDuration);
                yield return null;
            }

            _rectTransform.localScale = target;
        }
    }
}