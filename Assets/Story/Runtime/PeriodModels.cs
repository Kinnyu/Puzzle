using System;
using UnityEngine;

namespace Puzzle.Story
{
    [Serializable]
    internal sealed class PeriodModels
    {
        public Material pigment;
        public Mesh poplar, cypress, almond, sarcophagus;
        public Mesh chair, easel, palette, brushes, paints;
        public Mesh mineLamp, oilLamp, pendant, coffeeCup, platter, stewPot, crop;
        public Mesh book, cloth, loaf, basket, coffeePot, cartWheel, leafLitter;

        internal GameObject Place(Mesh mesh, Transform parent, Vector3 position, string name = null,
            Material material = null)
        {
            var model = new GameObject(name ?? mesh.name, typeof(MeshFilter), typeof(MeshRenderer));
            model.transform.SetParent(parent, false);
            model.transform.position = position;
            model.GetComponent<MeshFilter>().sharedMesh = mesh;
            model.GetComponent<MeshRenderer>().sharedMaterial = material != null ? material : pigment;
            return model;
        }
    }
}
