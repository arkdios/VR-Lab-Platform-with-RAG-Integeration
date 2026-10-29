using UnityEngine;

namespace PhysicsLab.Lab.Rope
{
    /// <summary>
    /// One arm of the force table string. One end is tied to the ring for good (ringAnchor). The other end
    /// is the loop (endAnchor) that holds a mass, the spring scale, or lies free on the table.
    /// The arm may pass over a pulley (guide). RopeNetwork solves the physics. This class holds the settings,
    /// keeps the tension, and draws the string: straight when taut, a curve when slack.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class RopeArm : MonoBehaviour
    {
        private const int ArcPointCount = 24;
        private const float StraightEnoughMetres = 0.001f;
        private const float TensionTimeSeconds = 0.15f;

        [Tooltip("Point on the ring where this arm is tied. A child of the Ring object.")]
        [SerializeField] private Transform ringAnchor;
        [Tooltip("Optional. The top of the pulley wheel that the string runs over.")]
        [SerializeField] private Transform guide;
        [Tooltip("The loop end of the arm: hook point on a mass or the scale, or the free end on the table.")]
        [SerializeField] private Transform endAnchor;
        [Tooltip("String length in metres, from the ring to the loop, along the path.")]
        [SerializeField] private float lengthMetres = 1f;
        [SerializeField] private float widthMetres = 0.002f;
        [Tooltip("+1 or -1. Which side a slack string curves to. Use different values so arms do not overlap.")]
        [SerializeField] private int slackSide = 1;
        [Tooltip("The end body never collides with these (ring, pulley, table), so a hanging mass does not jam.")]
        [SerializeField] private Collider[] ignoreCollisionWith;

        private LineRenderer _line;
        private Vector3[] _drawPoints;
        private Vector3[] _arc;
        private Vector3[] _guidePoints;
        private float _smoothedTension;

        public Rigidbody RingBody { get; private set; }
        public Rigidbody EndBody { get; private set; }
        public RopeLink Link { get; private set; }

        /// <summary>Tension in newtons, smoothed over 0.15 s so a label does not flicker.</summary>
        public float TensionNewtons => _smoothedTension;

        /// <summary>Tension in newtons from the last physics step, not smoothed.</summary>
        public float RawTensionNewtons => Link != null ? Link.tension : 0f;

        public float LengthMetres => lengthMetres;
        public bool IsTaut => Link != null && Link.overshoot > -StraightEnoughMetres;

        /// <summary>Changes the string length. Used by the builder and, later, by a "length" setting.</summary>
        public void SetLength(float metres) => lengthMetres = Mathf.Max(0.05f, metres);

        private void Awake()
        {
            if (!HasValidSetup())
            {
                enabled = false;
                return;
            }

            _guidePoints = guide != null ? new Vector3[1] : new Vector3[0];
            Link = new RopeLink { guides = _guidePoints, length = lengthMetres };
            _arc = new Vector3[ArcPointCount];
            _drawPoints = new Vector3[guide != null ? ArcPointCount + 1 : ArcPointCount];

            SetupLine();
            IgnoreCollisions();
        }

        /// <summary>Called by RopeNetwork every physics step, before the solver runs.</summary>
        public void RefreshLink()
        {
            Link.pointA = ringAnchor.position;
            Link.pointB = endAnchor.position;
            Link.length = lengthMetres;
            if (guide != null) _guidePoints[0] = guide.position;
        }

        /// <summary>Called by RopeNetwork after the solver ran.</summary>
        public void AfterSolve(float stepSeconds)
        {
            float blend = 1f - Mathf.Exp(-stepSeconds / TensionTimeSeconds);
            _smoothedTension += (Link.tension - _smoothedTension) * blend;
        }

        // LateUpdate runs after physics, so the string never lags behind the ring or the mass.
        private void LateUpdate()
        {
            Vector3 start = ringAnchor.position;
            Vector3 end = endAnchor.position;

            if (guide == null)
            {
                DrawFreeArm(start, end);
            }
            else
            {
                DrawGuidedArm(start, guide.position, end);
            }
            _line.positionCount = _drawPoints.Length;
            _line.SetPositions(_drawPoints);
        }

        // Ring to loop with nothing in between. Slack makes one curve.
        private void DrawFreeArm(Vector3 start, Vector3 end)
        {
            RopeArc.Fill(start, end, Mathf.Max(lengthMetres, Vector3.Distance(start, end)), SideFor(start, end), _arc);
            for (int i = 0; i < ArcPointCount; i++) _drawPoints[i] = _arc[i];
        }

        // Ring to pulley to loop. The hanging part is straight (the weight pulls it tight).
        // All the slack lies on the table, between the ring and the pulley.
        private void DrawGuidedArm(Vector3 start, Vector3 pulleyTop, Vector3 end)
        {
            float first = Vector3.Distance(start, pulleyTop);
            float second = Vector3.Distance(pulleyTop, end);
            float slack = Mathf.Max(0f, lengthMetres - first - second);

            float arcLength = slack > StraightEnoughMetres ? first + slack : first;
            RopeArc.Fill(start, pulleyTop, arcLength, SideFor(start, pulleyTop), _arc);
            for (int i = 0; i < ArcPointCount; i++) _drawPoints[i] = _arc[i];
            _drawPoints[ArcPointCount] = end;
        }

        // A sideways direction on the table, square to the chord. It never flips while the string moves.
        private Vector3 SideFor(Vector3 start, Vector3 end)
        {
            Vector3 chord = end - start;
            Vector3 side = Vector3.Cross(Vector3.up, chord);
            if (side.sqrMagnitude < 1e-8f) side = Vector3.right; // chord is straight up or down
            return side.normalized * Mathf.Sign(slackSide == 0 ? 1 : slackSide);
        }

        private void SetupLine()
        {
            _line = GetComponent<LineRenderer>();
            _line.useWorldSpace = true;
            _line.widthCurve = AnimationCurve.Constant(0f, 1f, 1f);
            _line.widthMultiplier = widthMetres;
            _line.numCapVertices = 2;
            _line.numCornerVertices = 2;
            _line.alignment = LineAlignment.View;
            _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _line.receiveShadows = false;
        }

        private void IgnoreCollisions()
        {
            if (ignoreCollisionWith == null) return;
            Collider[] own = EndBody.GetComponentsInChildren<Collider>();
            foreach (Collider other in ignoreCollisionWith)
            {
                if (other == null) continue;
                foreach (Collider mine in own) Physics.IgnoreCollision(mine, other, true);
            }
        }

        private bool HasValidSetup()
        {
            if (ringAnchor == null || endAnchor == null)
            {
                Debug.LogError("[RopeArm] Assign Ring Anchor and End Anchor.", this);
                return false;
            }
            RingBody = ringAnchor.GetComponentInParent<Rigidbody>();
            EndBody = endAnchor.GetComponentInParent<Rigidbody>();
            if (RingBody == null || EndBody == null)
            {
                Debug.LogError("[RopeArm] Both anchors need a Rigidbody on themselves or a parent.", this);
                return false;
            }
            return true;
        }
    }
}
