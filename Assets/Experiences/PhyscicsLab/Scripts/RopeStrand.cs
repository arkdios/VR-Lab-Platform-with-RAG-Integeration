using UnityEngine;

namespace PhysicsLab.Lab.Rope
{
    /// <summary>
    /// The visible string of one arm. It is a chain of small points. The points fall under gravity, lie on the table,
    /// the bench and the floor, run over the pulley wheel, and cannot pass through any of them.
    /// The forces (tension, taut or slack) still come from RopeNetwork. This class only shapes and draws the string.
    /// It pushes nothing, because a real string weighs about a gram, and a chain of heavy physics objects would
    /// stretch and would change the forces the lab measures.
    /// Put it on the same object as the RopeArm. It also draws in Edit Mode: Lab > Bake strings lets the chain
    /// settle and saves its shape in the scene.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(RopeArm), typeof(LineRenderer))]
    public class RopeStrand : MonoBehaviour
    {
        private const float SegmentMetres = 0.015f;      // distance between two points of the chain
        private const int MinPoints = 6;
        private const int MaxPoints = 80;
        private const float StepSeconds = 0.01f;         // the same step as RopeNetwork
        private const int BakeSteps = 600;               // 6 seconds of settling in Edit Mode
        private const int LengthPasses = 24;             // how often the chain is pulled back to its length per step
        private const int RepairPasses = 8;              // extra length passes after the contact fix
        private const float SkinMetres = 0.0005f;        // gap kept between the string and a surface
        private const float MinMoveMetres = 0.0002f;     // a point that moves less than this needs no contact test
        private const float LiftSearchMetres = 0.05f;    // how far above a point to look for the surface it sank into
        private const float WheelSavingPerRadius = 0.43f; // a round wheel saves this part of its radius against a path that cuts the corner (2 - pi/2)
        private const int BufferSize = 8;

        [Tooltip("Half the thickness the string takes up when it touches a surface.")]
        [SerializeField] private float radiusMetres = 0.002f;
        [Tooltip("Air drag. Higher settles faster.")]
        [SerializeField] private float dragPerSecond = 3f;
        [Tooltip("Surface grip. 0 is ice. At 0.5 a slack string stays where it lies and a pulled one slides.")]
        [SerializeField, Range(0f, 1f)] private float friction = 0.5f;
        [Tooltip("The round wheels this string runs over. Filled in by Lab > 0.")]
        [SerializeField] private SphereCollider[] wheels;
        [SerializeField, HideInInspector] private Vector3[] restingPoints;

        private RopeArm _arm;
        private LineRenderer _line;
        private Vector3[] _pos;      // where each point is now
        private Vector3[] _old;      // where it was one step ago (its speed is the difference)
        private Vector3[] _draw;
        private float _segment;
        private readonly RaycastHit[] _hits = new RaycastHit[BufferSize];
        private readonly Collider[] _overlaps = new Collider[BufferSize];

        /// <summary>Number of points in the chain, or 0 when the strand is not set up yet.</summary>
        public int PointCount => _pos == null ? 0 : _pos.Length;

        private void OnEnable() => Prepare();

        private void FixedUpdate()
        {
            if (Application.isPlaying && _pos != null) Step(Time.fixedDeltaTime);
        }

        // Runs after physics, so the string is drawn on the final positions of the ring and the mass.
        private void LateUpdate()
        {
            if (Application.isPlaying && _pos != null) Draw();
        }

        /// <summary>
        /// Edit Mode: starts the chain from scratch, lets it settle for a few seconds of physics time on the
        /// table, bench, floor and wheel, and saves the shape in the scene. Returns false when the arm is not built yet.
        /// </summary>
        public bool Bake()
        {
            restingPoints = null;
            if (!Prepare()) return false;

            Physics.SyncTransforms();   // so the contact tests see where everything is now
            for (int step = 0; step < BakeSteps; step++) Step(StepSeconds);
            restingPoints = (Vector3[])_pos.Clone();
            Draw();
            return true;
        }

        // ---------- setup ----------

        private bool Prepare()
        {
            _arm = GetComponent<RopeArm>();
            _line = GetComponent<LineRenderer>();
            if (_arm.RingAnchor == null || _arm.EndAnchor == null)
            {
                _pos = null;   // the arm is not built yet
                return false;
            }

            ApplyLineSettings();
            StartChain();
            Draw();
            return true;
        }

        private void ApplyLineSettings()
        {
            _line.useWorldSpace = true;
            _line.widthCurve = AnimationCurve.Constant(0f, 1f, 1f);
            _line.widthMultiplier = _arm.WidthMetres;
            _line.numCapVertices = 2;
            _line.numCornerVertices = 2;
            _line.alignment = LineAlignment.View;
            _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _line.receiveShadows = false;
        }

