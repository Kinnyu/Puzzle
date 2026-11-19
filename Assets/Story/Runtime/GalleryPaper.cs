using System.Collections.Generic;
using UnityEngine;

namespace Puzzle.Story
{
    public sealed class GalleryPaper : MonoBehaviour
    {
        const int Width = 640;
        const int Height = 448;
        const float PaperWidth = 0.50f;
        const float PaperHeight = 0.35f;
        internal const float BallRadius = 0.052f;
        internal static readonly Color32 Blank = new(248, 239, 214, 255);

        GalleryDrawingDesk desk;
        Texture2D drawing;
        Material material;
        MeshFilter surface;
        SphereCollider ballCollider;
        Rigidbody body;
        Vector3 previousPosition;
        float removeAt;
        bool scored, banked;

        internal bool IsBall => body != null;

        internal void Initialize(GalleryDrawingDesk owner, Material template)
        {
            desk = owner;
            drawing = new Texture2D(Width, Height, TextureFormat.RGBA32, false)
            {
                name = "Paper sketch",
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = drawing.GetRawTextureData<Color32>();
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Blank;
            drawing.Apply(false);

            material = new Material(template);
            material.SetTexture("_BaseMap", drawing);
            material.SetTextureScale("_BaseMap", Vector2.one);
            material.SetTextureOffset("_BaseMap", Vector2.zero);
            material.SetColor("_BaseColor", Color.white);

            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Drawing surface";
            quad.transform.SetParent(transform, false);
            quad.transform.localScale = new Vector3(PaperWidth, PaperHeight, 1f);
            quad.GetComponent<Collider>().enabled = false;
            quad.GetComponent<Renderer>().sharedMaterial = material;
            surface = quad.GetComponent<MeshFilter>();
        }

        internal bool TryGetPixel(Ray ray, out Vector2Int pixel)
        {
            pixel = default;
            var plane = new Plane(-transform.forward, transform.position);
            if (!plane.Raycast(ray, out float distance)) return false;
            Vector3 local = transform.InverseTransformPoint(ray.GetPoint(distance));
            float u = local.x / PaperWidth + 0.5f;
            float v = local.y / PaperHeight + 0.5f;
            if (u < 0f || u >= 1f || v < 0f || v >= 1f) return false;
            pixel = new Vector2Int((int)(u * Width), (int)(v * Height));
            return true;
        }

        internal void Paint(Vector2Int from, Vector2Int to, float radius, Color32 color)
        {
            var pixels = drawing.GetRawTextureData<Color32>();
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(from, to) / Mathf.Max(1f, radius * 0.4f)));
            for (int step = 0; step <= steps; step++)
            {
                Vector2 center = Vector2.Lerp(from, to, step / (float)steps);
                int xMin = Mathf.Max(0, Mathf.FloorToInt(center.x - radius));
                int xMax = Mathf.Min(Width - 1, Mathf.CeilToInt(center.x + radius));
                int yMin = Mathf.Max(0, Mathf.FloorToInt(center.y - radius));
                int yMax = Mathf.Min(Height - 1, Mathf.CeilToInt(center.y + radius));
                for (int y = yMin; y <= yMax; y++)
                {
                    float dy = y - center.y;
                    for (int x = xMin; x <= xMax; x++)
                    {
                        float dx = x - center.x;
                        if (dx * dx + dy * dy <= radius * radius) pixels[y * Width + x] = color;
                    }
                }
            }
            drawing.Apply(false);
        }

        internal void AttachTo(Transform view)
        {
            if (IsBall)
            {
                removeAt = 0f;
                ballCollider.enabled = false;
                body.interpolation = RigidbodyInterpolation.None;
                body.isKinematic = true;
            }
            transform.SetParent(view, false);
            UpdateHeldPose(IsBall);
        }

        void UpdateHeldPose(bool crumpled)
        {
            transform.SetLocalPositionAndRotation(
                crumpled ? new Vector3(0.17f, -0.13f, 0.55f) : new Vector3(0.21f, -0.18f, 0.74f),
                Quaternion.Euler(10f, -13f, -13f));
        }

