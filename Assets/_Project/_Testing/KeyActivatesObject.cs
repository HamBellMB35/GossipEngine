using UnityEngine;
using UnityEngine.InputSystem;

namespace TownsPeople.Testing
{
    /// <summary>
    /// Quick test utility — press a chosen key to toggle a target GameObject's active state on
    /// and off.
    /// </summary>
    public class KeyActivatesObject : MonoBehaviour
    {
        [Tooltip("Press this key to toggle Target Object's active state.")]
        [SerializeField] private Key _activationKey = Key.V;

        [Tooltip("The GameObject to toggle active/inactive when the key is pressed.")]
        [SerializeField] private GameObject _targetObject;

        private void Update()
        {
            if (Keyboard.current == null || _targetObject == null) return;

            if (Keyboard.current[_activationKey].wasPressedThisFrame)
            {
                bool newState = !_targetObject.activeSelf;
                _targetObject.SetActive(newState);
                Debug.Log($"<color=cyan>[KeyActivatesObject]</color> '{_targetObject.name}' set active: {newState} via {_activationKey}.");
            }
        }
    }
}