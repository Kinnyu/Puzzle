using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Puzzle.Story
{
    internal sealed class ChapterFourScenery : IDisposable
    {
        readonly Transform root;
        readonly Func<Vector3, Vector3> ground;
        readonly List<Material> materials = new();
        readonly Material paintedProps;
        readonly PeriodModels models;
        readonly Material timber, linen, leaf;
        internal readonly Vector3 Clinic, Gallery, House, Cypress, Gift;
        internal readonly Vector3[] Inspiration, Paintings, Display, Tools;
        internal readonly GameObject[] Copies = new GameObject[3], Exhibits = new GameObject[3],
            Supplies = new GameObject[4], Setup = new GameObject[3];
        internal readonly GameObject[] Frames = new GameObject[3], Backings = new GameObject[3];
        internal Texture2D GiftPainting { get; }

        internal ChapterFourScenery(Transform parent, Func<Vector3, Vector3> ground, Vector3 clinicLot, Vector3 galleryLot,
            Vector3 house, Texture2D[] art, Material paintedProps, PeriodModels models)
        {
            this.paintedProps = paintedProps;
            this.models = models;
            this.ground = ground;
            root = new GameObject("Chapter 4 scenery").transform;
            root.SetParent(parent);
            House = house;
            Clinic = clinicLot;
            Gallery = galleryLot;
            Cypress = ground(Clinic + new Vector3(-2.85f, 0f, -3.7f));
            Gift = ground(new Vector3(House.x + 1.9f, 0f, House.z - 3.05f));
            timber = Material("Dark oak", new Color(.27f, .18f, .14f));
            linen = Material("Canvas linen", new Color(.87f, .80f, .64f));
            leaf = Material("Cypress green", new Color(.12f, .25f, .21f));
            ClinicDetails();
            SpringTree();
            GiftPainting = PaintBlossoms();

            Inspiration = new[] { Clinic + new Vector3(-1.45f, 0, -1.24f),
                ground(Cypress + Vector3.right * .7f), Clinic + new Vector3(1.51f, 0, .22f) };
            Paintings = new[] { Clinic + new Vector3(1.73f, 0, 1.12f),
                House + new Vector3(-1.38f, 0, 1.05f), House + new Vector3(1.36f, 0, 1.05f) };
            Display = new[] { Gallery + new Vector3(0f, 0, 1.0f),
                Gallery + new Vector3(-1.65f, 0, 1.0f), Gallery + new Vector3(1.65f, 0, 1.0f) };
            for (int i = 0; i < art.Length; i++)
            {
                Copies[i] = Canvas("Collectible canvas", Paintings[i] + Vector3.up * 1.20f, art[i], .95f, .72f);
                var wall = new Vector3(Display[i].x, Gallery.y + 1.61f, Gallery.z + 1.94f);
                Frames[i] = Box("Exhibit frame", wall + Vector3.forward * .03f, new Vector3(1.50f, 1.12f, .07f), timber, false);
                Backings[i] = Box("Empty exhibit", wall, new Vector3(1.38f, 1.00f, .022f), linen, false);
                Exhibits[i] = Canvas("Exhibited canvas", wall - Vector3.forward * .02f, art[i], 1.36f, .98f);
            }

            Tools = new[] { House + new Vector3(1.80f, 0, .78f), House + new Vector3(-.94f, 0, -.98f),
                House + new Vector3(1.77f, 0, -.95f), House + new Vector3(-1.50f, 0, .90f) };
            Supplies[0] = Box("Blank canvas", Tools[0] + Vector3.up * .54f, new Vector3(.59f, .69f, .10f), linen, false);
            Supplies[1] = models.Place(models.brushes, root, House + new Vector3(-1.70f, .922f, -.93f));
            Supplies[2] = models.Place(models.paints, root, Tools[2] + Vector3.up * .025f);
            Supplies[3] = models.Place(models.easel, root, Tools[3] + Vector3.left * .32f);
            Vector3 stand = ground(Gift + new Vector3(.76f, 0, .38f));
            Setup[0] = models.Place(models.easel, root, stand);
            Setup[1] = Canvas("Spring canvas", stand + new Vector3(0f, 1.16f, -.19f), null, .69f, .53f);
            Setup[2] = models.Place(models.palette, root, stand + new Vector3(-.15f, .824f, -.047f));
        }

        void ClinicDetails()
        {
            Box("Clinic cot base", Clinic + new Vector3(-1.85f, .25f, 1.22f),
                new Vector3(1.16f, .5f, .82f), timber);
            Box("Clinic blanket", Clinic + new Vector3(-1.85f, .55f, 1.22f),
                new Vector3(1.16f, .10f, .82f), linen, false);
            models.Place(models.easel, root, Clinic + new Vector3(2.05f, 0, .31f));
            Box("Window sill", Clinic + new Vector3(-1.78f, 1.21f, -2.03f),
                new Vector3(.83f, .09f, .16f), timber, false);
            Box("Window interior", Clinic + new Vector3(-1.78f, 1.68f, -2.07f),
                new Vector3(.66f, .8f, .025f), leaf, false);
            Plant(models.cypress, Cypress, .17f);
        }

        void SpringTree() => Plant(models.almond, ground(Gift + new Vector3(.97f, 0, .96f)), .13f);

        void Plant(Mesh mesh, Vector3 foot, float radius)
        {
            var tree = models.Place(mesh, root, foot);
            var trunk = tree.AddComponent<CapsuleCollider>();
            trunk.center = Vector3.up * .83f;
            trunk.height = 1.66f;
            trunk.radius = radius;
        }

        internal void FinishCanvas() => Setup[1].GetComponent<Renderer>().sharedMaterial.SetTexture("_BaseMap", GiftPainting);

        internal GameObject PlaceLetter(Vector3 position)
        {
            var stand = new GameObject("Gauguin's letter");
            stand.transform.SetParent(root);
            stand.transform.position = position;
            var stone = Box("Letter rest", position + Vector3.up * .29f,
                new Vector3(.43f, .58f, .36f), timber, false);
            var envelope = Box("Envelope", position + new Vector3(0, .60f, -.018f),
                new Vector3(.25f, .034f, .17f), linen, false);
            stone.transform.SetParent(stand.transform, true);
            envelope.transform.SetParent(stand.transform, true);
            return stand;
        }

        GameObject Canvas(string name, Vector3 position, Texture image, float width, float height)
        {
            var material = Material(name, Color.white, true);
            material.SetTexture("_BaseMap", image);
            return Part(PrimitiveType.Quad, name, position, new Vector3(width, height, 1), material, false);
        }

        GameObject Box(string name, Vector3 position, Vector3 scale, Material material, bool solid = true) =>
            Part(PrimitiveType.Cube, name, position, scale, material, solid);

        GameObject Part(PrimitiveType type, string name, Vector3 position, Vector3 scale, Material material, bool solid)
        {
            var piece = GameObject.CreatePrimitive(type);
            piece.name = name;
            piece.transform.SetParent(root);
            piece.transform.position = position;
            piece.transform.localScale = scale;
            piece.GetComponent<Renderer>().sharedMaterial = material;
            if (!solid) piece.GetComponent<Collider>().enabled = false;
            return piece;
        }

        Material Material(string name, Color color, bool unlit = false)
        {
            var material = unlit ? new Material(Shader.Find("Universal Render Pipeline/Unlit")) : new Material(paintedProps);
            material.name = name;
            material.SetColor("_BaseColor", color);
            materials.Add(material);
            return material;
        }

        static Texture2D PaintBlossoms()
        {
            const int width = 440, height = 320;
            var pixels = new Color32[width * height];
            var rng = new System.Random(1890);
            for (int i = 0; i < pixels.Length; i++)
            {
                int tint = rng.Next(-12, 12);
                pixels[i] = new Color32((byte)(77 + tint), (byte)(146 + tint), (byte)(156 + tint), 255);
            }
            void Dab(Vector2 point, int rx, int ry, Color32 color)
            {
                for (int y = -ry; y <= ry; y++)
                for (int x = -rx; x <= rx; x++)
                {
                    if ((float)(x * x) / (rx * rx) + (float)(y * y) / (ry * ry) > 1f) continue;
                    int px = Mathf.RoundToInt(point.x) + x, py = Mathf.RoundToInt(point.y) + y;
                    if ((uint)px < width && (uint)py < height) pixels[py * width + px] = color;
                }
            }
            void Branch(Vector2 from, Vector2 to, int thickness)
            {
                int count = Mathf.CeilToInt(Vector2.Distance(from, to));
                for (int i = 0; i <= count; i++)
                {
                    var point = Vector2.Lerp(from, to, i / (float)count);
                    Dab(point, thickness, thickness, new Color32(66, 74, 56, 255));
                    Dab(point + Vector2.up * 2f, Mathf.Max(1, thickness / 3), 1, new Color32(139, 140, 104, 255));
                }
            }
            var trunk = new Vector2(204f, 151f);
            Branch(new Vector2(423, -19), new Vector2(318, 76), 12);
            Branch(new Vector2(318, 76), trunk, 9);
            Branch(trunk, new Vector2(52, 263), 6);
            for (int b = 0; b < 12; b++)
            {
                var from = Vector2.Lerp(new Vector2(355, 49), new Vector2(71, 249), (b + 1f) / 13f);
                var to = from + new Vector2(rng.Next(-81, 53), rng.Next(29, 83) * (b % 4 == 0 ? -1 : 1));
                Branch(from, to, rng.Next(2, 5));
                for (int f = 0; f < 6; f++)
                {
                    var center = Vector2.Lerp(from, to, (f + 1f) / 6f) + new Vector2(rng.Next(-10, 11), rng.Next(-7, 8));
                    for (int petal = 0; petal < 5; petal++)
                    {
                        float a = petal * (Mathf.PI * 2f / 5f);
                        Dab(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 5.5f, 5, 4,
                            petal % 2 == 0 ? new Color32(243, 218, 203, 255) : new Color32(255, 239, 217, 255));
                    }
                    Dab(center, 3, 2, new Color32(202, 144, 142, 255));
                }
            }
            var portrait = new Texture2D(width, height, TextureFormat.RGBA32, false) { name = "Gift of Spring" };
            portrait.SetPixels32(pixels);
            portrait.Apply(false, true);
            return portrait;
        }

        public void Dispose()
        {
            foreach (var material in materials) Object.Destroy(material);
            Object.Destroy(GiftPainting);
            if (root != null) Object.Destroy(root.gameObject);
        }
    }
}
