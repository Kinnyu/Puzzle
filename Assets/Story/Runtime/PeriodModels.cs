using System;
using UnityEngine;

namespace Puzzle.Story
{
    [Serializable]
    internal sealed class PeriodModels
    {
        internal const float EaselLedge = .816f;
        public Material pigment;
        public Mesh poplar, cypress, almond, sarcophagus;
        public Mesh chair, easel, palette, brushes, paints;
        public Mesh mineLamp, oilLamp, pendant, coffeeCup, platter, stewPot, crop;
        public Mesh book, cloth, loaf, basket, coffeePot, cartWheel, leafLitter;

        internal static void RestOnSurface(GameObject item, float surfaceHeight)
        {
            var bottom = item.GetComponent<Renderer>().bounds.min.y;
            item.transform.position += Vector3.up * (surfaceHeight - bottom);
        }

        internal GameObject Place(Mesh mesh, Transform parent, Vector3 position, string name = null,
            Material material = null, bool outline = true)
        {
            var model = new GameObject(name ?? mesh.name, typeof(MeshFilter), typeof(MeshRenderer));
            model.transform.SetParent(parent, false);
            model.transform.position = position;
            model.GetComponent<MeshFilter>().sharedMesh = mesh;
            var surface = material != null ? material : pigment;
            if (outline) StoryPropInk.SetSurface(model, surface);
            else model.GetComponent<MeshRenderer>().sharedMaterial = surface;
            return model;
        }
    }
}
