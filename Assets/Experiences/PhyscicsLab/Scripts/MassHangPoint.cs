using System.Collections.Generic;
using UnityEngine;

namespace PhysicsLab.Lab
{
    /// <summary>
    /// The loop at the end of a pulley string. The student hangs hooked masses on it by hand, like in the real lab.
    /// The mass of the pulley is the total of the masses hanging on the loop. With nothing on it, the mass is 0.
    /// Put it on the Mass object of a pulley (the one the string ties to). It replaces the mass buttons.
    /// </summary>
    [DisallowMultipleComponent]
    public class MassHangPoint : MonoBehaviour
    {
        private const int MaxHooked = 4;
        private const float HookTipAboveLoopMetres = 0.005f;   // the hook of a mass reaches a little above the loop

        [SerializeField] private Pulley pulley;
        [Tooltip("How close the hook of a mass must be to the loop, in metres, when you let go of it.")]
        [SerializeField] private float catchRadiusMetres = 0.10f;
        [Tooltip("The gap between two masses that hang side by side on the loop, in metres.")]
        [SerializeField] private float sideGapMetres = 0.01f;

        private static readonly List<MassHangPoint> Loops = new List<MassHangPoint>();

        private readonly List<HookedBenchMass> _hooked = new List<HookedBenchMass>(MaxHooked);
        private Collider[] _ownColliders;
        private BoxCollider _box;
        private Vector3 _baseCenter;
        private Vector3 _baseSize;

        /// <summary>Total mass of everything that hangs on this loop, in kilograms.</summary>
        public float HookedKg { get; private set; }

        public int HookedCount => _hooked.Count;

        private void Awake()
        {
            if (pulley == null)
            {
                Debug.LogError("[MassHangPoint] Assign the Pulley.", this);
                enabled = false;
                return;
            }

            _ownColliders = GetComponents<Collider>();
            _box = GetComponent<BoxCollider>();
            if (_box != null)
            {
                _baseCenter = _box.center;
                _baseSize = _box.size;
            }
        }

        private void OnEnable() => Loops.Add(this);

        private void OnDisable() => Loops.Remove(this);

        // With nothing hooked the pulley starts at 0 kg, so the mass is always the total of what hangs here.
        private void Start() => Refresh();

        /// <summary>The nearest loop whose catch circle holds the point, or null.</summary>
        public static MassHangPoint FindNear(Vector3 hookTip)
        {
            MassHangPoint best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < Loops.Count; i++)
            {
                float distance = (Loops[i].transform.position - hookTip).sqrMagnitude;
                float reach = Loops[i].catchRadiusMetres;
                if (distance > reach * reach || distance >= bestDistance) continue;

                best = Loops[i];
                bestDistance = distance;
            }
            return best;
        }

        /// <summary>Hangs a mass on the loop. Returns false when the loop is full.</summary>
        public bool TryHook(HookedBenchMass mass)
        {
            if (_hooked.Count >= MaxHooked || _hooked.Contains(mass)) return false;

            _hooked.Add(mass);
            mass.HangOn(this, _ownColliders);
            Relayout();
            Refresh();
            return true;
        }

        /// <summary>Takes a mass off the loop.</summary>
        public void Unhook(HookedBenchMass mass)
        {
            if (!_hooked.Remove(mass)) return;

            mass.LeaveLoop(_ownColliders);
            Relayout();
            Refresh();
        }

        // Masses hang side by side on the loop, in a row that is centred under it.
        private void Relayout()
        {
            float total = sideGapMetres * Mathf.Max(0, _hooked.Count - 1);
            for (int i = 0; i < _hooked.Count; i++) total += _hooked[i].WorldBounds().size.x;

            Vector3 loop = transform.position;
            float left = loop.x - total * 0.5f;
            for (int i = 0; i < _hooked.Count; i++)
            {
                Bounds box = _hooked[i].WorldBounds();
                float centreY = loop.y + HookTipAboveLoopMetres - box.size.y * 0.5f;
                var target = new Vector3(left + box.size.x * 0.5f, centreY, loop.z);
                _hooked[i].transform.position += target - box.center;
                left += box.size.x + sideGapMetres;
            }
        }

        // Sets the mass of the pulley, and makes the box of the loop wide and tall enough to hold what hangs on it,
        // so the whole load rests on the bench as one piece.
        private void Refresh()
        {
            float total = 0f;
            for (int i = 0; i < _hooked.Count; i++) total += _hooked[i].MassKg;
            HookedKg = total;
            pulley.SetMass(total);
            FitCollider();
        }

        private void FitCollider()
        {
            if (_box == null) return;

            var local = new Bounds(_baseCenter, _baseSize);
            for (int i = 0; i < _hooked.Count; i++) EncapsulateWorldBox(ref local, _hooked[i].WorldBounds());
            _box.center = local.center;
            _box.size = local.size;
        }

        // Adds the eight corners of a world box to a box in the local space of this object.
        private void EncapsulateWorldBox(ref Bounds local, Bounds world)
        {
            Vector3 low = world.min;
            Vector3 high = world.max;
            for (int corner = 0; corner < 8; corner++)
            {
                var point = new Vector3(
                    (corner & 1) == 0 ? low.x : high.x,
                    (corner & 2) == 0 ? low.y : high.y,
                    (corner & 4) == 0 ? low.z : high.z);
                local.Encapsulate(transform.InverseTransformPoint(point));
            }
        }
    }
}