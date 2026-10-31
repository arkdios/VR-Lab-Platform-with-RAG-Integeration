#if UNITY_EDITOR
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace PhysicsLab.Lab.EditorTools
{
    /// <summary>
    /// Menu: Lab > 7 Make props grabbable.
    /// Makes the loose parts under "LabProps (Temp)" (hooked masses, pulley clamps, ring) grabbable with the
    /// mouse and the headset, and makes the furniture there (bench, chair, table copy) solid, so parts rest on it.
    /// Safe to run twice. Needs LabSerialized.SetString, SetBool and SetEnum (patch 3 in the guide).
    /// </summary>
    public static class LabPropsBuilder
    {
        private const string PropsRootName = "LabProps (Temp)";
        private const string IdPrefix = "prop-";
        private const float DefaultMassKg = 0.05f;
        private const float RingMassKg = 0.01f;
        private const float MinBoxMetres = 0.01f;
        private const float LinearDamping = 1f;            // a little drag, so a dropped part does not slide for ever
        private const float AngularDamping = 4f;
        private const float MaxDepenetrationSpeed = 1f;    // a part that starts inside something is eased out, not thrown
        private const int VelocityTrackingIndex = 2;       // XRGrabInteractable movement type: 0 Instantaneous, 1 Kinematic, 2 VelocityTracking

        // Props whose name starts with one of these are furniture: solid, but not grabbable.
        private static readonly string[] FurnitureNames = { "LabBench", "LabChair", "ForceTable" };

        [MenuItem("Lab/7 Make props grabbable")]
        private static void Menu()
        {
            var log = new StringBuilder("[LabPropsBuilder]\n");
            bool ok = Run(log);
            if (ok) EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            if (ok) Debug.Log(log.ToString()); else Debug.LogError(log.ToString());
        }

        public static bool Run(StringBuilder log)
        {
            Transform props = LabSceneFixes.FindSceneObject(PropsRootName);
            if (props == null)
            {
                log.AppendLine($"  No object named {PropsRootName}, so there are no props to set up.");
                return true;
            }

            int grabbable = 0;
            int solid = 0;
            foreach (Transform prop in props)   // direct children only: each one is a prop
            {
                if (IsFurniture(prop.name))
                {
                    MakeSolid(prop, log);
                    solid++;
                }
                else if (MakeGrabbable(prop, log))
                {
                    grabbable++;
                }
            }

            log.AppendLine($"  Props done: {grabbable} can be grabbed, {solid} are solid furniture.");
            return true;
        }

        // ---------- loose parts ----------

        private static bool MakeGrabbable(Transform prop, StringBuilder log)
        {
            if (!TryMeasureLocal(prop, out Bounds local))
            {
                log.AppendLine($"  Warning: {prop.name} has no renderer, so it was skipped.");
                return false;
            }

            GameObject go = prop.gameObject;
            FitBox(go, local);
            ConfigureBody(go, MassFromName(prop.name));

            LabObject labObject = GetOrAdd<LabObject>(go);
            LabSerialized.SetString(labObject, "objectId", IdPrefix + prop.name.ToLowerInvariant().Replace(' ', '-'));
            LabSerialized.SetBool(labObject, "freeDrag", true);   // dragged in 3D, the mouse wheel pushes and pulls

            GetOrAdd<ReturnIfFallen>(go);   // safety net: back to the start if it ever drops through the floor
            ConfigureGrab(go, log);

            log.AppendLine($"  {prop.name}: grabbable, {MassFromName(prop.name) * 1000f:0} g, box {local.size.x:0.000} x {local.size.y:0.000} x {local.size.z:0.000} m.");
            return true;
        }

        // One box around everything the prop draws. A box is simple and safe for a part that physics moves.
        private static void FitBox(GameObject go, Bounds local)
        {
            BoxCollider box = GetOrAdd<BoxCollider>(go);
            Undo.RecordObject(box, "Fit prop collider");
            box.isTrigger = false;
            box.center = local.center;
            box.size = Vector3.Max(local.size, Vector3.one * MinBoxMetres);
        }

        private static void ConfigureBody(GameObject go, float massKg)
        {
            Rigidbody body = GetOrAdd<Rigidbody>(go);
            Undo.RecordObject(body, "Prop body");
            body.mass = massKg;
            body.useGravity = true;
            body.isKinematic = false;
            body.linearDamping = LinearDamping;
            body.angularDamping = AngularDamping;
            body.maxDepenetrationVelocity = MaxDepenetrationSpeed;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            // The part may turn about the vertical axis only, so it stays the way up it was placed.
            body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        }

        // Same settings as the lab parts: velocity tracking, no rotation tracking, no throw.
        private static void ConfigureGrab(GameObject go, StringBuilder log)
        {
            XRGrabInteractable grab = GetOrAdd<XRGrabInteractable>(go);
            bool movement = LabSerialized.SetEnum(grab, "m_MovementType", VelocityTrackingIndex);
            bool rotation = LabSerialized.SetBool(grab, "m_TrackRotation", false);
            bool throwing = LabSerialized.SetBool(grab, "m_ThrowOnDetach", false);
            if (movement && rotation && throwing) return;

            log.AppendLine($"  Warning: {go.name}: some XRGrabInteractable settings were not found. Set Movement Type = Velocity Tracking, " +
                           "Track Rotation off, Throw On Detach off by hand.");
        }

        // "HookedMass100g" is 0.1 kg. The ring is light. Anything else gets a small default.
        private static float MassFromName(string propName)
        {
            Match grams = Regex.Match(propName, @"(\d+)g$");
            if (grams.Success) return int.Parse(grams.Groups[1].Value) / 1000f;
            return propName.StartsWith("Ring") ? RingMassKg : DefaultMassKg;
        }

        // ---------- furniture ----------

        private static bool IsFurniture(string propName)
        {
            foreach (string furniture in FurnitureNames)
            {
                if (propName.StartsWith(furniture)) return true;
            }
            return false;
        }

        // A mesh collider follows the real shape (a bench with a shelf is not a solid block).
        // There is no Rigidbody, so furniture never moves: physics treats it as fixed.
        private static void MakeSolid(Transform furniture, StringBuilder log)
        {
            int added = 0;
            foreach (MeshFilter filter in furniture.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null || filter.GetComponent<Collider>() != null) continue;
                MeshCollider collider = Undo.AddComponent<MeshCollider>(filter.gameObject);
                collider.sharedMesh = filter.sharedMesh;
                added++;
            }
            log.AppendLine($"  {furniture.name}: solid furniture, {added} mesh collider(s) added.");
        }

        // ---------- helpers ----------

        // The box that holds every renderer of the prop, measured in the prop's own space,
        // because a BoxCollider is written in local space.
        private static bool TryMeasureLocal(Transform prop, out Bounds local)
        {
            local = default(Bounds);
            bool started = false;
            foreach (Renderer renderer in prop.GetComponentsInChildren<Renderer>())
            {
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 point = prop.InverseTransformPoint(CornerOf(renderer.bounds, corner));
                    if (!started)
                    {
                        local = new Bounds(point, Vector3.zero);
                        started = true;
                    }
                    else
                    {
                        local.Encapsulate(point);
                    }
                }
            }
            return started;
        }

        // Corner 0 to 7 of a box. Bit 0 picks x, bit 1 picks y, bit 2 picks z (min or max).
        private static Vector3 CornerOf(Bounds box, int corner)
        {
            return new Vector3(
                (corner & 1) == 0 ? box.min.x : box.max.x,
                (corner & 2) == 0 ? box.min.y : box.max.y,
                (corner & 4) == 0 ? box.min.z : box.max.z);
        }

        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            T existing = go.GetComponent<T>();
            return existing != null ? existing : Undo.AddComponent<T>(go);
        }
    }
}
#endif