        private void StartChain()
        {
            float length = RestLength();
            int count = Mathf.Clamp(Mathf.RoundToInt(length / SegmentMetres) + 1, MinPoints, MaxPoints);
            _segment = length / (count - 1);
            _pos = new Vector3[count];
            _old = new Vector3[count];
            _draw = new Vector3[count];

            if (restingPoints != null && restingPoints.Length == count) System.Array.Copy(restingPoints, _pos, count);
            else ShapeStart(length);
            System.Array.Copy(_pos, _old, count);
        }

        // A string that hugs a round wheel is a little shorter than the path the solver measures through the corner point.
        private float RestLength()
        {
            float saving = 0f;
            if (wheels != null)
            {
                foreach (SphereCollider wheel in wheels)
                {
                    if (wheel != null) saving = Mathf.Max(saving, WheelSavingPerRadius * WheelRadius(wheel));
                }
            }
            return Mathf.Max(0.05f, _arm.LengthMetres - saving);
        }

        // The first shape: a bow on the table, like the old drawn string, so slack has a side to fall to.
        private void ShapeStart(float length)
        {
            Vector3 start = _arm.RingAnchor.position;
            Vector3 end = _arm.EndAnchor.position;
            Vector3 side = Vector3.Cross(Vector3.up, end - start);
            if (side.sqrMagnitude < 1e-8f) side = Vector3.right;
            side = side.normalized * (_arm.SlackSide < 0 ? -1f : 1f);

            if (_arm.Guide == null) RopeArc.Fill(start, end, length, side, _pos);
            else ShapeStartOverWheel(start, end, side, length);
        }

        // Ring to the wheel as a bow on the table, then straight down to the loop.
        private void ShapeStartOverWheel(Vector3 start, Vector3 end, Vector3 side, float length)
        {
            Vector3 wheelTop = _arm.Guide.position;
            int last = _pos.Length - 1;
            float hanging = Vector3.Distance(wheelTop, end);
            float onTable = Mathf.Max(0.01f, length - hanging);
            int turn = Mathf.Clamp(Mathf.RoundToInt(last * onTable / length), 1, last - 1);

            var firstPart = new Vector3[turn + 1];
            RopeArc.Fill(start, wheelTop, onTable, side, firstPart);
            for (int i = 0; i <= turn; i++) _pos[i] = firstPart[i];
            for (int i = turn + 1; i <= last; i++) _pos[i] = Vector3.Lerp(wheelTop, end, (i - turn) / (float)(last - turn));
        }

        // ---------- one physics step ----------

        private void Step(float dt)
        {
            PinEnds();
            MoveFreely(dt);
            for (int pass = 0; pass < LengthPasses; pass++) KeepLengths(pass);
            CollideWithRoom();
            for (int pass = 0; pass < RepairPasses; pass++) KeepLengths(pass);
        }

        // Both ends follow the ring and the loop exactly.
        private void PinEnds()
        {
            int last = _pos.Length - 1;
            _pos[0] = _arm.RingAnchor.position;
            _old[0] = _pos[0];
            _pos[last] = _arm.EndAnchor.position;
            _old[last] = _pos[last];
        }

        // Verlet step: keep the speed from last step (with a little drag) and add gravity.
        private void MoveFreely(float dt)
        {
            Vector3 pull = Physics.gravity * (dt * dt);
            float keep = Mathf.Exp(-dragPerSecond * dt);
            for (int i = 1; i < _pos.Length - 1; i++)
            {
                Vector3 velocity = (_pos[i] - _old[i]) * keep;
                _old[i] = _pos[i];
                _pos[i] += velocity + pull;
            }
        }

        // One sweep along the chain that pulls every pair of neighbours back to the segment length.
        // The sweep direction flips on every pass, so the correction reaches both ends faster.
        private void KeepLengths(int pass)
        {
            int last = _pos.Length - 1;
            for (int n = 0; n < last; n++)
            {
                int i = pass % 2 == 0 ? n : last - 1 - n;
                Vector3 gap = _pos[i + 1] - _pos[i];
                float distance = gap.magnitude;
                if (distance < 1e-6f) continue;

                float error = (distance - _segment) / distance;
                float shareA = i == 0 ? 0f : (i + 1 == last ? 1f : 0.5f);   // a pinned end does not move
                float shareB = i + 1 == last ? 0f : (i == 0 ? 1f : 0.5f);
                _pos[i] += gap * (error * shareA);
                _pos[i + 1] -= gap * (error * shareB);
            }
            PushOutOfWheels();
            PinEnds();
        }

        // A wheel is a ball in the maths. A point inside it is moved out along the line from its centre.
        private void PushOutOfWheels()
        {
            if (wheels == null) return;

            foreach (SphereCollider wheel in wheels)
            {
                if (wheel == null) continue;

                Vector3 center = wheel.transform.TransformPoint(wheel.center);
                float reach = WheelRadius(wheel);
                for (int i = 1; i < _pos.Length - 1; i++)
                {
                    Vector3 offset = _pos[i] - center;
                    float distance = offset.magnitude;
                    if (distance < reach && distance > 1e-6f) _pos[i] = center + offset * (reach / distance);
                }
            }
        }

