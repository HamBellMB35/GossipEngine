using System.Collections;
using UnityEngine;
using TMPro;

namespace TownsPeople.UI
{
    /// <summary>
    /// Drives a sand-dissolve visual on a single TextMeshProUGUI, layered ON TOP of whatever
    /// CanvasGroup alpha fade already controls its parent (e.g. DialogueMenuUI's _panelFader).
    /// Does NOT touch CanvasGroup alpha at all — Unity's UI system already bakes the parent
    /// CanvasGroup's alpha into this text's vertex colors before TMP_SandDissolve.shader ever
    /// runs, so the existing fade keeps working with zero extra wiring. This component only
    /// animates the shader's _DissolveAmount on a per-instance material, fully independent of
    /// that fade.
    ///
    /// Generates its own noise texture at runtime by default — no texture asset required to
    /// drop this into a project.
    /// </summary>
    public class TMPSandDissolveEffect : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _text;
        [SerializeField] private Shader _dissolveShader;

        [Header("Noise")]
        [Tooltip("Leave empty to auto-generate a Perlin noise texture at Awake().")]
        [SerializeField] private Texture2D _noiseTexture;
        [SerializeField] private int _generatedNoiseSize = 128;
        [Tooltip("Higher = finer sand grain (more noise cells tiled across the texture).")]
        [SerializeField] private float _generatedNoiseScale = 6f;

        [Header("Look")]
        [SerializeField] private Color _edgeColor = new Color(1f, 0.75f, 0.35f, 1f);
        [SerializeField] private float _edgeWidth = 0.08f;
        [SerializeField] private Vector2 _windDirection = new Vector2(1f, 0.35f);
        [SerializeField] private float _windStrength = 0.15f;

        private Material _runtimeMaterial;
        private Texture2D _generatedNoise;
        private Coroutine _activeRoutine;

        private static readonly int DissolveAmountID = Shader.PropertyToID("_DissolveAmount");
        private static readonly int EdgeColorID = Shader.PropertyToID("_EdgeColor");
        private static readonly int EdgeWidthID = Shader.PropertyToID("_EdgeWidth");
        private static readonly int WindDirectionID = Shader.PropertyToID("_WindDirection");
        private static readonly int WindStrengthID = Shader.PropertyToID("_WindStrength");
        private static readonly int NoiseTexID = Shader.PropertyToID("_NoiseTex");

        private void Awake()
        {
            if (_text == null) _text = GetComponent<TextMeshProUGUI>();

            if (_dissolveShader == null)
            {
                Debug.LogWarning($"<color=orange>[TMPSandDissolveEffect]</color> No dissolve shader assigned on '{name}' — effect disabled.", this);
                enabled = false;
                return;
            }

            // Per-object material — required so multiple option buttons dissolving at
            // different times/rates don't share (and fight over) the same _DissolveAmount.
            _runtimeMaterial = new Material(_dissolveShader);
            _runtimeMaterial.CopyPropertiesFromMaterial(_text.fontSharedMaterial); // Carries over _MainTex (font atlas) and _FaceColor by matching property names.
            _text.fontMaterial = _runtimeMaterial;
            _runtimeMaterial = _text.fontMaterial; // fontMaterial getter returns the instance TMP itself now owns.

            Texture2D noiseToUse = _noiseTexture;
            if (noiseToUse == null)
            {
                _generatedNoise = GenerateNoiseTexture(_generatedNoiseSize, _generatedNoiseScale);
                noiseToUse = _generatedNoise;
            }

            _runtimeMaterial.SetTexture(NoiseTexID, noiseToUse);
            _runtimeMaterial.SetColor(EdgeColorID, _edgeColor);
            _runtimeMaterial.SetFloat(EdgeWidthID, _edgeWidth);
            _runtimeMaterial.SetVector(WindDirectionID, _windDirection);
            _runtimeMaterial.SetFloat(WindStrengthID, _windStrength);
            _runtimeMaterial.SetFloat(DissolveAmountID, 0f);
        }

        /// <summary>
        /// Plays the dissolve over `duration` seconds, 0 (fully visible) to 1 (fully dissolved).
        /// Call this alongside whatever already triggers this text's CanvasGroup fade-out —
        /// they run independently, so pass a matching duration if you want them to finish
        /// together, or offset the call slightly for a staggered look.
        /// </summary>
        public void PlayDissolve(float duration)
        {
            if (!enabled) return;
            if (_activeRoutine != null) StopCoroutine(_activeRoutine);
            _activeRoutine = StartCoroutine(DissolveRoutine(duration));
        }

        /// <summary>Resets the text to fully solid — call before reusing/showing this option again.</summary>
        public void ResetDissolve()
        {
            if (!enabled) return;
            if (_activeRoutine != null) { StopCoroutine(_activeRoutine); _activeRoutine = null; }
            _runtimeMaterial.SetFloat(DissolveAmountID, 0f);
        }

        private IEnumerator DissolveRoutine(float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                _runtimeMaterial.SetFloat(DissolveAmountID, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
            _runtimeMaterial.SetFloat(DissolveAmountID, 1f);
            _activeRoutine = null;
        }

        /// <summary>
        /// Simple tiled Perlin noise, baked to a texture once at Awake(). Good enough for a
        /// dissolve cutoff pattern — doesn't need to be seamless-tileable since each option
        /// button gets its own material instance and UV space.
        /// </summary>
        private static Texture2D GenerateNoiseTexture(int size, float scale)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = (float)x / size * scale;
                    float ny = (float)y / size * scale;
                    float value = Mathf.PerlinNoise(nx, ny);
                    tex.SetPixel(x, y, new Color(value, value, value, value));
                }
            }
            tex.Apply();

            return tex;
        }

        private void OnDestroy()
        {
            if (_runtimeMaterial != null) Destroy(_runtimeMaterial);
            if (_generatedNoise != null) Destroy(_generatedNoise);
        }
    }
}