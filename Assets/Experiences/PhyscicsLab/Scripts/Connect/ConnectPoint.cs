using UnityEngine;

namespace PhysicsLab.Lab.Connect
{
    /// <summary>What a spot accepts. More kinds (string loop, hook) can be added later.</summary>
    public enum ConnectKind
    {
        TableSlot
    }

    /// <summary>How bright a spot glows.</summary>
    public enum GlowState
    {
        Hidden,
        Dim,
        Bright
    }

    /// <summary>
    /// One spot a part can snap to (for example a clamp position on the table rim).
    /// It shows a glowing marker: hidden normally, dim while a fitting part is held,
    /// bright and pulsing when that part is close enough to snap.
    /// </summary>
    public class ConnectPoint : MonoBehaviour
    {
        private const float PulseSpeed = 8f;
        private const float PulseSize = 0.25f;

        [SerializeField] private ConnectKind kind = ConnectKind.TableSlot;
        [SerializeField] private Renderer glowRenderer;
        [SerializeField] private Color dimColor = new Color(0.1f, 0.45f, 0.5f, 1f);
        [SerializeField] private Color brightColor = new Color(0.2f, 1f, 0.9f, 1f);

        private Material _material;
        private Vector3 _baseScale;
        private GlowState _state = GlowState.Hidden;

        public ConnectKind Kind => kind;

        /// <summary>Angle around the table, in degrees, counter-clockwise. Same convention as Pulley.</summary>
        public float AngleDegrees { get; set; }

        /// <summary>The part sitting here now, or null when the spot is free.</summary>
        public ConnectPart Occupant { get; set; }

        public bool IsFree => Occupant == null;

        /// <summary>Called once by the code that creates the spot.</summary>
        public void Setup(ConnectKind newKind, Renderer marker, float angleDegrees)
        {
            kind = newKind;
            glowRenderer = marker;
            AngleDegrees = angleDegrees;
            _baseScale = marker.transform.localScale;
            _material = marker.material; // one material copy per spot, so each can have its own colour
            SetGlow(GlowState.Hidden);
        }

        public void SetGlow(GlowState state)
        {
            _state = state;
            if (glowRenderer == null) return;

            glowRenderer.enabled = state != GlowState.Hidden;
            if (state == GlowState.Dim) _material.color = dimColor;
            if (state == GlowState.Bright) _material.color = brightColor;
            if (state != GlowState.Bright) glowRenderer.transform.localScale = _baseScale;
        }

        // The bright marker breathes in and out, so the target is easy to see.
        private void Update()
        {
            if (_state != GlowState.Bright || glowRenderer == null) return;
            float pulse = 1f + PulseSize * Mathf.Sin(Time.unscaledTime * PulseSpeed);
            Vector3 scale = _baseScale;
            scale.x *= pulse;
            scale.z *= pulse;
            glowRenderer.transform.localScale = scale;
        }
    }
}
