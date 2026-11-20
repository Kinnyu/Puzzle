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
        Material vincentCanvas;
        internal Vector3 House { get; }
        internal Vector3 Door => House + new Vector3(0f, 0f, -2.05f);
        internal Vector3 Cemetery { get; }
        internal Vector3 Lookout => Cemetery + Vector3.forward * 1.8f;
        internal Vector3 Leaves { get; }
        internal Vector3 Stone { get; }

        internal ChapterThreeScenery(Transform parent, Func<Vector3, Vector3> ground, Vector3 house,
            Vector3 cemetery, Texture2D gauguin)
        {
            this.ground = ground;
            root = new GameObject("Chapter 3 scenery").transform;
            root.SetParent(parent);
            House = ground(house) + Vector3.up * .12f;
            Cemetery = ground(cemetery);
            Leaves = ground(Cemetery + new Vector3(1.15f, 0f, -3f));
            Stone = ground(Cemetery + new Vector3(-.65f, 0f, -6f));
            BuildHouse(gauguin);
            BuildCemetery();
        }

        void BuildHouse(Texture2D gauguin)
        {
            var plaster = Surface("Ochre plaster", new Color(.78f, .55f, .24f));
            var tiles = Surface("Terracotta", new Color(.37f, .15f, .11f));
            var walnut = Surface("Walnut", new Color(.26f, .16f, .13f));
            var trim = Surface("Shutter green", new Color(.16f, .29f, .24f));
            var interior = Surface("Amber window", new Color(.92f, .72f, .36f), true);
            var wallStone = Surface("Foundation", new Color(.43f, .38f, .31f));
            Box("Yellow House floor", House + Vector3.down * .07f, new Vector3(5.25f, .14f, 3.95f), wallStone);
            HouseBox("Rear wall", 0, 1.48f, 1.98f, 5.25f, 2.96f, .16f, plaster);
            HouseBox("West wall", -2.55f, 1.48f, 0, .16f, 2.96f, 3.95f, plaster);
            HouseBox("East wall", 2.55f, 1.48f, 0, .16f, 2.96f, 3.95f, plaster);
            for (int side = -1; side <= 1; side += 2)
            {
                HouseBox("Door side", side * 1.66f, 1.48f, -1.98f, 1.82f, 2.96f, .16f, plaster);
                HouseBox("Window frame", side * 1.71f, 1.75f, -2.075f, .87f, .99f, .07f, trim, false);
                HouseBox("Lit window", side * 1.71f, 1.75f, -2.119f, .67f, .77f, .015f, interior, false);
                HouseBox("Window mullion", side * 1.71f, 1.75f, -2.134f, .044f, .77f, .019f, trim, false);
                HouseBox("Window transom", side * 1.71f, 1.75f, -2.134f, .67f, .045f, .019f, trim, false);
                var roof = HouseBox("Roof tiles", 0, 3.42f, side * .97f, 5.62f, .19f, 2.25f, tiles, false);
                roof.transform.rotation = Quaternion.Euler(side * 24f, 0f, 0f);
            }
            HouseBox("Roof ridge", 0, 3.87f, 0, 5.6f, .14f, .22f, tiles, false);
            HouseBox("Lintel", 0, 2.61f, -1.98f, 1.51f, .70f, .16f, plaster);
            HouseBox("Welcome mat", 0, .012f, -1.58f, 1.03f, .02f, .59f, trim, false);
            HouseBox("Writing desk", -1.76f, .42f, -.92f, .92f, .84f, .63f, walnut);
            HouseBox("Desk top", -1.76f, .86f, -.92f, 1.04f, .08f, .76f, walnut, false);
            HouseBox("Chair seat", -1.71f, .46f, -.3f, .43f, .10f, .43f, trim, false);
            HouseBox("Chair back", -1.71f, .69f, -.1f, .43f, .45f, .08f, trim, false);
            for (int side = -1; side <= 1; side += 2)
            {
                HouseBox("Chair leg", -1.71f + side * .16f, .22f, -.46f, .06f, .44f, .06f, trim, false);
                HouseBox("Chair leg", -1.71f + side * .16f, .22f, -.14f, .06f, .44f, .06f, trim, false);
            }
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

        void BuildCemetery()
        {
            var gravel = Surface("Alyscamps gravel", new Color(.49f, .37f, .25f));
            var stone = Surface("Weathered limestone", new Color(.47f, .45f, .40f));
            var bark = Surface("Poplar bark", new Color(.26f, .17f, .12f));
            var copper = Surface("Copper leaves", new Color(.73f, .36f, .09f));
            var gold = Surface("Golden leaves", new Color(.91f, .60f, .17f));
            var leaf = Surface("Fallen leaves", new Color(.91f, .56f, .16f));
            for (int i = 0; i < 8; i++)
            {
                var path = ground(Cemetery + Vector3.back * (i - 1) * 1.28f);
                Box("Gravel lane", path + Vector3.up * .018f, new Vector3(2.4f, .024f, 1.30f), gravel, false);
            }
            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < 4; i++)
                {
                    var tree = ground(Cemetery + new Vector3(side * (1.87f + (i % 2) * .25f), 0, -.7f - i * 2.33f));
                    Primitive(PrimitiveType.Cylinder, "Poplar trunk", tree + Vector3.up * 1.40f,
                        new Vector3(.33f, 1.4f, .33f), bark, true);
                    Primitive(PrimitiveType.Sphere, "Autumn crown", tree + new Vector3(0, 3.6f, 0),
                        new Vector3(1.13f, 3.05f, 1.29f), copper, false);
                    Primitive(PrimitiveType.Sphere, "Golden crown", tree + new Vector3(side * -.24f, 3.5f, -.12f),
                        new Vector3(.99f, 2.55f, 1.16f), gold, false);
                }
            }
            for (int i = 0; i < 3; i++)
            {
                var tomb = ground(Cemetery + new Vector3(-1.88f, 0f, -3.5f - 2.31f * i));
                Box("Sarcophagus", tomb + Vector3.up * .30f, new Vector3(.76f, .61f, 1.62f), stone);
                Box("Stone lid", tomb + Vector3.up * .64f, new Vector3(.86f, .16f, 1.73f), stone, false);
            }
            var scatter = new System.Random(1888);
            for (int i = 0; i < 30; i++)
            {
                var point = ground(Cemetery + new Vector3((float)scatter.NextDouble() * 3.3f - 1.65f, 0,
                    -(float)scatter.NextDouble() * 8.4f));
                var fallen = Box("Fallen leaf", point + Vector3.up * .036f, new Vector3(.11f, .012f, .19f), leaf, false);
                fallen.transform.rotation = Quaternion.Euler(0, scatter.Next(180), 0);
            }
        }

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
            var shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
            var material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", color);
            if (!unlit) material.SetFloat("_Smoothness", .11f);
            materials.Add(material);
            return material;
        }

        internal Texture2D Study(PaintingStudy elements)
        {
            int index = (int)elements;
            return studies[index] != null ? studies[index] : studies[index] = Paint(elements);
        }

        internal void HangStudy() => vincentCanvas.SetTexture("_BaseMap", Study(PaintingStudy.All));

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
            foreach (Texture2D study in studies) if (study != null) Object.Destroy(study);
            if (root != null) Object.Destroy(root.gameObject);
        }
    }
}
