#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace TownsPeople.EditorTools
{
    /// <summary>
    /// Shared visual theme for TownsPeople's custom Editor tools — a consistent, rounded-
    /// corner "signature look" (same visual language as ProceduralUISprites' runtime UI:
    /// corner radius, border, fill) rather than default flat IMGUI boxes. Every color and the
    /// corner radius are user-editable and persist across Editor sessions via EditorPrefs, so
    /// a custom palette survives restarts. Deliberately kept separate from any one window/tool
    /// so it can be shared across multiple wizards for one unified look, not just this one.
    /// </summary>
    public static class TownsPeopleEditorTheme
    {
        private const string KeyAccent = "TownsPeople.Theme.Accent";
        private const string KeyPanel = "TownsPeople.Theme.Panel";
        private const string KeyPanelSelected = "TownsPeople.Theme.PanelSelected";
        private const string KeyBackground = "TownsPeople.Theme.Background";
        private const string KeyBorder = "TownsPeople.Theme.Border";
        private const string KeyCornerRadius = "TownsPeople.Theme.CornerRadius";

        private static readonly Color DefaultAccent = new Color(0.36f, 0.62f, 0.92f, 1f);
        private static readonly Color DefaultPanel = new Color(0.20f, 0.20f, 0.22f, 1f);
        private static readonly Color DefaultPanelSelected = new Color(0.22f, 0.32f, 0.44f, 1f);
        private static readonly Color DefaultBackground = new Color(0.15f, 0.15f, 0.16f, 1f);
        private static readonly Color DefaultBorder = new Color(0.80f, 0.66f, 0.32f, 1f);
        private const float DefaultCornerRadius = 6f;

        public static Color Accent
        {
            get => LoadColor(KeyAccent, DefaultAccent);
            set { SaveColor(KeyAccent, value); InvalidateCache(); }
        }

        public static Color Panel
        {
            get => LoadColor(KeyPanel, DefaultPanel);
            set { SaveColor(KeyPanel, value); InvalidateCache(); }
        }

        public static Color PanelSelected
        {
            get => LoadColor(KeyPanelSelected, DefaultPanelSelected);
            set { SaveColor(KeyPanelSelected, value); InvalidateCache(); }
        }

        public static Color Background
        {
            get => LoadColor(KeyBackground, DefaultBackground);
            set { SaveColor(KeyBackground, value); InvalidateCache(); }
        }

        public static Color Border
        {
            get => LoadColor(KeyBorder, DefaultBorder);
            set { SaveColor(KeyBorder, value); InvalidateCache(); }
        }

        public static float CornerRadius
        {
            get => EditorPrefs.GetFloat(KeyCornerRadius, DefaultCornerRadius);
            set { EditorPrefs.SetFloat(KeyCornerRadius, value); InvalidateCache(); }
        }

        public static void ResetToDefaults()
        {
            Accent = DefaultAccent;
            Panel = DefaultPanel;
            PanelSelected = DefaultPanelSelected;
            Background = DefaultBackground;
            Border = DefaultBorder;
            CornerRadius = DefaultCornerRadius;
        }

        private static Color LoadColor(string key, Color fallback)
        {
            if (!EditorPrefs.HasKey(key + ".r")) return fallback;
            return new Color(
                EditorPrefs.GetFloat(key + ".r"),
                EditorPrefs.GetFloat(key + ".g"),
                EditorPrefs.GetFloat(key + ".b"),
                EditorPrefs.GetFloat(key + ".a"));
        }

        private static void SaveColor(string key, Color value)
        {
            EditorPrefs.SetFloat(key + ".r", value.r);
            EditorPrefs.SetFloat(key + ".g", value.g);
            EditorPrefs.SetFloat(key + ".b", value.b);
            EditorPrefs.SetFloat(key + ".a", value.a);
        }

        // --- Rounded texture / GUIStyle generation, cached until the theme changes ---

        private static readonly Dictionary<string, Texture2D> _textureCache = new Dictionary<string, Texture2D>();

        private static void InvalidateCache()
        {
            foreach (Texture2D tex in _textureCache.Values)
            {
                if (tex != null) Object.DestroyImmediate(tex);
            }
            _textureCache.Clear();
        }

        /// <summary>
        /// Returns a cached, 9-sliceable rounded-rect texture for the given fill/border
        /// colors, regenerated only when the theme's corner radius or the requested colors
        /// change — not on every OnGUI call.
        /// </summary>
        public static Texture2D GetRoundedTexture(Color fillColor, float borderThickness, Color borderColor)
        {
            string key = $"{fillColor}|{borderThickness}|{borderColor}|{CornerRadius}";
            if (_textureCache.TryGetValue(key, out Texture2D cached) && cached != null)
            {
                return cached;
            }

            Texture2D tex = CreateRoundedTexture(48, CornerRadius, borderThickness, borderColor, fillColor);
            _textureCache[key] = tex;
            return tex;
        }

        /// <summary>
        /// Builds a GUIStyle using a rounded background texture (9-sliced via border offsets
        /// so it stretches cleanly at any size) with sensible padding/margin for a clean
        /// "card" look.
        /// </summary>
        public static GUIStyle CreateCardStyle(Color fillColor)
        {
            Texture2D bg = GetRoundedTexture(fillColor, 1.5f, Border);
            int sliceBorder = Mathf.Clamp(Mathf.RoundToInt(CornerRadius) + 4, 2, 20);

            return new GUIStyle(GUIStyle.none)
            {
                normal = { background = bg },
                border = new RectOffset(sliceBorder, sliceBorder, sliceBorder, sliceBorder),
                padding = new RectOffset(12, 12, 10, 10),
                margin = new RectOffset(4, 4, 4, 4)
            };
        }

        /// <summary>
        /// Builds a toggle-button style — selected uses PanelSelected fill + Accent border,
        /// unselected uses plain Panel fill + normal Border.
        /// </summary>
        public static GUIStyle CreateCategoryButtonStyle(bool selected)
        {
            Color fill = selected ? PanelSelected : Panel;
            Color border = selected ? Accent : Border;
            Texture2D bg = GetRoundedTexture(fill, selected ? 2f : 1f, border);
            int sliceBorder = Mathf.Clamp(Mathf.RoundToInt(CornerRadius) + 4, 2, 20);

            return new GUIStyle(GUIStyle.none)
            {
                normal = { background = bg, textColor = selected ? Color.white : new Color(0.82f, 0.82f, 0.82f) },
                alignment = TextAnchor.MiddleCenter,
                fontStyle = selected ? FontStyle.Bold : FontStyle.Normal,
                fontSize = 12,
                border = new RectOffset(sliceBorder, sliceBorder, sliceBorder, sliceBorder),
                padding = new RectOffset(10, 10, 6, 6),
                margin = new RectOffset(3, 3, 3, 3),
                fixedHeight = 30
            };
        }

        /// <summary>
        /// Signed distance field for a rounded box, centered at origin — the standard
        /// Inigo Quilez rounded-box SDF. Negative inside the shape, zero at the rounded
        /// boundary, positive outside.
        /// </summary>
        private static float RoundedBoxSDF(float px, float py, float halfWidth, float halfHeight, float radius)
        {
            float qx = Mathf.Abs(px) - halfWidth + radius;
            float qy = Mathf.Abs(py) - halfHeight + radius;
            float outsidePart = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));
            float insidePart = Mathf.Min(Mathf.Max(qx, qy), 0f);
            return outsidePart + insidePart - radius;
        }

        private static Texture2D CreateRoundedTexture(int size, float cornerRadius, float borderThickness, Color borderColor, Color fillColor)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };

            float halfSize = size * 0.5f;
            float radius = Mathf.Min(cornerRadius, halfSize);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = (x + 0.5f) - halfSize;
                    float py = (y + 0.5f) - halfSize;

                    float dist = RoundedBoxSDF(px, py, halfSize, halfSize, radius);
                    float outsideAlpha = 1f - Mathf.Clamp01(dist + 0.5f); // ~1px anti-aliased edge

                    Color pixelColor = (borderThickness > 0f && dist > -borderThickness) ? borderColor : fillColor;
                    pixelColor.a *= outsideAlpha;

                    tex.SetPixel(x, y, pixelColor);
                }
            }

            tex.Apply();
            return tex;
        }
    }
}
#endif