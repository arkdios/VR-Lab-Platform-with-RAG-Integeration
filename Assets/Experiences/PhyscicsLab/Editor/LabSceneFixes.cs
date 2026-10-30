#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PhysicsLab.Lab.EditorTools
{
    /// <summary>
    /// Menu: Lab > 1 Fix floor fall and UI.
    /// Gives the spring scale a collider and the same locks as the ring (so it stays on the table),
    /// sets the Canvas Scaler to 1920 x 1080 (so the panels leave the middle of the screen free),
    /// and puts StudentScreenGate on the student screen. Safe to run twice.
    /// </summary>
    public static class LabSceneFixes
    {
        private const float DampingLikeRing = 6f;

        [MenuItem("Lab/1 Fix floor fall and UI")]
        private static void Run()
        {
            var log = new StringBuilder("[LabSceneFixes]\n");
            RunAll(log);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log(log.ToString());
        }

        /// <summary>Scale fix, Canvas Scaler, screen gate. Returns true when the spring scale was found.</summary>
        public static bool RunAll(StringBuilder log)
        {
            SpringScale scale = PrepareSpringScale(log);
            FixCanvasScaler(log);
            AddScreenGate(log);
            FixCameraLayers(log);
            return scale != null;
        }

        /// <summary>
        /// Collider, locks and helper scripts for the spring scale. Also used by the string builder.
        /// </summary>
        public static SpringScale PrepareSpringScale(StringBuilder log)
        {
            SpringScale scale = Object.FindFirstObjectByType<SpringScale>();
            if (scale == null)
            {
                log.AppendLine("  ERROR: no SpringScale in the scene.");
                return null;
            }

            Rigidbody body = scale.GetComponent<Rigidbody>();
            Undo.RecordObject(body, "Fix spring scale body");
            // Freeze Y and rotation, like the ring (constraints 116). Damping stops it sliding.
            body.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotation;
            body.linearDamping = DampingLikeRing;
            body.interpolation = RigidbodyInterpolation.None;
            log.AppendLine("  Spring scale: Y and rotation frozen, damping 6.");

            if (scale.GetComponentInChildren<Collider>() == null) AddFittedBox(scale.gameObject, log);
            else log.AppendLine("  Spring scale: already has a collider.");

            AddIfMissing<HoldInPlaceOnRelease>(scale.gameObject, log);
            AddIfMissing<ReturnIfFallen>(scale.gameObject, log);
            EditorUtility.SetDirty(scale.gameObject);
            return scale;
        }

        // One box around all renderers. The footprint is square, because the model turns to face the ring.
        private static void AddFittedBox(GameObject target, StringBuilder log)
        {
            Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                log.AppendLine("  Spring scale: no renderers found, collider NOT added. Add a Box Collider by hand.");
                return;
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

            BoxCollider box = Undo.AddComponent<BoxCollider>(target);
            Vector3 scale = target.transform.lossyScale;
            float footprint = Mathf.Max(bounds.size.x, bounds.size.z);
            box.center = target.transform.InverseTransformPoint(bounds.center);
            box.size = new Vector3(footprint / scale.x, bounds.size.y / scale.y, footprint / scale.z);
            log.AppendLine($"  Spring scale: Box Collider added, world size {footprint:F3} x {bounds.size.y:F3} x {footprint:F3} m.");
        }

        // The lab parts sit on the LabFixed and LabBody layers. A camera that does not draw those layers
        // shows an empty room in the Game view, while the Scene view (it draws every layer) looks fine.
        private static void FixCameraLayers(StringBuilder log)
        {
            int labLayers = LayerMask.GetMask("LabFixed", "LabBody");
            foreach (Camera cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                bool drawsDefaultLayer = (cam.cullingMask & 1) != 0;   // the stereo output camera draws only its own layer
                if (!drawsDefaultLayer || (cam.cullingMask & labLayers) == labLayers) continue;

                Undo.RecordObject(cam, "Camera draws lab layers");
                cam.cullingMask |= labLayers;
                EditorUtility.SetDirty(cam);
                log.AppendLine($"  {cam.name}: now draws LabFixed and LabBody (table, pulleys, masses, ring).");
            }
        }

        private static void FixCanvasScaler(StringBuilder log)
        {
            CanvasScaler scaler = Object.FindFirstObjectByType<CanvasScaler>(FindObjectsInactive.Include);
            if (scaler == null)
            {
                log.AppendLine("  ERROR: no Canvas Scaler found.");
                return;
            }
            Undo.RecordObject(scaler, "Canvas Scaler reference");
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            EditorUtility.SetDirty(scaler);
            log.AppendLine("  Canvas Scaler reference resolution set to 1920 x 1080 (VR uses a world-space canvas, which ignores it).");
        }

        private static void AddScreenGate(StringBuilder log)
        {
            Transform screen = FindSceneObject("StudentScreen");
            if (screen == null)
            {
                log.AppendLine("  ERROR: no object named StudentScreen.");
                return;
            }
            AddIfMissing<StudentScreenGate>(screen.gameObject, log);
        }

        private static void AddIfMissing<T>(GameObject target, StringBuilder log) where T : Component
        {
            if (target.GetComponent<T>() != null)
            {
                log.AppendLine($"  {target.name}: {typeof(T).Name} already there.");
                return;
            }
            Undo.AddComponent<T>(target);
            log.AppendLine($"  {target.name}: {typeof(T).Name} added.");
        }

        /// <summary>
        /// Finds an object by name in the open scene, including inactive ones. Skips prefab assets.
        /// </summary>
        public static Transform FindSceneObject(string objectName)
        {
            foreach (Transform t in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (t.name == objectName && t.gameObject.scene.IsValid() && !EditorUtility.IsPersistent(t)) return t;
            }
            return null;
        }
    }
}
#endif
