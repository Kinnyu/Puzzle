using UnityEngine;

namespace Puzzle.Story
{
    internal static class StoryPropInk
    {
        static Material ink;
        static readonly int Center = Shader.PropertyToID("_InkCenter");
        static readonly MaterialPropertyBlock outlineProperties = new();

        // Story props use single-submesh meshes. Unity redraws the last submesh
        // with the additional material, keeping the original renderer and collider.
        internal static void SetSurface(GameObject item, Material surface)
        {
            if (ink == null) ink = Resources.Load<Material>("StarryNight/Prop Ink");
            var renderer = item.GetComponent<MeshRenderer>();
            renderer.sharedMaterials = new[] { surface, ink };
            var bounds = item.GetComponent<MeshFilter>().sharedMesh.bounds;
            Vector3 center = bounds.center;
            bool flat = bounds.size.z < .00001f;
            outlineProperties.SetVector(Center, new Vector4(center.x, center.y, center.z, flat ? 1 : 0));
            renderer.SetPropertyBlock(outlineProperties, 1);
        }
    }
}
