using System;
using System.Collections.Generic;
using UnityEngine;

namespace Xenomorphtype
{
    internal sealed class XMTLineTextureCache : IDisposable
    {
        private readonly Dictionary<int, CachedLineTexture> texturesByKey = new Dictionary<int, CachedLineTexture>();

        public void DrawPolyline(int key, IList<Vector2> points, Color color, float width, float haloWidthMultiplier = 1f, float haloAlphaMultiplier = 0f)
        {
            if (Event.current.type != EventType.Repaint || points == null || points.Count < 2 || width <= 0f)
            {
                return;
            }

            float haloWidth = XMTLineDrawer.GetHaloWidth(width, haloWidthMultiplier, haloAlphaMultiplier);
            Rect bounds = XMTLineDrawer.GetPolylineBounds(points, Mathf.Max(width, haloWidth));
            if (bounds.width <= 0f || bounds.height <= 0f)
            {
                return;
            }

            int textureWidth = Mathf.CeilToInt(bounds.width);
            int textureHeight = Mathf.CeilToInt(bounds.height);
            int hash = XMTLineDrawer.GetPolylineHash(points, color, width, haloWidthMultiplier, haloAlphaMultiplier, textureWidth, textureHeight);

            CachedLineTexture cachedTexture;
            if (!texturesByKey.TryGetValue(key, out cachedTexture))
            {
                cachedTexture = new CachedLineTexture();
                texturesByKey[key] = cachedTexture;
            }

            if (cachedTexture.Texture == null ||
                cachedTexture.Hash != hash ||
                cachedTexture.Width != textureWidth ||
                cachedTexture.Height != textureHeight)
            {
                cachedTexture.Release();
                cachedTexture.Texture = XMTLineDrawer.RenderPolylineTexture(points, bounds, color, width, haloWidthMultiplier, haloAlphaMultiplier, textureWidth, textureHeight);
                cachedTexture.Hash = hash;
                cachedTexture.Width = textureWidth;
                cachedTexture.Height = textureHeight;
            }

            if (cachedTexture.Texture != null)
            {
                GUI.DrawTexture(bounds, cachedTexture.Texture);
            }
        }

        public void Dispose()
        {
            foreach (CachedLineTexture cachedTexture in texturesByKey.Values)
            {
                cachedTexture.Release();
            }

            texturesByKey.Clear();
        }

        private sealed class CachedLineTexture
        {
            public RenderTexture Texture;
            public int Hash;
            public int Width;
            public int Height;

            public void Release()
            {
                if (Texture == null)
                {
                    return;
                }

                Texture.Release();
                UnityEngine.Object.Destroy(Texture);
                Texture = null;
            }
        }
    }

    internal static class XMTLineDrawer
    {
        private const float minimumSegmentLength = 0.01f;
        private const float texturePaddingMultiplier = 3f;
        private const string lineMaterialShader = "Hidden/Internal-Colored";

        private static Material lineMaterial;
        private static readonly List<Vector2> leftPoints = new List<Vector2>();
        private static readonly List<Vector2> rightPoints = new List<Vector2>();
        private static readonly List<Vector2> localPoints = new List<Vector2>();

        public static Rect GetPolylineBounds(IList<Vector2> points, float width)
        {
            float minX = points[0].x;
            float maxX = points[0].x;
            float minY = points[0].y;
            float maxY = points[0].y;

            for (int i = 1; i < points.Count; i++)
            {
                Vector2 point = points[i];
                minX = Mathf.Min(minX, point.x);
                maxX = Mathf.Max(maxX, point.x);
                minY = Mathf.Min(minY, point.y);
                maxY = Mathf.Max(maxY, point.y);
            }

            float padding = Mathf.Max(2f, width * texturePaddingMultiplier);
            return Rect.MinMaxRect(
                Mathf.Floor(minX - padding),
                Mathf.Floor(minY - padding),
                Mathf.Ceil(maxX + padding),
                Mathf.Ceil(maxY + padding));
        }

        public static float GetHaloWidth(float width, float haloWidthMultiplier, float haloAlphaMultiplier)
        {
            return haloAlphaMultiplier > 0f && haloWidthMultiplier > 1f ? width * haloWidthMultiplier : width;
        }

        public static int GetPolylineHash(IList<Vector2> points, Color color, float width, float haloWidthMultiplier, float haloAlphaMultiplier, int textureWidth, int textureHeight)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + textureWidth;
                hash = hash * 31 + textureHeight;
                hash = hash * 31 + Mathf.RoundToInt(width * 100f);
                hash = hash * 31 + Mathf.RoundToInt(haloWidthMultiplier * 100f);
                hash = hash * 31 + Mathf.RoundToInt(haloAlphaMultiplier * 100f);
                hash = hash * 31 + Mathf.RoundToInt(color.r * 255f);
                hash = hash * 31 + Mathf.RoundToInt(color.g * 255f);
                hash = hash * 31 + Mathf.RoundToInt(color.b * 255f);
                hash = hash * 31 + Mathf.RoundToInt(color.a * 255f);

