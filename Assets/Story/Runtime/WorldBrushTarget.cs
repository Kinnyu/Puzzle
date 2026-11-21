using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Puzzle.Story
{
    // One task owns its projected stencil and visited cells, including across board closes.
    internal sealed class WorldBrushTarget : IDisposable
    {
        internal const int Width = 160, Height = 100;
        const int Cells = Width * Height;
        internal readonly GameObject Object;
        internal readonly string Verb;
        internal readonly bool Erase;
        internal readonly StoryBrush.Option[] Options;
        internal readonly Action<int> Complete;
        readonly Color32[] previous;
        readonly float[] grain;
        readonly List<Part> parts = new();
        readonly MaterialPropertyBlock properties = new();
        readonly bool[] silhouette = new bool[Cells], mask = new bool[Cells], visited = new bool[Cells];
        readonly Color32[] preview = new Color32[Cells];
        Texture2D stencil;
        Vector4 axisX, axisY;
        int total, filled, strokes;
        internal int Choice { get; private set; }
        internal bool Free => Options[Choice].Mask == StoryBrush.Shape.Free;
        internal bool Finished => Free ? strokes >= 3 : total > 0 && filled >= Mathf.CeilToInt(total * .9f);
        internal string Progress => Free ? $"{strokes} / 3 筆" : $"{(total == 0 ? 0 : filled * 100 / total)}% / 90%";
        internal Vector3 Center { get; private set; }

        sealed class Part
        {
            internal MeshRenderer Renderer;
            internal Mesh Mesh;
            internal Vector3[] Points;
            internal int[] Triangles;
        }

        internal WorldBrushTarget(GameObject obj, string verb, bool erase, StoryBrush.Option[] options,
            Action<int> complete, Color32[] prior, float[] relief)
        {
            Object = obj;
            Verb = verb;
            Erase = erase;
            Options = options;
            Complete = complete;
            previous = prior;
            grain = relief;
            Center = obj.transform.position;
        }

        internal void Prepare(Camera eyes)
        {
            if (stencil != null) return;
            // Paint on the picture face, excluding attached stands and posts. Erasers include the whole pile.
            MeshFilter face = !Erase && Object.transform.childCount > 0 &&
                Object.transform.GetChild(0).name.EndsWith("畫面", StringComparison.Ordinal)
                ? Object.transform.GetChild(0).GetComponent<MeshFilter>() : null;
            if (!Erase && !face) face = Object.GetComponent<MeshFilter>();
            var filters = face ? new[] { face } : Object.GetComponentsInChildren<MeshFilter>();
            Bounds bounds = default;
            foreach (var filter in filters)
            {
                var renderer = filter.GetComponent<MeshRenderer>();
                var mesh = filter.sharedMesh;
                if (!renderer || !renderer.enabled || !mesh || !mesh.isReadable) continue;
                var points = mesh.vertices;
                for (int i = 0; i < points.Length; i++) points[i] = filter.transform.TransformPoint(points[i]);
                if (parts.Count == 0) bounds = renderer.bounds;
                else bounds.Encapsulate(renderer.bounds);
                parts.Add(new Part { Renderer = renderer, Mesh = mesh, Points = points, Triangles = mesh.triangles });
            }
            Center = parts.Count == 0 ? Object.transform.position : bounds.center;
            var normal = (Center - eyes.transform.position).normalized;
            var right = Vector3.Cross(Vector3.up, normal);
            if (right.sqrMagnitude < .001f) right = eyes.transform.right;
            right.Normalize();
            var up = Vector3.Cross(normal, right).normalized;
            // A sheet, sign or patch of road keeps the same composition across layers and viewing angles.
            if (face)
            {
                var size = Vector3.Scale(face.sharedMesh.bounds.size, face.transform.lossyScale);
                if (size.z < Mathf.Min(size.x, size.y) * .4f)
                {
                    right = face.transform.right;
                    up = face.transform.up;
                }
                else if (size.y < Mathf.Min(size.x, size.z) * .4f)
                {
                    right = face.transform.right;
                    up = face.transform.forward;
                }
            }
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            foreach (var part in parts)
                foreach (var point in part.Points)
                {
                    var xy = new Vector2(Vector3.Dot(right, point), Vector3.Dot(up, point));
                    min = Vector2.Min(min, xy);
                    max = Vector2.Max(max, xy);
                }
            axisX = Axis(right, min.x, max.x);
            axisY = Axis(up, min.y, max.y);
            foreach (var part in parts)
            {
                var pixels = Array.ConvertAll(part.Points, Pixel);
                for (int i = 0; i < part.Triangles.Length; i += 3)
                    Rasterize(pixels[part.Triangles[i]], pixels[part.Triangles[i + 1]], pixels[part.Triangles[i + 2]]);
            }
            stencil = new Texture2D(Width, Height, TextureFormat.RGBA32, false, true)
            { name = Verb, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Point };
            Select(0);
            ApplyCut(true);
        }

        static Vector4 Axis(Vector3 direction, float low, float high)
        {
            float scale = 1f / Mathf.Max(.0001f, high - low);
            return new Vector4(direction.x, direction.y, direction.z, -low) * scale;
        }

        Vector2 Pixel(Vector3 point)
        {
            var p = new Vector4(point.x, point.y, point.z, 1);
            return new Vector2(Vector4.Dot(axisX, p) * Width, Vector4.Dot(axisY, p) * Height);
        }

        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        void Rasterize(Vector2 a, Vector2 b, Vector2 c)
        {
            float winding = Cross(b - a, c - a);
            if (Mathf.Abs(winding) < .00001f) return;
            var low = Vector2Int.Max(Vector2Int.zero, Vector2Int.FloorToInt(Vector2.Min(a, Vector2.Min(b, c))));
            var high = Vector2Int.Min(new Vector2Int(Width - 1, Height - 1), Vector2Int.CeilToInt(Vector2.Max(a, Vector2.Max(b, c))));
            for (int y = low.y; y <= high.y; y++)
                for (int x = low.x; x <= high.x; x++)
                {
                    var p = new Vector2(x + .5f, y + .5f);
                    if (Cross(b - a, p - a) * winding >= 0 && Cross(c - b, p - b) * winding >= 0 && Cross(a - c, p - c) * winding >= 0)
                        silhouette[y * Width + x] = true;
                }
        }

        // Story props can be non-solid. Pick their rendered triangles without enabling gameplay colliders.
        internal bool Hit(Ray ray, out float distance, out Vector2 pixel)
        {
            distance = float.PositiveInfinity;
            pixel = default;
            foreach (var part in parts)
            {
                if (!part.Renderer || !part.Renderer.bounds.IntersectRay(ray)) continue;
                var t = part.Triangles;
                var v = part.Points;
                for (int i = 0; i < t.Length; i += 3)
                {
                    var a = v[t[i]];
                    var ab = v[t[i + 1]] - a;
                    var ac = v[t[i + 2]] - a;
                    var h = Vector3.Cross(ray.direction, ac);
                    float det = Vector3.Dot(ab, h);
                    if (Mathf.Abs(det) < 1e-9f) continue;
                    var s = ray.origin - a;
                    float u = Vector3.Dot(s, h) / det;
                    if (u < 0 || u > 1) continue;
                    var q = Vector3.Cross(s, ab);
                    float w = Vector3.Dot(ray.direction, q) / det;
                    if (w < 0 || u + w > 1) continue;
                    float depth = Vector3.Dot(ac, q) / det;
                    if (depth > 0 && depth < distance) distance = depth;
                }
            }
            if (float.IsPositiveInfinity(distance)) return false;
            pixel = Pixel(ray.GetPoint(distance));
            return true;
        }

        internal void Select(int choice)
        {
            Choice = choice;
            Array.Clear(visited, 0, Cells);
            total = filled = strokes = 0;
            for (int i = 0; i < Cells; i++)
            {
                int x = i % Width, y = i / Width;
                mask[i] = silhouette[i] && (Erase || StoryBrush.InMask(Options[choice].Mask, (x + .5f) / Width, (y + .5f) / Height));
                if (mask[i]) total++;
            }
            for (int i = 0; i < Cells; i++)
            {
                int x = i % Width, y = i / Width;
                bool edge = mask[i] && (x < 2 || y < 2 || x >= Width - 2 || y >= Height - 2 ||
                    !mask[i - 2] || !mask[i + 2] || !mask[i - 2 * Width] || !mask[i + 2 * Width]);
                Color32 guide = Erase ? new Color32(255, 203, 135, 0) : new Color32(145, 234, 255, 0);
                guide.a = !mask[i] ? (byte)0 : edge && (x + y) % 12 < 8 ? (byte)235 : (byte)36;
                preview[i] = previous != null && previous[i].a > 0 && !mask[i] ? previous[i] : guide;
            }
            Upload();
        }

        Color32 Ink(int i)
        {
            Color color = Options[Choice].Color;
            Color ink = Color.Lerp(color * (.7f + .4f * grain[i]), new Color(.96f, .86f, .61f), grain[i] * grain[i] * .24f);
            ink.a = 1;
            return ink;
        }

        internal bool Deposit(Vector2 pixel)
        {
            int cx = Mathf.FloorToInt(pixel.x), cy = Mathf.FloorToInt(pixel.y);
            if (cx < 0 || cy < 0 || cx >= Width || cy >= Height || !mask[cy * Width + cx]) return false;
            int radius = Free ? 3 : 8;
            for (int y = Mathf.Max(0, cy - radius); y <= Mathf.Min(Height - 1, cy + radius); y++)
                for (int x = Mathf.Max(0, cx - radius); x <= Mathf.Min(Width - 1, cx + radius); x++)
                {
                    int i = y * Width + x;
                    if (!mask[i] || visited[i] || (x - cx) * (x - cx) + (y - cy) * (y - cy) > radius * radius) continue;
                    visited[i] = true;
                    filled++;
                    preview[i] = Erase ? new Color32(0, 0, 0, 255) : Ink(i);
                }
            return true;
        }

        internal void EndStroke(float length) { if (Free && length >= 5) strokes++; }

        internal void Assist()
        {
            for (int i = 0; i < Cells; i++)
            {
                if (!mask[i]) continue;
                int x = i % Width, y = i / Width;
                if (Free && (x < Width / 6 || x > Width * 5 / 6 || (y + (int)(7 * Mathf.Sin(x * .05f))) % 22 > 4)) continue;
                visited[i] = true;
                preview[i] = Erase ? new Color32(0, 0, 0, 255) : Ink(i);
            }
            filled = total;
            strokes = 3;
            Upload();
        }

        internal void Upload()
        {
            stencil.SetPixels32(preview);
            stencil.Apply(false);
        }

        internal Color32[] Layers()
        {
            var result = previous == null ? new Color32[Cells] : (Color32[])previous.Clone();
            for (int i = 0; i < Cells; i++)
                if (visited[i] && !Erase) result[i] = Ink(i);
            return result;
        }

        internal void Draw(Camera camera, Material guide, bool hover)
        {
            properties.Clear();
            Configure(properties);
            properties.SetFloat("_BrushHighlight", hover ? 1.65f : 1);
            foreach (var part in parts)
            {
                var renderer = part.Renderer;
                if (!renderer || !renderer.gameObject.activeInHierarchy) continue;
                for (int i = 0; i < part.Mesh.subMeshCount; i++)
                    Graphics.DrawMesh(part.Mesh, renderer.localToWorldMatrix, guide, renderer.gameObject.layer,
                        camera, i, properties, ShadowCastingMode.Off, false);
            }
        }

        void Configure(MaterialPropertyBlock block)
        {
            block.SetTexture("_StoryStencil", stencil);
            block.SetVector("_StoryAxisX", axisX);
            block.SetVector("_StoryAxisY", axisY);
            block.SetFloat("_StoryErase", Erase ? 1 : 0);
        }

        // Cut both pigment and outline, retaining each renderer's existing material overrides.
        internal void ApplyCut(bool enabled)
        {
            if (!Erase || !stencil) return;
            foreach (var part in parts)
            {
                var renderer = part.Renderer;
                if (!renderer) continue;
                renderer.GetPropertyBlock(properties);
                if (enabled) Configure(properties);
                else properties.SetFloat("_StoryErase", 0);
                renderer.SetPropertyBlock(properties);
                // Ink has a per-material block which overrides the renderer-wide block.
                if (renderer.sharedMaterials.Length < 2) continue;
                renderer.GetPropertyBlock(properties, 1);
                if (enabled) Configure(properties);
                else properties.SetFloat("_StoryErase", 0);
                renderer.SetPropertyBlock(properties, 1);
            }
        }

        public void Dispose()
        {
            ApplyCut(false);
            if (stencil) UnityEngine.Object.Destroy(stencil);
        }

    }
}
