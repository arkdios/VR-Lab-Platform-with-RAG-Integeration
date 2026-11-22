using UnityEngine;

namespace PhysicsLab.Lab
{
    /// <summary>
    /// A loose hooked mass (50 g, 100 g or 200 g) that the student carries by hand.
    /// Let it go close to the loop at the end of a pulley string and it hangs on the loop.
    /// Pick it up and it comes off. Its weight counts on a pulley only while it hangs on a loop (see MassHangPoint).
    /// Put it on every loose hooked mass. It needs a LabObject and a Rigidbody on the same object.
    /// </summary>
    [RequireComponent(typeof(LabObject), typeof(Rigidbody))]
    public class HookedBenchMass : MonoBehaviour
    {
        [Tooltip("The weight of this mass in kilograms. Zero means: use the mass of the Rigidbody.")]
        [SerializeField] private float massKg;

        private LabObject _labObject;
        private Rigidbody _body;
        private Collider[] _colliders;
        private Renderer[] _renderers;
        private Transform _home;                 // the parent it had before it was hooked
        private MassHangPoint _loop;             // the loop it hangs on, or null
        private bool _releasePending;            // it was let go: decide in the next physics step
        private RigidbodyInterpolation _interpolation;
        private CollisionDetectionMode _detection;

        public float MassKg => massKg;
        public bool IsHooked => _loop != null;

        private void Awake()
        {
            _labObject = GetComponent<LabObject>();
            _body = GetComponent<Rigidbody>();
            _colliders = GetComponentsInChildren<Collider>(true);
            _renderers = GetComponentsInChildren<Renderer>(true);
            _home = transform.parent;
            _interpolation = _body.interpolation;
            _detection = _body.collisionDetectionMode;
            if (massKg <= 0f) massKg = _body.mass;
        }

        private void OnEnable() => _labObject.HeldChanged += OnHeldChanged;

        private void OnDisable() => _labObject.HeldChanged -= OnHeldChanged;

        // Grabbed: it comes off the loop at once. Let go: wait for the next physics step (see FixedUpdate).
        private void OnHeldChanged(LabObject changed, bool held)
        {
            _releasePending = !held;
            if (held && _loop != null) _loop.Unhook(this);
        }

        // A headset grab gives the body back to physics a little after the release event, so the choice
        // is made here, one physics step later, when the body is free for sure.
        private void FixedUpdate()
        {
            if (!_releasePending || _labObject.IsHeld) return;

            _releasePending = false;
            MassHangPoint loop = MassHangPoint.FindNear(HookTip());
            if (loop == null || !loop.TryHook(this)) Fall();
        }

        /// <summary>The box around everything this mass draws, in world space.</summary>
        public Bounds WorldBounds()
        {
            if (_renderers.Length == 0) return new Bounds(transform.position, Vector3.one * 0.02f);

            Bounds box = _renderers[0].bounds;
            for (int i = 1; i < _renderers.Length; i++) box.Encapsulate(_renderers[i].bounds);
            return box;
        }

        /// <summary>
        /// Called by MassHangPoint. The mass stops being a loose part: no gravity, no speed, and it
        /// follows the loop. It ignores the loop's own colliders, so the two never push each other.
        /// </summary>
        public void HangOn(MassHangPoint loop, Collider[] loopColliders)
        {
            _loop = loop;
            _body.linearVelocity = Vector3.zero;
            _body.angularVelocity = Vector3.zero;
            _body.interpolation = RigidbodyInterpolation.None;
            _body.collisionDetectionMode = CollisionDetectionMode.Discrete;
            _body.isKinematic = true;

            transform.SetParent(loop.transform, true);
            transform.rotation = Quaternion.identity;   // hangs straight, like a real hooked mass
            SetIgnore(loopColliders, true);
        }

        /// <summary>Called by MassHangPoint when the mass leaves the loop. It becomes a loose part again.</summary>
        public void LeaveLoop(Collider[] loopColliders)
        {
            SetIgnore(loopColliders, false);
            transform.SetParent(_home, true);
            _body.isKinematic = false;
            _body.interpolation = _interpolation;
            _body.collisionDetectionMode = _detection;
            _loop = null;
        }

        // The top of the mass, where its hook is.
        private Vector3 HookTip()
        {
            Bounds box = WorldBounds();
            return new Vector3(box.center.x, box.max.y, box.center.z);
        }

        // Let go away from every loop: a normal loose part that falls.
        private void Fall()
        {
            _body.isKinematic = false;
            _body.useGravity = true;
        }

        private void SetIgnore(Collider[] others, bool ignore)
        {
            foreach (Collider mine in _colliders)
            {
                foreach (Collider other in others)
                {
                    if (mine != null && other != null) Physics.IgnoreCollision(mine, other, ignore);
                }
            }
        }
    }
}