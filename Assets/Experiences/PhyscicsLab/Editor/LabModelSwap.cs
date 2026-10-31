#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace PhysicsLab.Lab.EditorTools
{
    /// <summary>
    /// Menu: Lab > 5 Swap in real models. Puts the real models (force table, ring, pulley clamp, hooked masses)
    /// on the parts that still use cylinders and cubes.
    ///
    /// How: every part keeps its object, its Rigidbody and its scripts. Its own mesh becomes an invisible box with
    /// the size of the real model (the proxy), and the real model is added as a child. The builders (Lab > 0)
    /// read the proxy, so they place everything by the real sizes. Run this first, then Lab > 0. Safe to run again.
    /// </summary>
    public static class LabModelSwap
    {
        private const string PrefabFolder = "Assets/Experiences/PhyscicsLab/Prefabs/";
        private const string ModelsFolder = "Assets/Experiences/PhyscicsLab/Models";
        private const string ProxiesFolderName = "Proxies";
        private const string ModelName = "RealModel";
        private const float SmallestPartMetres = 0.005f;
        private const float LargestPartMetres = 5f;
        private const float SmallestTableRadiusMetres = 0.05f;
        private const float LargestTableRadiusMetres = 3f;
        private const float FlatToleranceMetres = 0.002f;
        private const float UpFacingLimit = 0.95f;
        private const float BinsPerMetre = 500f;   // table top heights are grouped in 2 mm steps

        // Turn a model here only if it was drawn lying down or facing the wrong way. Degrees around x, y and z.
        private static readonly Vector3 TableTurn = Vector3.zero;
        private static readonly Vector3 RingTurn = Vector3.zero;
        private static readonly Vector3 PulleyTurn = Vector3.zero;
        private static readonly Vector3 MassTurn = Vector3.zero;

        // Two triangles per face of a box. The vertex number's bits are x, y and z.
        private static readonly int[] BoxTriangles =
        {
            0, 2, 1, 1, 2, 3,   4, 5, 6, 5, 7, 6,   0, 1, 4, 1, 5, 4,
            2, 6, 3, 3, 6, 7,   0, 4, 2, 2, 4, 6,   1, 3, 5, 3, 7, 5
        };

        private struct TableTop
        {
            public Vector3 centre;   // middle of the top surface, in world space
            public float radius;
        }

        [MenuItem("Lab/5 Swap in real models")]
        private static void Menu()
        {
            var log = new StringBuilder("[LabModelSwap]\n");
            if (Run(log))
            {
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                log.AppendLine("Done. Next: Lab > 0 Build working lab, then save the scene.");
                Debug.Log(log.ToString());
            }
            else
            {
                Debug.LogError(log.ToString());
            }
        }

        public static bool Run(StringBuilder log)
        {
            Transform equipment = LabSceneFixes.FindSceneObject("LabEquipments");
            if (equipment == null) return Fail(log, "no object named LabEquipments.");

            EnableReadWrite("ForceTable.fbx", log);

            bool ok = SwapTable(equipment, log);
            ok &= SwapCentred(equipment.Find("Ring"), "Ring.prefab", RingTurn, log);
            ok &= SwapCentred(equipment.Find("Pulley 1"), "PulleyClamp.prefab", PulleyTurn, log);
            ok &= SwapCentred(equipment.Find("Pulley 2"), "PulleyClamp.prefab", PulleyTurn, log);
            ok &= SwapMass(equipment, "Mass 1", log);
            ok &= SwapMass(equipment, "Mass 2", log);
            return ok;
        }

        // ---------- parts ----------

        private static bool SwapTable(Transform equipment, StringBuilder log)
        {
            Transform table = equipment.Find("Table");
            if (table == null) return Fail(log, "no Table under LabEquipments.");

            Quaternion savedRotation = PrepareForMeasure(table);
            GameObject model = Spawn(table, "ForceTable.prefab", ModelName, TableTurn, log);
            if (model == null || !TryMeasure(model, "ForceTable", out Bounds all, log))
            {
                table.rotation = savedRotation;
                return false;
            }

            TableTop top = FindTop(model, all, log);
            if (top.radius < SmallestTableRadiusMetres || top.radius > LargestTableRadiusMetres)
            {
                table.rotation = savedRotation;
                return Fail(log, $"the table top radius is {top.radius:F3} m. Check the model's Scale Factor and turn.");
            }

            // The model stands on the floor, with the middle of its top over the middle of LabEquipments.
            float floorY = equipment.position.y;
            Vector3 shift = new Vector3(equipment.position.x - top.centre.x, floorY - all.min.y, equipment.position.z - top.centre.z);
            model.transform.position += shift;
            float height = top.centre.y + shift.y - floorY;

            Vector3 modelPosition = model.transform.position;
            table.position = new Vector3(equipment.position.x, floorY + height * 0.5f, equipment.position.z);
            model.transform.position = modelPosition;

            SetProxy(table, new Vector3(top.radius * 2f, height, top.radius * 2f), Vector3.zero);
            table.rotation = savedRotation;
            log.AppendLine($"  Table: ForceTable in. Top radius {top.radius:F3} m, top height {height:F3} m.");
            return true;
        }

        // For parts whose pivot must be the middle of the part (ring, pulley clamp).
        private static bool SwapCentred(Transform part, string prefabFile, Vector3 turn, StringBuilder log)
        {
            if (part == null) return Fail(log, $"a part for {prefabFile} is missing under LabEquipments.");

            Quaternion savedRotation = PrepareForMeasure(part);
            GameObject model = Spawn(part, prefabFile, ModelName, turn, log);
            if (model == null || !TryMeasure(model, prefabFile, out Bounds bounds, log))
            {
                part.rotation = savedRotation;
                return false;
            }

            model.transform.position += part.position - bounds.center;
            SetProxy(part, bounds.size, Vector3.zero);
            part.rotation = savedRotation;
            log.AppendLine($"  {part.name}: {prefabFile} in, {Describe(bounds.size)}.");
            return true;
        }

        // For the hanging masses. The pivot is the top of the hook, because that is where the string ties on.
        private static bool SwapMass(Transform equipment, string massName, StringBuilder log)
        {
            Transform mass = equipment.Find(massName);
            Pulley owner = FindOwner(equipment, mass);
            if (mass == null || owner == null) return Fail(log, $"{massName} is missing, or no pulley points at it.");

            Quaternion savedRotation = PrepareForMeasure(mass);
            GameObject small = SpawnHanging(mass, "HookedMass50g.prefab", out Bounds smallBounds, log);
            GameObject medium = SpawnHanging(mass, "HookedMass100g.prefab", out Bounds mediumBounds, log);
            GameObject large = SpawnHanging(mass, "HookedMass200g.prefab", out Bounds largeBounds, log);
            if (small == null || medium == null || large == null)
            {
                mass.rotation = savedRotation;
                return false;
            }

            SetProxy(mass, mediumBounds.size, new Vector3(0f, -mediumBounds.size.y * 0.5f, 0f));
            mass.rotation = savedRotation;

            HookedMassVisual visual = mass.GetComponent<HookedMassVisual>();
            if (visual == null) visual = Undo.AddComponent<HookedMassVisual>(mass.gameObject);
            LabSerialized.SetObject(visual, "pulley", owner);
            LabSerialized.SetObject(visual, "model50g", small);
            LabSerialized.SetObject(visual, "model100g", medium);
            LabSerialized.SetObject(visual, "model200g", large);
            HideWeightStack(mass);

            log.AppendLine($"  {massName}: hooked masses 50, 100 and 200 g in ({Describe(largeBounds.size)} for 200 g). The nearest one shows.");
            return true;
        }

        private static GameObject SpawnHanging(Transform mass, string prefabFile, out Bounds bounds, StringBuilder log)
        {
            string childName = ModelName + "_" + prefabFile.Replace("HookedMass", "").Replace(".prefab", "");
            GameObject model = Spawn(mass, prefabFile, childName, MassTurn, log);
            bounds = default(Bounds);
            if (model == null || !TryMeasure(model, prefabFile, out bounds, log)) return null;

            Vector3 topMiddle = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
            model.transform.position += mass.position - topMiddle;
            return model;
        }

        // The old weight stack is replaced by the real models.
        private static void HideWeightStack(Transform mass)
        {
            HangingMassVisual stack = mass.GetComponent<HangingMassVisual>();
            if (stack == null) return;

            Transform weights = LabSerialized.ReadReference<Transform>(stack, "weights");
            // In this scene the "weights" field points at the mass itself. Hiding it would switch off the whole
            // mass (its Rigidbody and the string end). So only hide a stack that is a separate child.
            if (weights != null && !mass.IsChildOf(weights))
            {
                Undo.RecordObject(weights.gameObject, "Hide weight stack");
                weights.gameObject.SetActive(false);
            }
            Undo.RecordObject(stack, "Stop weight stack");
            stack.enabled = false;
        }

        private static Pulley FindOwner(Transform equipment, Transform mass)
        {
            if (mass == null) return null;
            LabObject massObject = mass.GetComponent<LabObject>();
            foreach (Pulley pulley in equipment.GetComponentsInChildren<Pulley>())
            {
                if (LabSerialized.ReadReference<LabObject>(pulley, "massObject") == massObject) return pulley;
            }
            return null;
        }

        // ---------- models and measuring ----------

        private static void EnableReadWrite(string fbxFile, StringBuilder log)
        {
            var importer = AssetImporter.GetAtPath(PrefabFolder + fbxFile) as ModelImporter;
            if (importer == null || importer.isReadable) return;

            importer.isReadable = true;
            importer.SaveAndReimport();
            log.AppendLine($"  {fbxFile}: Read/Write turned on, so the table top can be found.");
        }

        // A turn and a scale on the part would bend the measuring, so both are cleared while measuring.
        private static Quaternion PrepareForMeasure(Transform part)
        {
            Undo.RecordObject(part, "Prepare part for model");
            Quaternion saved = part.rotation;
            part.rotation = Quaternion.identity;
            part.localScale = Vector3.one;
            return saved;
        }

        private static GameObject Spawn(Transform part, string prefabFile, string childName, Vector3 turn, StringBuilder log)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + prefabFile);
            if (prefab == null)
            {
                log.AppendLine($"  ERROR: no model at {PrefabFolder + prefabFile}.");
                return null;
            }

            Transform old = part.Find(childName);
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

            var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, part);
            Undo.RegisterCreatedObjectUndo(model, "Real model");
            model.name = childName;
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.Euler(turn);
            model.transform.localScale = Vector3.one;
            SetLayer(model.transform, part.gameObject.layer);
            return model;
        }

        private static void SetLayer(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            foreach (Transform child in root) SetLayer(child, layer);
        }

        private static bool TryMeasure(GameObject model, string label, out Bounds bounds, StringBuilder log)
        {
            bounds = default(Bounds);
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                log.AppendLine($"  ERROR: {label} has no mesh to measure.");
                return false;
            }

            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

            float biggest = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            if (biggest < SmallestPartMetres || biggest > LargestPartMetres)
            {
                log.AppendLine($"  ERROR: {label} is {biggest:F3} m across. That is not a lab part size. Check its Scale Factor.");
                return false;
            }
            return true;
        }

        private static string Describe(Vector3 size) => $"{size.x:F3} x {size.y:F3} x {size.z:F3} m";

        // ---------- invisible proxy ----------

        private static void SetProxy(Transform part, Vector3 size, Vector3 centre)
        {
            MeshFilter filter = part.GetComponent<MeshFilter>();
            if (filter == null) filter = Undo.AddComponent<MeshFilter>(part.gameObject);
            MeshRenderer meshRenderer = part.GetComponent<MeshRenderer>();
            if (meshRenderer == null) meshRenderer = Undo.AddComponent<MeshRenderer>(part.gameObject);

            Undo.RecordObject(filter, "Proxy mesh");
            Undo.RecordObject(meshRenderer, "Proxy renderer");
            filter.sharedMesh = GetOrCreateProxyMesh(part.name, size, centre);
            meshRenderer.sharedMaterials = new Material[0];   // nothing to draw, but the bounds stay valid
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
        }

        private static Mesh GetOrCreateProxyMesh(string partName, Vector3 size, Vector3 centre)
        {
            string folder = ModelsFolder + "/" + ProxiesFolderName;
            if (!AssetDatabase.IsValidFolder(ModelsFolder)) AssetDatabase.CreateFolder("Assets/Experiences/PhyscicsLab", "Models");
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(ModelsFolder, ProxiesFolderName);

            string path = $"{folder}/{partName}_Proxy.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool isNew = mesh == null;
            if (isNew) mesh = new Mesh { name = partName + "_Proxy" };

            FillBox(mesh, size, centre);
            if (isNew) AssetDatabase.CreateAsset(mesh, path); else EditorUtility.SetDirty(mesh);
            return mesh;
        }

        private static void FillBox(Mesh mesh, Vector3 size, Vector3 centre)
        {
            Vector3 half = size * 0.5f;
            var corners = new Vector3[8];
            for (int i = 0; i < corners.Length; i++)
            {
                corners[i] = centre + new Vector3((i & 1) == 0 ? -half.x : half.x,
                                                  (i & 2) == 0 ? -half.y : half.y,
                                                  (i & 4) == 0 ? -half.z : half.z);
            }

            mesh.Clear();
            mesh.vertices = corners;
            mesh.triangles = BoxTriangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        // ---------- table top ----------

        // The table top is the biggest flat, upward-facing surface. Legs, the post and a degree ring do not change it.
        private static TableTop FindTop(GameObject model, Bounds all, StringBuilder log)
        {
            var areaByBin = new Dictionary<int, float>();
            var pointsByBin = new Dictionary<int, List<Vector3>>();
            foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh != null && filter.sharedMesh.isReadable) CollectUpFacing(filter, areaByBin, pointsByBin);
            }

            if (areaByBin.Count == 0)
            {
                log.AppendLine("  Warning: could not read the table mesh. The outer box is used, so the pulleys may sit too far out.");
                return new TableTop { centre = new Vector3(all.center.x, all.max.y, all.center.z), radius = Mathf.Max(all.extents.x, all.extents.z) };
            }
            return MeasureTop(BestBin(areaByBin), pointsByBin);
        }

        private static void CollectUpFacing(MeshFilter filter, Dictionary<int, float> areaByBin, Dictionary<int, List<Vector3>> pointsByBin)
        {
            Mesh mesh = filter.sharedMesh;
            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            Transform t = filter.transform;

            for (int i = 0; i + 2 < triangles.Length; i += 3)
            {
                Vector3 a = t.TransformPoint(vertices[triangles[i]]);
                Vector3 b = t.TransformPoint(vertices[triangles[i + 1]]);
                Vector3 c = t.TransformPoint(vertices[triangles[i + 2]]);
                Vector3 normal = Vector3.Cross(b - a, c - a);
                float area = normal.magnitude * 0.5f;
                bool flat = Mathf.Abs(a.y - b.y) < FlatToleranceMetres && Mathf.Abs(a.y - c.y) < FlatToleranceMetres;
                if (area < 1e-9f || !flat || normal.normalized.y < UpFacingLimit) continue;

                int bin = Mathf.RoundToInt((a.y + b.y + c.y) / 3f * BinsPerMetre);
                areaByBin[bin] = (areaByBin.TryGetValue(bin, out float sum) ? sum : 0f) + area;
                if (!pointsByBin.TryGetValue(bin, out List<Vector3> points)) pointsByBin[bin] = points = new List<Vector3>();
                points.Add(a);
                points.Add(b);
                points.Add(c);
            }
        }

        // The bin (with its two neighbours) that holds the most upward-facing area.
        private static int BestBin(Dictionary<int, float> areaByBin)
        {
            int best = 0;
            float bestArea = -1f;
            foreach (int bin in areaByBin.Keys)
            {
                float area = AreaAt(areaByBin, bin - 1) + AreaAt(areaByBin, bin) + AreaAt(areaByBin, bin + 1);
                if (area <= bestArea) continue;
                bestArea = area;
                best = bin;
            }
            return best;
        }

        private static float AreaAt(Dictionary<int, float> areaByBin, int bin) => areaByBin.TryGetValue(bin, out float area) ? area : 0f;

        private static TableTop MeasureTop(int bin, Dictionary<int, List<Vector3>> pointsByBin)
        {
            Vector3 min = new Vector3(float.MaxValue, 0f, float.MaxValue);
            Vector3 max = new Vector3(float.MinValue, 0f, float.MinValue);
            float heightSum = 0f;
            int count = 0;
            for (int b = bin - 1; b <= bin + 1; b++)
            {
                if (!pointsByBin.TryGetValue(b, out List<Vector3> points)) continue;
                foreach (Vector3 p in points)
                {
                    min = new Vector3(Mathf.Min(min.x, p.x), 0f, Mathf.Min(min.z, p.z));
                    max = new Vector3(Mathf.Max(max.x, p.x), 0f, Mathf.Max(max.z, p.z));
                    heightSum += p.y;
                    count++;
                }
            }

            Vector3 centre = new Vector3((min.x + max.x) * 0.5f, heightSum / count, (min.z + max.z) * 0.5f);
            return new TableTop { centre = centre, radius = Mathf.Max(max.x - min.x, max.z - min.z) * 0.5f };
        }

        private static bool Fail(StringBuilder log, string reason)
        {
            log.AppendLine("  ERROR: " + reason);
            return false;
        }
    }
}
#endif