        internal void Fold(float t)
        {
            surface.transform.localScale = new Vector3(
                Mathf.Lerp(PaperWidth, BallRadius * 2, t),
                Mathf.Lerp(PaperHeight, BallRadius * 2, t), 1);
            transform.localRotation = Quaternion.Euler(10f + 40f * t, -13f + 92f * t, -13f - 25f * t);
        }

        internal void Crumple(Mesh mesh, PhysicsMaterial physicsMaterial)
        {
            surface.sharedMesh = mesh;
            surface.transform.localScale = Vector3.one;
            UpdateHeldPose(true);

            ballCollider = gameObject.AddComponent<SphereCollider>();
            ballCollider.radius = BallRadius;
            ballCollider.contactOffset = 0.004f;
            ballCollider.sharedMaterial = physicsMaterial;
            ballCollider.enabled = false;
            body = gameObject.AddComponent<Rigidbody>();
            body.mass = 0.018f;
            body.linearDamping = 0.12f;
            body.angularDamping = 0.4f;
            body.isKinematic = true;
        }

        internal void Release(Transform world, Vector3 position, Vector3 velocity, Collider playerBody)
        {
            transform.SetParent(world, true);
            transform.position = position;
            body.position = position;
            body.isKinematic = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            ballCollider.enabled = true;
            if (playerBody != null) Physics.IgnoreCollision(ballCollider, playerBody);
            body.linearVelocity = velocity;
            body.angularVelocity = new Vector3(7f, 2f, -4f);
            previousPosition = position;
            removeAt = Time.time + 35f;
            scored = false;
            banked = false;
        }

        void OnCollisionEnter(Collision collision)
        {
            if (removeAt > 0f && desk.IsBackboard(collision.collider)) banked = true;
        }

        void FixedUpdate()
        {
            if (removeAt <= 0f) return;
            Vector3 position = body.position;
            if (!scored && desk.CrossedBinOpening(previousPosition, position))
            {
                scored = true;
                desk.Scored(banked);
            }
            previousPosition = position;
            if (Time.time >= removeAt) Destroy(gameObject);
        }

        internal static Mesh CreateCrumpledMesh()
        {
            const int rings = 8;
            const int sides = 12;
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            for (int ring = 0; ring < rings; ring++)
            {
                for (int side = 0; side < sides; side++)
                {
                    var a = new Vector2(side / (float)sides, ring / (float)rings);
                    var b = new Vector2((side + 1) / (float)sides, a.y);
                    var c = new Vector2(a.x, (ring + 1) / (float)rings);
                    var d = new Vector2(b.x, c.y);
                    if (ring > 0) AddFace(a, b, c);
                    if (ring < rings - 1) AddFace(b, d, c);
                }
            }
            var mesh = new Mesh { name = "Faceted crumpled paper" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            return mesh;

            void AddFace(Vector2 a, Vector2 b, Vector2 c)
            {
                Vector3 p = Shape(a), q = Shape(b), r = Shape(c);
                int i = vertices.Count;
                vertices.Add(p);
                vertices.Add(q);
                vertices.Add(r);
                uv.Add(a);
                uv.Add(b);
                uv.Add(c);
                bool outward = Vector3.Dot(Vector3.Cross(q - p, r - p), p + q + r) > 0;
                triangles.Add(i);
                triangles.Add(outward ? i + 1 : i + 2);
                triangles.Add(outward ? i + 2 : i + 1);
            }

            static Vector3 Shape(Vector2 uv)
            {
                float latitude = Mathf.PI * uv.y;
                float longitude = 2f * Mathf.PI * uv.x;
                Vector3 direction = new(Mathf.Sin(latitude) * Mathf.Cos(longitude),
                    Mathf.Cos(latitude), Mathf.Sin(latitude) * Mathf.Sin(longitude));
                float folds = Mathf.Sin(Vector3.Dot(direction, new Vector3(15.8f, 8.4f, -12.7f)));
                return direction * (BallRadius * (0.91f + 0.12f * folds));
            }
        }

        void OnDestroy()
        {
            Destroy(drawing);
            Destroy(material);
        }
    }
}