        private static float WheelRadius(SphereCollider wheel)
        {
            Vector3 scale = wheel.transform.lossyScale;
            return wheel.radius * Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z));
        }

        // ---------- contact with the room ----------

        // The string touches only things that never move (table, bench, floor, walls). Parts with a Rigidbody
        // (ring, masses, scale, chair) do not push it, and it does not push them.
        private void CollideWithRoom()
        {
            for (int i = 1; i < _pos.Length - 1; i++)
            {
                Vector3 from = _old[i];
                Vector3 move = _pos[i] - from;
                float distance = move.magnitude;
                if (distance > MinMoveMetres && FindFirstFixedHit(from, move / distance, distance, out RaycastHit hit))
                {
                    StopAtSurface(i, from, move / distance, distance, hit);
                }
                LiftOutOfSurface(i);
            }
        }

        private bool FindFirstFixedHit(Vector3 from, Vector3 direction, float distance, out RaycastHit nearest)
        {
            int count = Physics.SphereCastNonAlloc(from, radiusMetres, direction, _hits, distance + SkinMetres,
                                                   ~0, QueryTriggerInteraction.Ignore);
            nearest = default(RaycastHit);
            float best = float.MaxValue;
            for (int k = 0; k < count; k++)
            {
                RaycastHit hit = _hits[k];
                bool isFixed = hit.collider.attachedRigidbody == null;
                if (isFixed && hit.distance > 0f && hit.distance < best)
                {
                    best = hit.distance;
                    nearest = hit;
                }
            }
            return best < float.MaxValue;
        }

        // The point stops on the surface and slides along it.
        private void StopAtSurface(int i, Vector3 from, Vector3 direction, float distance, RaycastHit hit)
        {
            Vector3 normal = hit.normal;
            Vector3 after = direction * (distance - hit.distance);          // the move that is left after touching
            float push = Mathf.Max(0f, -Vector3.Dot(after, normal));         // how far it tried to go into the surface
            Vector3 target = from + direction * hit.distance + normal * SkinMetres + Vector3.ProjectOnPlane(after, normal);

            Vector3 travel = target - from;
            _pos[i] = from + SlideWithGrip(travel, normal, push) + normal * Vector3.Dot(travel, normal);

            Vector3 velocity = _pos[i] - from;
            float away = Vector3.Dot(velocity, normal);
            velocity = Vector3.ProjectOnPlane(velocity, normal) + normal * Mathf.Max(0f, away);   // no speed into the surface
            _old[i] = _pos[i] - velocity;
        }

        // Coulomb grip: friction can remove as much sideways move as friction times the push into the surface.
        // So a slack string stays where it lies, and a string that is pulled hard slides.
        private Vector3 SlideWithGrip(Vector3 travel, Vector3 normal, float push)
        {
            Vector3 sideways = Vector3.ProjectOnPlane(travel, normal);
            float grip = friction * push;
            float length = sideways.magnitude;
            return length <= grip ? Vector3.zero : sideways * (1f - grip / length);
        }

        // The cast cannot see a surface the point already sank into. This finds the surface above it and lifts the point out.
        private void LiftOutOfSurface(int i)
        {
            if (!OverlapsFixedCollider(_pos[i])) return;

            Vector3 above = _pos[i] + Vector3.up * LiftSearchMetres;
            int hits = Physics.RaycastNonAlloc(above, Vector3.down, _hits, LiftSearchMetres + radiusMetres * 2f,
                                               ~0, QueryTriggerInteraction.Ignore);
            float top = float.MinValue;
            for (int k = 0; k < hits; k++)
            {
                if (_hits[k].collider.attachedRigidbody == null) top = Mathf.Max(top, _hits[k].point.y);
            }
            if (top > float.MinValue) _pos[i].y = Mathf.Max(_pos[i].y, top + radiusMetres + SkinMetres);
        }

        private bool OverlapsFixedCollider(Vector3 point)
        {
            int count = Physics.OverlapSphereNonAlloc(point, radiusMetres * 0.9f, _overlaps, ~0, QueryTriggerInteraction.Ignore);
            for (int k = 0; k < count; k++)
            {
                if (_overlaps[k].attachedRigidbody == null) return true;
            }
            return false;
        }

        // ---------- drawing ----------

        // The chain is drawn with both ends exactly on the ring and the loop.
        private void Draw()
        {
            System.Array.Copy(_pos, _draw, _pos.Length);
            _draw[0] = _arm.RingAnchor.position;
            _draw[_draw.Length - 1] = _arm.EndAnchor.position;
            _line.positionCount = _draw.Length;
            _line.SetPositions(_draw);
        }
    }
}