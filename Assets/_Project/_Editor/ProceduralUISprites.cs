#if UNITY_EDITOR
using System.IO;
using UnityEngine;
using UnityEditor;

namespace Project.CustomEditor
{
    /// <summary>
    /// Generates rounded-rectangle textures (with an optional border and a vertical gradient
    /// fill) entirely in code — no external art assets required — and saves them as real PNG
    /// files imported as 9-sliced Sprites, so they stay crisp at any size in the UI.
    /// </summary>
    public static class ProceduralUISprites
    {
        /// <summary>
        /// Generates a rounded-rect sprite and saves it to the given path (creating/overwriting
        /// the PNG asset), configured for 9-slicing so it can be stretched cleanly.
        /// </summary>
        public static Sprite CreateRoundedRectSprite(
            string assetPath,
            int textureSize,
            float cornerRadius,
            float borderThickness,
            Color borderColor,
            Color fillTop,
            Color fillBottom)
        {
            Texture2D texture = GenerateTexture(textureSize, cornerRadius, borderThickness, borderColor, fillTop, fillBottom);

            byte[] pngBytes = texture.EncodeToPNG();
            Object.DestroyImmediate(texture);

            File.WriteAllBytes(assetPath, pngBytes);
            AssetDatabase.ImportAsset(assetPath);

            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.filterMode = FilterMode.Bilinear;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;

                // Border sized to cover the rounded corner area, so 9-slicing stretches only
                // the flat middle sections and leaves the rounded corners undistorted.
                float borderPx = cornerRadius + borderThickness + 2f;
                importer.spriteBorder = new Vector4(borderPx, borderPx, borderPx, borderPx);

                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        }

        private static Texture2D GenerateTexture(
            int size,
            float cornerRadius,
            float borderThickness,
            Color borderColor,
            Color fillTop,
            Color fillBottom)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Vector2 halfExtent = new Vector2(size / 2f, size / 2f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 pixelCenterOffset = new Vector2(x + 0.5f, y + 0.5f) - halfExtent;
                    float distance = RoundedRectSignedDistance(pixelCenterOffset, halfExtent, cornerRadius);

                    // Soft anti-aliased edge over roughly 1 pixel.
                    float outerAlpha = Mathf.Clamp01(0.5f - distance);

                    Color pixelColor;
                    if (distance > -borderThickness)
                    {
                        pixelColor = borderColor;
                    }
                    else
                    {
                        float verticalT = y / (float)(size - 1);
                        pixelColor = Color.Lerp(fillBottom, fillTop, verticalT);
                    }

                    pixelColor.a *= outerAlpha;
                    texture.SetPixel(x, y, pixelColor);
                }
            }

            texture.Apply();
            return texture;
        }

        /// <summary>
        /// Standard rounded-rectangle signed distance field: negative inside the shape,
        /// positive outside, zero exactly on the (rounded) edge.
        /// </summary>
        private static float RoundedRectSignedDistance(Vector2 point, Vector2 halfExtent, float cornerRadius)
        {
            Vector2 q = new Vector2(
                Mathf.Abs(point.x) - halfExtent.x + cornerRadius,
                Mathf.Abs(point.y) - halfExtent.y + cornerRadius);

            float outsideDistance = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude;
            float insideDistance = Mathf.Min(Mathf.Max(q.x, q.y), 0f);

            return outsideDistance + insideDistance - cornerRadius;
        }
    }
}
#endif