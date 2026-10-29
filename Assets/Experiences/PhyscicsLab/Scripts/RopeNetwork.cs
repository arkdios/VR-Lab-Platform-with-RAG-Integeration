using System.Collections.Generic;
using UnityEngine;

namespace PhysicsLab.Lab.Rope
{
    /// <summary>
    /// Runs all string arms every physics step. Before PhysX moves anything it reads each body,
    /// lets RopeSolver cut the speeds that would stretch a string, and writes the speeds back.
    /// Put one on the object that holds the arms.
    /// </summary>
    [DefaultExecutionOrder(200)] // after DesktopPointer and TableBounds, which also run in FixedUpdate
    public class RopeNetwork : MonoBehaviour
    {
        [SerializeField] private RopeArm[] arms;
        [Tooltip("Solver passes per step. 12 is enough for four arms on one ring.")]
        [SerializeField, Range(4, 30)] private int iterations = 12;
        [Tooltip("Physics step in seconds. 0.01 keeps a slack string from snapping hard.")]
        [SerializeField] private float stepSeconds = 0.01f;
        [Tooltip("Shows each arm's tension and taut/slack state at the top left. Editor and development builds only.")]
        [SerializeField] private bool showTensionsOnScreen = true;

        private Rigidbody[] _bodies;
        private RopeBodyState[] _states;
        private Vector3[] _lastPositions;
        private RopeLink[] _links;

        private void Awake()
        {
            Time.fixedDeltaTime = stepSeconds; // saved with the scene, so it also holds in the WebGL build
            Rebuild();
        }

        /// <summary>
        /// Collects the bodies and links. Call it again if an arm gets a new end body at runtime.
        /// </summary>
        public void Rebuild()
        {
            if (arms == null)
            {
                Debug.LogError("[RopeNetwork] Assign the arms.", this);
                arms = new RopeArm[0];
            }

            var unique = new List<Rigidbody>();
            var activeArms = new List<RopeArm>();
            foreach (RopeArm arm in arms)
            {
                if (arm == null || arm.RingBody == null || arm.EndBody == null) continue;
                activeArms.Add(arm);
                AddOnce(unique, arm.RingBody);
                AddOnce(unique, arm.EndBody);
            }

            _bodies = unique.ToArray();
            _states = new RopeBodyState[_bodies.Length];
            _lastPositions = new Vector3[_bodies.Length];
            _links = new RopeLink[activeArms.Count];

            for (int i = 0; i < _bodies.Length; i++)
            {
                // The rope writes speeds every step. Interpolation would fight that, and a sleeping body ignores it.
                _bodies[i].interpolation = RigidbodyInterpolation.None;
                _bodies[i].sleepThreshold = 0f;
                _lastPositions[i] = _bodies[i].position;
            }
            for (int k = 0; k < activeArms.Count; k++)
            {
                _links[k] = activeArms[k].Link;
                _links[k].bodyA = unique.IndexOf(activeArms[k].RingBody);
                _links[k].bodyB = unique.IndexOf(activeArms[k].EndBody);
            }
            arms = activeArms.ToArray();
        }

        private void FixedUpdate()
        {
            if (_links == null || _links.Length == 0) return;
            float dt = Time.fixedDeltaTime;

            for (int i = 0; i < _bodies.Length; i++) _states[i] = ReadState(i, dt);
            for (int k = 0; k < arms.Length; k++) arms[k].RefreshLink();

            RopeSolver.Solve(_states, _links, _links.Length, dt, iterations, true);

            for (int i = 0; i < _bodies.Length; i++) WriteVelocity(i);
            for (int k = 0; k < arms.Length; k++) arms[k].AfterSolve(dt);
        }

        // Debug text so you can check the tensions against the slides. Not drawn in a release build.
        private void OnGUI()
        {
            if (!showTensionsOnScreen || arms == null) return;
            if (!Application.isEditor && !Debug.isDebugBuild) return;

            for (int i = 0; i < arms.Length; i++)
            {
                string state = arms[i].IsTaut ? "taut" : "slack";
                GUI.Label(new Rect(10f, 10f + 20f * i, 420f, 20f),
                          $"{arms[i].name}: {arms[i].TensionNewtons:0.00} N ({state})");
            }
        }

        private RopeBodyState ReadState(int index, float dt)
        {
            Rigidbody body = _bodies[index];
            RigidbodyConstraints c = body.constraints;
            Vector3 axes = new Vector3(
                (c & RigidbodyConstraints.FreezePositionX) != 0 ? 0f : 1f,
                (c & RigidbodyConstraints.FreezePositionY) != 0 ? 0f : 1f,
                (c & RigidbodyConstraints.FreezePositionZ) != 0 ? 0f : 1f);

            // A kinematic body (headset grab) or a fully frozen body (scale left in place) cannot be moved by the string.
            bool fixedInPlace = body.isKinematic || axes == Vector3.zero;

            var state = new RopeBodyState { freeAxes = axes };
            state.inverseMass = fixedInPlace ? 0f : 1f / Mathf.Max(body.mass, 1e-4f);
            state.gravity = body.useGravity && !fixedInPlace ? Vector3.Scale(Physics.gravity, axes) : Vector3.zero;

            // A kinematic body reports no speed, so measure it from how far it moved.
            state.velocity = body.isKinematic
                ? (body.position - _lastPositions[index]) / dt
                : Vector3.Scale(body.linearVelocity, axes);
            _lastPositions[index] = body.position;
            return state;
        }

        private void WriteVelocity(int index)
        {
            if (_states[index].inverseMass <= 0f) return;
            _bodies[index].linearVelocity = Vector3.Scale(_states[index].velocity, _states[index].freeAxes);
        }

        private static void AddOnce(List<Rigidbody> list, Rigidbody body)
        {
            if (!list.Contains(body)) list.Add(body);
        }
    }
}