                for (int i = 0; i < points.Count; i++)
                {
                    hash = hash * 31 + Mathf.RoundToInt(points[i].x * 10f);
                    hash = hash * 31 + Mathf.RoundToInt(points[i].y * 10f);
                }

                return hash;
            }
        }

        public static RenderTexture RenderPolylineTexture(IList<Vector2> points, Rect bounds, Color color, float width, float haloWidthMultiplier, float haloAlphaMultiplier, int textureWidth, int textureHeight)
        {
            EnsureLineMaterial();
            if (lineMaterial == null)
            {
                return null;
            }

            RenderTexture texture = new RenderTexture(textureWidth, textureHeight, 0, RenderTextureFormat.ARGB32)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.Create();

            localPoints.Clear();
            for (int i = 0; i < points.Count; i++)
            {
                localPoints.Add(points[i] - bounds.position);
            }

            RenderTexture previousTexture = RenderTexture.active;
            RenderTexture.active = texture;
            GL.Clear(true, true, Color.clear);

            lineMaterial.SetPass(0);
            GL.PushMatrix();
            GL.LoadPixelMatrix(0f, textureWidth, textureHeight, 0f);

            if (haloAlphaMultiplier > 0f && haloWidthMultiplier > 1f)
            {
                Color haloColor = color;
                haloColor.a *= haloAlphaMultiplier;
                DrawStroke(localPoints, width * haloWidthMultiplier, haloColor);
            }

            DrawStroke(localPoints, width, color);
            GL.PopMatrix();
            RenderTexture.active = previousTexture;

            return texture;
        }

        private static void DrawStroke(IList<Vector2> points, float width, Color color)
        {
            BuildStroke(points, width);
            GL.Begin(GL.TRIANGLES);
            GL.Color(color);

            for (int i = 1; i < leftPoints.Count; i++)
            {
                AddTriangle(leftPoints[i - 1], rightPoints[i - 1], rightPoints[i]);
                AddTriangle(leftPoints[i - 1], rightPoints[i], leftPoints[i]);
            }

            GL.End();
        }

        private static void EnsureLineMaterial()
        {
            if (lineMaterial != null)
            {
                return;
            }

            Shader shader = Shader.Find(lineMaterialShader);
            if (shader == null)
            {
                return;
            }

            lineMaterial = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            lineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            lineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
            lineMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            lineMaterial.SetInt("_ZWrite", 0);
        }

        private static void BuildStroke(IList<Vector2> points, float width)
        {
            leftPoints.Clear();
            rightPoints.Clear();

            float halfWidth = width / 2f;

            for (int i = 0; i < points.Count; i++)
            {
                Vector2 current = points[i];
                if (i == 0)
                {
                    Vector2 normal = SegmentNormal(points[0], points[1]);
                    AddStrokePair(current, normal, halfWidth);
                    continue;
                }

                if (i == points.Count - 1)
                {
                    Vector2 normal = SegmentNormal(points[i - 1], points[i]);
                    AddStrokePair(current, normal, halfWidth);
                    continue;
                }

                Vector2 incomingNormal = SegmentNormal(points[i - 1], current);
                Vector2 outgoingNormal = SegmentNormal(current, points[i + 1]);
                Vector2 miter = incomingNormal + outgoingNormal;
                if (miter.sqrMagnitude <= minimumSegmentLength)
                {
                    AddStrokePair(current, outgoingNormal, halfWidth);
                    continue;
                }

                miter.Normalize();
                float scale = halfWidth / Mathf.Max(0.25f, Vector2.Dot(miter, outgoingNormal));
                AddStrokePair(current, miter, Mathf.Min(scale, width * 2f));
            }
        }

        private static Vector2 SegmentNormal(Vector2 start, Vector2 end)
        {
            Vector2 direction = end - start;
            if (direction.sqrMagnitude <= minimumSegmentLength)
            {
                return Vector2.up;
            }

            direction.Normalize();
            return new Vector2(-direction.y, direction.x);
        }

        private static void AddStrokePair(Vector2 point, Vector2 normal, float offset)
        {
            leftPoints.Add(point + normal * offset);
            rightPoints.Add(point - normal * offset);
        }

        private static void AddTriangle(Vector2 a, Vector2 b, Vector2 c)
        {
            GL.Vertex3(a.x, a.y, 0f);
            GL.Vertex3(b.x, b.y, 0f);
            GL.Vertex3(c.x, c.y, 0f);
        }
    }
}
