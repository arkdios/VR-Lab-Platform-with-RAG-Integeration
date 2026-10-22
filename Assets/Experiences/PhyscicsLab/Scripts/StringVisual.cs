using System;
using UnityEngine;

namespace PhysicsLab.Lab
{
    /// <summary>
    /// Draws a string through a list of anchor points with a LineRenderer. Points are re-read every
    /// frame in LateUpdate (after physics and interpolation), so the string never lags the ring,
    /// the pulley or the hanging mass. Visual only: the physics force is applied by Pulley and SpringScale.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class StringVisual : MonoBehaviour
    {
        [Serializable]
        private struct StringPoint
        {
            [Tooltip("Object this point is attached to.")]
            public Transform anchor;
            [Tooltip("Offset in the anchor's local space, in metres.")]
            public Vector3 localOffset;
        }

        [SerializeField] private StringPoint[] points;
        [SerializeField] private float widthMetres = 0.002f;

        private LineRenderer _line;
        private Vector3[] _positions;

        private void Awake()
        {
            if (!HasValidPoints())
            {
                enabled = false;
                return;
            }

            _line = GetComponent<LineRenderer>();
            _line.useWorldSpace = true;
            _line.positionCount = points.Length;
            _line.widthCurve = AnimationCurve.Constant(0f, 1f, 1f);
            _line.widthMultiplier = widthMetres;
            _line.numCapVertices = 2;
            _line.alignment = LineAlignment.View;
            _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _line.receiveShadows = false;
            _positions = new Vector3[points.Length];
        }

        // Fixed-size array and SetPositions keep this free of garbage allocation.
        private void LateUpdate()
        {
            for (int i = 0; i < points.Length; i++)
            {
                _positions[i] = points[i].anchor.TransformPoint(points[i].localOffset);
            }
            _line.SetPositions(_positions);
        }

        private bool HasValidPoints()
        {
            if (points == null || points.Length < 2)
            {
                Debug.LogError("[StringVisual] Needs at least 2 points.", this);
                return false;
            }
            for (int i = 0; i < points.Length; i++)
            {
                if (points[i].anchor != null) continue;
                Debug.LogError($"[StringVisual] Point {i} has no anchor.", this);
                return false;
            }
            return true;
        }
    }
}
