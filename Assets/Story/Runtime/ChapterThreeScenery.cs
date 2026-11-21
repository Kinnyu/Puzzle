using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Puzzle.Story
{
    [Flags]
    internal enum PaintingStudy { None = 0, Perspective = 1, Leaves = 2, Stone = 4, All = 7 }

    internal sealed class ChapterThreeScenery : IDisposable
    {
        readonly Func<Vector3, Vector3> ground;
        readonly Transform root;
        readonly List<Material> materials = new();
        readonly Texture2D[] studies = new Texture2D[8];
        readonly Material paintedProps;
        readonly PeriodModels models;
        Material vincentCanvas;
        internal Vector3 House { get; }
        internal Vector3 Door => House + new Vector3(0f, 0f, -2.05f);
        internal Vector3 Cemetery { get; }
        internal Vector3 Lookout => Cemetery + Vector3.forward * 1.8f;
        internal Vector3 Leaves { get; }
        internal Vector3 Stone { get; }

        internal ChapterThreeScenery(Transform parent, Func<Vector3, Vector3> ground, Vector3 house,
            Vector3 cemetery, Texture2D gauguin, Material paintedProps, PeriodModels models)
        {
            this.paintedProps = paintedProps;
            this.models = models;
            this.ground = ground;
            root = new GameObject("Chapter 3 scenery").transform;
            root.SetParent(parent);
            House = house;
            Cemetery = ground(cemetery);
            Leaves = ground(Cemetery + new Vector3(1.15f, 0f, -3f));
            Stone = ground(Cemetery + new Vector3(-.65f, 0f, -6f));
            FurnishStudio(gauguin);
            BuildCemetery();
        }

        void FurnishStudio(Texture2D gauguin)
        {
            var walnut = Surface("Walnut", new Color(.26f, .16f, .13f));
            var trim = Surface("Shutter green", new Color(.16f, .29f, .24f));
            var interior = Surface("Drawing paper", new Color(.92f, .82f, .63f));
            HouseBox("Welcome mat", 0, .012f, -1.58f, 1.03f, .02f, .59f, trim, false);
            HouseBox("Writing desk", -1.76f, .42f, -.92f, .92f, .84f, .63f, walnut);
            HouseBox("Desk top", -1.76f, .86f, -.92f, 1.04f, .08f, .76f, walnut, false);
            models.Place(models.chair, root, House + new Vector3(-1.71f, 0, -.30f));
            HouseBox("Stack of papers", -1.76f, .914f, -.96f, .24f, .027f, .33f, interior, false);
            var lamp = new GameObject("Studio light").AddComponent<Light>();
            lamp.transform.SetParent(root);
            lamp.transform.position = House + new Vector3(0f, 2.52f, -.24f);
            lamp.type = LightType.Point;
            lamp.color = new Color(1f, .78f, .46f);
            lamp.range = 4.8f;
            lamp.intensity = 1.65f;

            vincentCanvas = FramedCanvas(-1.22f, walnut, null);
            FramedCanvas(1.22f, walnut, gauguin, .88f, 1.12f);
        }

        Material FramedCanvas(float x, Material wood, Texture image, float width = 1.12f, float height = .86f)
        {
            HouseBox("Painting frame", x, 1.79f, 1.865f, width + .10f, height + .10f, .05f, wood, false);
            var material = Surface("Painted canvas", Color.white, true);
            material.SetTexture("_BaseMap", image);
            var canvas = GameObject.CreatePrimitive(PrimitiveType.Quad);
            canvas.name = "Painted canvas";
            canvas.transform.SetParent(root);
            canvas.transform.position = House + new Vector3(x, 1.79f, 1.828f);
            canvas.transform.localScale = new Vector3(width, height, 1f);
            canvas.GetComponent<Collider>().enabled = false;
            canvas.GetComponent<Renderer>().sharedMaterial = material;
            return material;
        }

        Material gravel, stone, poplar, leaf;
        Mesh cemeteryLane;

        void LayAvenue()
        {
            // Sample both verges and the centre so the autumn walk follows the existing ground.
            const int strips = 24;
            var vertices = new Vector3[(strips + 1) * 3];
            var uv = new Vector2[vertices.Length];
            var indices = new int[strips * 12];
            for (int row = 0; row <= strips; row++)
            {
                float z = 2.55f - row * .62f;
                for (int col = 0; col < 3; col++)
                {
                    float x = (col - 1) * 1.22f;
                    int k = row * 3 + col;
                    vertices[k] = ground(Cemetery + new Vector3(x, 0, z)) - Cemetery + Vector3.up * .02f;
                    uv[k] = new Vector2(x, z);
                    if (row == strips || col == 2) continue;
                    int t = (row * 2 + col) * 6;
                    indices[t] = k; indices[t + 1] = k + 1; indices[t + 2] = k + 3;
                    indices[t + 3] = k + 1; indices[t + 4] = k + 4; indices[t + 5] = k + 3;
                }
            }
            cemeteryLane = new Mesh { name = "Alyscamps avenue", vertices = vertices, uv = uv, triangles = indices };
            cemeteryLane.RecalculateNormals();
            models.Place(cemeteryLane, root, Cemetery, material: gravel);

            for (int row = 0; row < strips; row += 2)
            {
                int k = row * 3;
                Vector3 across = vertices[k + 2] - vertices[k];
                Vector3 along = vertices[k + 7] - vertices[k + 1];
                var leaves = models.Place(models.leafLitter, root, Cemetery + vertices[k + 4] + Vector3.up * .012f, material: leaf);
                leaves.transform.rotation = Quaternion.FromToRotation(Vector3.up, Vector3.Cross(across, along).normalized)
                    * Quaternion.Euler(0, row % 4 == 0 ? 0 : 180, 0);
            }
        }

        void BuildCemetery()
        {
            gravel = Surface("Alyscamps gravel", new Color(.49f, .37f, .25f));
            stone = new Material(models.pigment);
            poplar = new Material(models.pigment);
            materials.Add(stone);
            materials.Add(poplar);
            leaf = new Material(models.pigment);
            materials.Add(leaf);
            LayAvenue();
            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < 4; i++)
                {
                    var tree = ground(Cemetery + new Vector3(side * (1.87f + (i % 2) * .25f), 0, -.7f - i * 2.33f));
                    var poplarTree = models.Place(models.poplar, root, tree, "Alyscamps poplar", poplar);
                    poplarTree.transform.rotation = Quaternion.Euler(0, (side * 41 + i * 73), 0);
                    poplarTree.transform.localScale = Vector3.one * (1f + .04f * ((i + side + 1) % 3));
                    var trunk = poplarTree.AddComponent<CapsuleCollider>();
                    trunk.center = Vector3.up * 1.35f;
                    trunk.height = 2.7f;
                    trunk.radius = .17f;
                }
            }
            for (int i = 0; i < 3; i++)
            {
                var tomb = ground(Cemetery + new Vector3(-2.70f, 0f, -1.84f - 2.33f * i));
                var trough = models.Place(models.sarcophagus, root, tomb, material: stone);
                var body = trough.AddComponent<BoxCollider>();
                body.center = Vector3.up * .25f;
                body.size = new Vector3(.76f, .50f, 1.66f);
            }
        }

        internal void UpdateStudyColors(PaintingStudy elements, float blend)
        {
            if ((elements & PaintingStudy.Perspective) != 0)
                EaseColor(gravel, new Color(.70f, .47f, .24f), blend);
            if ((elements & PaintingStudy.Stone) != 0)
                EaseColor(stone, new Color(.93f, .85f, 1f), blend);
            if ((elements & PaintingStudy.Leaves) != 0)
            {
                EaseColor(poplar, new Color(1f, .86f, .65f), blend);
                EaseColor(leaf, new Color(1f, .88f, .64f), blend);
            }
        }

        static void EaseColor(Material material, Color target, float blend) =>
            material.color = Color.Lerp(material.color, target, blend);

        GameObject HouseBox(string name, float x, float y, float z, float width, float height, float depth,
            Material material, bool solid = true) => Box(name, House + new Vector3(x, y, z),
            new Vector3(width, height, depth), material, solid);

        GameObject Box(string name, Vector3 position, Vector3 size, Material material, bool solid = true) =>
            Primitive(PrimitiveType.Cube, name, position, size, material, solid);

        GameObject Primitive(PrimitiveType type, string name, Vector3 position, Vector3 size, Material material, bool solid)
        {
            var item = GameObject.CreatePrimitive(type);
            item.name = name;
            item.transform.SetParent(root);
            item.transform.position = position;
            item.transform.localScale = size;
            item.GetComponent<Renderer>().sharedMaterial = material;
            if (!solid) item.GetComponent<Collider>().enabled = false;
            return item;
        }

        Material Surface(string name, Color color, bool unlit = false)
        {
            var material = unlit ? new Material(Shader.Find("Universal Render Pipeline/Unlit")) : new Material(paintedProps);
            material.name = name;
            material.SetColor("_BaseColor", color);
            materials.Add(material);
            return material;
        }

        internal Texture2D Study(PaintingStudy elements)
        {
            int index = (int)elements;
            return studies[index] != null ? studies[index] : studies[index] = Paint(elements);
        }

        internal void HangStudy(PaintingStudy elements) => vincentCanvas.SetTexture("_BaseMap", Study(elements));

        static Texture2D Paint(PaintingStudy elements)
        {
            const int width = 320, height = 256;
            var pixels = new Color32[width * height];
            var random = new System.Random(1888);
            var sky = new Color32(113, 147, 141, 255);
            var field = new Color32(137, 107, 61, 255);
            var road = new Color32(193, 147, 86, 255);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float spread = Mathf.Lerp(132f, 7f, y / 154f);
                    bool onRoad = y < 154 && Mathf.Abs(x - (146f + .15f * y)) < spread &&
                        (elements & PaintingStudy.Perspective) != 0;
                    Color32 baseColor = y >= 157 ? sky : onRoad ? road : field;
                    int shade = random.Next(-10, 11);
                    pixels[y * width + x] = new Color32((byte)(baseColor.r + shade),
                        (byte)(baseColor.g + shade), (byte)(baseColor.b + shade), 255);
                }
            }

            void Stroke(float x, float y, int w, int h, Color32 color, float tilt = 0f)
            {
                for (int v = -h; v <= h; v++)
                {
                    for (int u = -w; u <= w; u++)
                    {
                        if (u * u / (float)(w * w) + v * v / (float)(h * h) > 1f) continue;
                        int tx = Mathf.RoundToInt(x + u + v * tilt), ty = Mathf.RoundToInt(y + v);
                        if ((uint)tx < width && (uint)ty < height) pixels[ty * width + tx] = color;
                    }
                }
            }
            for (int i = 0; i < 1650; i++)
            {
                int x = random.Next(width), y = random.Next(height);
                var pigment = pixels[y * width + x];
                Stroke(x, y, random.Next(1, 5), random.Next(1, 3), pigment, -.55f);
            }
            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 5; i >= 0; i--)
                {
                    float depth = i / 6f;
                    float x = 155f + side * Mathf.Lerp(131, 18, depth);
                    float y = Mathf.Lerp(38, 137, depth);
                    int trunkHeight = Mathf.RoundToInt(Mathf.Lerp(110, 30, depth));
                    Stroke(x, y + trunkHeight * .49f, Mathf.Max(2, 7 - i), trunkHeight / 2,
                        new Color32(69, 50, 43, 255), side * -.11f);
                    for (int leaf = 0; leaf < 62; leaf++)
                    {
                        int amber = random.Next(12, 56);
                        Color32 color = (elements & PaintingStudy.Leaves) != 0
                            ? new Color32((byte)(168 + amber), (byte)(81 + amber), 37, 255)
                            : new Color32((byte)(73 + amber), (byte)(90 + amber), 72, 255);
                        float leafX = x + random.Next(-32, 33) * (1 - depth);
                        float leafY = y + trunkHeight + random.Next(-32, 33) * (1 - depth);
                        Stroke(leafX, leafY, random.Next(2, 7), random.Next(2, 5), color, .7f);
                    }
                }
            }
            if ((elements & PaintingStudy.Stone) != 0)
            {
                for (int i = 3; i >= 0; i--)
                {
                    Stroke(48 + 28 * i, 37 + 26 * i, 26 - 5 * i, 11 - 2 * i,
                        new Color32(95, 91, 84, 255), 1.1f);
                    Stroke(46 + 28 * i, 46 + 25 * i, 25 - 5 * i, 7 - i,
                        new Color32(186, 169, 136, 255), 1.4f);
                }
            }
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { name = $"Alyscamps study {elements}" };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        public void Dispose()
        {
            foreach (Material material in materials) Object.Destroy(material);
            if (cemeteryLane != null) Object.Destroy(cemeteryLane);
            foreach (Texture2D study in studies) if (study != null) Object.Destroy(study);
            if (root != null) Object.Destroy(root.gameObject);
        }
    }
}
