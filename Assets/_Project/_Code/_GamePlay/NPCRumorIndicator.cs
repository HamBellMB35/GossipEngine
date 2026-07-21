using System.Collections.Generic;
using UnityEngine;

namespace TownsPeople.GamePlay
{
    /// <summary>
    /// OPTIONAL, fully standalone visualization add-on. Spawns one small, randomly-colored
    /// sphere above the NPC's head for each rumor currently known, arranged in a row (2 rumors
    /// = 2 spheres side by side, and so on). Requires no dependency injection and has zero
    /// coupling to any other system besides reading NPCGossipMemory's rumor count — safe to
    /// add to some NPCs and not others, or remove entirely, without affecting anything else.
    /// </summary>
    [RequireComponent(typeof(NPCGossipMemory))]
    public class NPCRumorIndicator : MonoBehaviour
    {
        [Tooltip("Height above the NPC's root position where the row of spheres is centered.")]
        [SerializeField] private float _heightOffset = 2.5f;

        [Tooltip("Horizontal spacing between each sphere in the row.")]
        [SerializeField] private float _sphereSpacing = 0.25f;

        [Tooltip("Diameter of each indicator sphere.")]
        [SerializeField] private float _sphereScale = 0.15f;

        [Tooltip("Optional material template — a tinted instance is created per sphere. Leave empty to use Unity's default Standard shader.")]
        [SerializeField] private Material _sphereMaterialTemplate;

        private NPCGossipMemory _gossipMemory;
        private readonly List<GameObject> _spawnedSpheres = new List<GameObject>();

        private void Awake()
        {
            _gossipMemory = GetComponent<NPCGossipMemory>();
        }

        private void OnEnable()
        {
            if (_gossipMemory == null) return;

            _gossipMemory.OnKnownRumorCountChanged += HandleKnownRumorCountChanged;
            // Sync immediately in case this indicator was added after rumors were already learned.
            HandleKnownRumorCountChanged(_gossipMemory.KnownRumors.Count);
        }

        private void OnDisable()
        {
            if (_gossipMemory != null)
            {
                _gossipMemory.OnKnownRumorCountChanged -= HandleKnownRumorCountChanged;
            }
        }

        private void HandleKnownRumorCountChanged(int newCount)
        {
            while (_spawnedSpheres.Count < newCount)
            {
                SpawnSphere();
            }

            // Not currently reachable (nothing removes rumors yet), but handled for
            // forward-compatibility if a "forget rumor" feature is ever added.
            while (_spawnedSpheres.Count > newCount)
            {
                RemoveLastSphere();
            }

            RepositionSpheres();
        }

        private void SpawnSphere()
        {
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = $"RumorIndicator_{_spawnedSpheres.Count}";
            sphere.transform.SetParent(transform, false);
            sphere.transform.localScale = Vector3.one * _sphereScale;

            // Purely visual — strip the auto-added physics collider.
            Collider sphereCollider = sphere.GetComponent<Collider>();
            if (sphereCollider != null)
            {
                Destroy(sphereCollider);
            }

            Renderer sphereRenderer = sphere.GetComponent<Renderer>();
            if (sphereRenderer != null)
            {
                Material material = _sphereMaterialTemplate != null
                    ? new Material(_sphereMaterialTemplate)
                    : new Material(Shader.Find("Standard"));

                material.color = Random.ColorHSV(0f, 1f, 0.6f, 1f, 0.7f, 1f);
                sphereRenderer.material = material;
            }

            _spawnedSpheres.Add(sphere);
        }

        private void RemoveLastSphere()
        {
            int lastIndex = _spawnedSpheres.Count - 1;
            GameObject sphere = _spawnedSpheres[lastIndex];
            _spawnedSpheres.RemoveAt(lastIndex);

            if (sphere != null)
            {
                Destroy(sphere);
            }
        }

        private void RepositionSpheres()
        {
            int count = _spawnedSpheres.Count;
            if (count == 0) return;

            float totalWidth = (count - 1) * _sphereSpacing;
            float startX = -totalWidth / 2f;

            for (int i = 0; i < count; i++)
            {
                _spawnedSpheres[i].transform.localPosition = new Vector3(startX + i * _sphereSpacing, _heightOffset, 0f);
            }
        }

        private void OnDestroy()
        {
            foreach (GameObject sphere in _spawnedSpheres)
            {
                if (sphere != null)
                {
                    Destroy(sphere);
                }
            }
            _spawnedSpheres.Clear();
        }
    }
}