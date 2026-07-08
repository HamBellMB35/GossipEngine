using UnityEngine;

namespace Project.UI
{
    /// <summary>
    /// Rotates this transform to face the active camera every frame. Intended for world-space
    /// UI (the [E] interaction prompt and speech bubble canvas) so it stays readable regardless
    /// of which direction the player approaches from or how the camera is angled.
    /// </summary>
    public class Billboard : MonoBehaviour
    {
        [Tooltip("If enabled, only rotates around the Y axis (stays upright, ignores camera pitch). If disabled, fully matches the camera's rotation.")]
        [SerializeField] private bool _lockVerticalRotation = true;

        [Tooltip("Flip 180 degrees. Use this if the UI renders backwards/unreadable for your specific canvas setup.")]
        [SerializeField] private bool _flip180 = false;

        private Camera _targetCamera;

        private void OnEnable()
        {
            _targetCamera = Camera.main;
        }

        private void LateUpdate()
        {
            if (_targetCamera == null)
            {
                _targetCamera = Camera.main;
                if (_targetCamera == null) return;
            }

            Quaternion targetRotation;

            if (_lockVerticalRotation)
            {
                float cameraYaw = _targetCamera.transform.eulerAngles.y;
                targetRotation = Quaternion.Euler(0f, cameraYaw, 0f);
            }
            else
            {
                targetRotation = _targetCamera.transform.rotation;
            }

            if (_flip180)
            {
                targetRotation *= Quaternion.Euler(0f, 180f, 0f);
            }

            transform.rotation = targetRotation;
        }
    }
}