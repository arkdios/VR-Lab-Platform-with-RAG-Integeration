#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace PhysicsLab.Lab.EditorTools
{
    /// <summary>
    /// Real-shape colliders instead of boxes, for the force table and the chair.
    /// Table: it never moves, so it gets ONE exact mesh collider on its root, built from every drawn part of its model.
    /// Keeping one collider on the root matters: LabStringBuilder reads table.GetComponent&lt;Collider&gt;() to tell the
    /// ring, the scale and the masses to ignore the table.
    /// Chair: it can be grabbed, so it has a Rigidbody. A moving body may only use convex shapes, so each part of
    /// the chair gets its own convex mesh collider. Together they follow the chair, without dents.
    /// Used by LabPhysicsRepair (table) and LabPropsBuilder (chair).
    /// </summary>
    internal static class LabMeshColliders
    {
        private const string MeshFolder = "Assets/Experiences/PhyscicsLab/Models/Proxies";

        /// <summary>
        /// Replaces every collider under root with one exact mesh collider on root. Returns false (and changes
        /// nothing) when the model draws no mesh, so the caller can keep its old collider.
        /// </summary>
        public static bool ReplaceWithOneMesh(GameObject root, string meshName, StringBuilder log)
        {
            List<MeshFilter> parts = DrawnMeshes(root);
            if (parts.Count == 0)
            {
                log.AppendLine($"  Warning: {root.name} has no drawn mesh, so no mesh collider was made.");
                return false;
            }

            Mesh combined = BuildCombinedMesh(root.transform, parts, meshName);
            RemoveAllColliders(root);
            MeshCollider collider = Undo.AddComponent<MeshCollider>(root);
            collider.sharedMesh = combined;
            collider.convex = false;   // fine for a part that never moves
            log.AppendLine($"  {root.name}: one exact mesh collider from {parts.Count} part(s), {combined.triangles.Length / 3} triangles. The old collider is gone.");
            return true;
        }

        /// <summary>
        /// Replaces every collider under root with one convex mesh collider per drawn part.
        /// Returns how many old colliders were removed. Changes nothing when the model draws no mesh.
        /// </summary>
        public static int ReplaceWithConvexParts(GameObject root, StringBuilder log)
        {
            List<MeshFilter> parts = DrawnMeshes(root);
            if (parts.Count == 0)
            {
                log.AppendLine($"  Warning: {root.name} has no drawn mesh, so no convex collider was made.");
                return 0;
            }

            int removed = RemoveAllColliders(root);
            foreach (MeshFilter part in parts)
            {
                MakeReadable(part.sharedMesh);
                MeshCollider collider = Undo.AddComponent<MeshCollider>(part.gameObject);
                collider.sharedMesh = part.sharedMesh;   // read again: a re-import can replace the mesh object
                collider.convex = true;
            }
            log.AppendLine($"  {root.name}: {parts.Count} convex mesh collider(s), the real shape without dents.");
            return removed;
        }

        // The parts of a model that are drawn. The invisible proxy on the table root has no material, so it is skipped.
        private static List<MeshFilter> DrawnMeshes(GameObject root)
        {
            var parts = new List<MeshFilter>();
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>())   // skips switched-off objects
            {
                Renderer drawn = filter.GetComponent<Renderer>();
                bool isDrawn = drawn != null && drawn.sharedMaterials.Length > 0;
                if (filter.sharedMesh != null && isDrawn) parts.Add(filter);
            }
            return parts;
        }

        // One mesh in the root's own space, saved as an asset so the scene can point at it.
        private static Mesh BuildCombinedMesh(Transform root, List<MeshFilter> parts, string meshName)
        {
            var instances = new CombineInstance[parts.Count];
            for (int i = 0; i < parts.Count; i++)
            {
                MakeReadable(parts[i].sharedMesh);
                instances[i].mesh = parts[i].sharedMesh;   // read again after the re-import
                instances[i].transform = root.worldToLocalMatrix * parts[i].transform.localToWorldMatrix;
            }

            string path = $"{MeshFolder}/{meshName}.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool isNew = mesh == null;
            if (isNew) mesh = new Mesh { name = meshName };

            mesh.Clear();
            mesh.indexFormat = IndexFormat.UInt32;   // room for a big model
            mesh.CombineMeshes(instances, true, true);
            if (isNew) AssetDatabase.CreateAsset(mesh, path); else EditorUtility.SetDirty(mesh);
            return mesh;
        }

        // A mesh collider in a build needs a readable mesh. This switches the model file to Read/Write if it is not.
        private static void MakeReadable(Mesh mesh)
        {
            string path = AssetDatabase.GetAssetPath(mesh);
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null || importer.isReadable) return;

            importer.isReadable = true;
            importer.SaveAndReimport();
        }

        private static int RemoveAllColliders(GameObject root)
        {
            Collider[] old = root.GetComponentsInChildren<Collider>(true);
            foreach (Collider collider in old) Undo.DestroyObjectImmediate(collider);
            return old.Length;
        }
    }
}
#endif