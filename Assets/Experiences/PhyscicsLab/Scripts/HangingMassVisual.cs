using UnityEngine;

namespace PhysicsLab.Lab
{
    /// <summary>
    /// Sizes the weight stack on the hanger platform to match the pulley's mass:
    /// 0.12 m of stack per kg by default (0.5 kg = 6 cm). Hidden at 0 kg. Visual only.
    /// Put it on the mass object (the one whose origin is the string knot).
    /// </summary>
    public class HangingMassVisual : MonoBehaviour
    {
        private const float MinVisibleHeightMetres = 0.001f;

        [SerializeField] private Pulley pulley;
        [Tooltip("The weight stack that sits on the platform. Must be a child of this object.")]
        [SerializeField] private Transform weights;
        [SerializeField] private float heightPerKgMetres = 0.12f;
        [Tooltip("Local y of the platform top in the mass model.")]
        [SerializeField] private float platformTopY = -0.070f;
        [Tooltip("Height of the weights mesh at scale 1. Unity's built-in cylinder is 2 m tall.")]
        [SerializeField] private float meshHeightAtUnitScale = 2f;

        private void Awake()
        {
            if (pulley != null && weights != null) return;
            Debug.LogError("[HangingMassVisual] Assign Pulley and Weights.", this);
            enabled = false;
        }

        private void OnEnable()
        {
            pulley.MassChanged += OnMassChanged;
            Refresh();
        }

        private void OnDisable() => pulley.MassChanged -= OnMassChanged;

        private void OnMassChanged(Pulley changed) => Refresh();

        private void Refresh()
        {
            float height = pulley.MassKg * heightPerKgMetres;
            bool visible = height >= MinVisibleHeightMetres;
            weights.gameObject.SetActive(visible);
            if (!visible) return;

            Vector3 scale = weights.localScale;
            scale.y = height / meshHeightAtUnitScale;
            weights.localScale = scale;

            Vector3 position = weights.localPosition;
            position.y = platformTopY + height * 0.5f; // stack grows upward from the platform
            weights.localPosition = position;
        }
    }
}
