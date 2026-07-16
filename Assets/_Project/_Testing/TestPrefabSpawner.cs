using UnityEngine;

namespace Project.Testing
{
    /// <summary>
    /// Quick test utility: spawns copies of a player-provided prefab in a row extending to
    /// the right of a chosen origin, triggered via a public method — intended to be wired up
    /// to a NPCGossipMemory CustomDialogueOption's OnSelected event for testing purposes.
    /// </summary>
    public class TestPrefabSpawner : MonoBehaviour
    {
        [Tooltip("The prefab to spawn. Assign any prefab you want to test with.")]
        [SerializeField] private GameObject _prefabToSpawn;

        [Tooltip("How many copies to spawn each time TriggerSpawn() is called.")]
        [SerializeField] private int _spawnCount = 3;

        [Tooltip("Distance between each spawned copy.")]
        [SerializeField] private float _spacing = 1.5f;

        [Tooltip("How far from the origin's position the first copy spawns.")]
        [SerializeField] private float _startDistance = 1.5f;

        [Tooltip("v2: Where spawned prefabs appear relative to (e.g. the Player's Transform). Leave empty to use this GameObject itself (the old default behavior).")]
        [SerializeField] private Transform _spawnOrigin;

        private Transform Origin => _spawnOrigin != null ? _spawnOrigin : transform;

        /// <summary>
        /// Spawns _spawnCount copies of _prefabToSpawn in a row extending to the origin's
        /// right, spaced _spacing apart. Wire this to a UnityEvent (e.g. a NPCGossipMemory
        /// CustomDialogueOption's OnSelected) to trigger it from the dialogue menu.
        /// </summary>
        public void TriggerSpawn()
        {
            if (_prefabToSpawn == null)
            {
                Debug.LogWarning($"<color=orange>[TestPrefabSpawner]</color> '{gameObject.name}' has no Prefab To Spawn assigned.", this);
                return;
            }

            Transform origin = Origin;

            for (int i = 0; i < _spawnCount; i++)
            {
                Vector3 spawnPosition = origin.position + origin.right * (_startDistance + i * _spacing);
                Instantiate(_prefabToSpawn, spawnPosition, origin.rotation);
            }

            Debug.Log($"<color=green>[TestPrefabSpawner]</color> Spawned {_spawnCount}x '{_prefabToSpawn.name}' next to '{origin.name}'.");
        }
    }
}