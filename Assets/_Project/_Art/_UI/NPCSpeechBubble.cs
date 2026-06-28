using System.Collections;
using UnityEngine;
using TMPro;

namespace Project.UI
{
    /// <summary>
    /// n your UI folder. This script handles showing the text, auto-hiding it after a few seconds,
    /// and forcing the bubble to look at the player camera.
    /// </summary>
    public class NPCSpeechBubble : MonoBehaviour
    {
        [Header("UI Components")]
        [SerializeField] private GameObject bubbleCanvasRoot;
        [SerializeField] private TextMeshProUGUI dialogueText;

        [Header("Settings")]
        [SerializeField] private float visibleDuration = 6f; // Duration the speech bubble remains visible


        private Coroutine _hideCoroutine;
        private Transform _mainCameraTransform;

        private void Start()
        {
            if(Camera.main != null)
            {
                _mainCameraTransform = Camera.main.transform;
            }
            else
            {
                Debug.LogError("Main camera not found. Please ensure there is a camera tagged as 'MainCamera' in the scene.");
            }

            // Hide by default on startup
            if (bubbleCanvasRoot != null) bubbleCanvasRoot.SetActive(false);
        }

        private void LateUpdate()
        {
            // Billboard effect: Make the speech bubble face the player
            if(bubbleCanvasRoot != null && bubbleCanvasRoot.activeSelf && _mainCameraTransform != null)
            {
                bubbleCanvasRoot.transform.LookAt(bubbleCanvasRoot.transform.position + _mainCameraTransform.forward);
                // bubbleCanvasRoot.transform.Rotate(0, 180, 0); // Adjust rotation to face the camera correctly
            }
        }

        public void DisplayText(string text)
        {
            if(dialogueText == null || bubbleCanvasRoot == null)
            {
                Debug.LogError("Dialogue text or bubble canvas root is not assigned.");
                return;
            }

            dialogueText.text = text;
            bubbleCanvasRoot.SetActive(true);

            // We reset the auto-hide timer if she says something else
            if(_hideCoroutine != null)
            {
                StopCoroutine(_hideCoroutine);
            }

            _hideCoroutine = StartCoroutine(HideAfterDelay());

        }

        private IEnumerator HideAfterDelay()
        {
            yield return new WaitForSeconds(visibleDuration);
            bubbleCanvasRoot.SetActive(false);
        }


    }
}
