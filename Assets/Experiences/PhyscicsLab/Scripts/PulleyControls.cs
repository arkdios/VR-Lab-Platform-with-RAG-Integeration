using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PhysicsLab.Lab
{
    /// <summary>
    /// Four buttons and a label that change one pulley's angle and hanging mass.
    /// </summary>
    public class PulleyControls : MonoBehaviour
    {
        [SerializeField] private Pulley pulley;
        [SerializeField] private TMP_Text label;
        [SerializeField] private Button angleDownButton;
        [SerializeField] private Button angleUpButton;
        [SerializeField] private Button massDownButton;
        [SerializeField] private Button massUpButton;
        [SerializeField] private float angleStepDegrees = 15f;
        [SerializeField] private float massStepKg = 0.05f;

        private void Awake()
        {
            angleDownButton.onClick.AddListener(() => pulley.SetAngle(pulley.AngleDegrees - angleStepDegrees));
            angleUpButton.onClick.AddListener(() => pulley.SetAngle(pulley.AngleDegrees + angleStepDegrees));
            massDownButton.onClick.AddListener(() => pulley.SetMass(pulley.MassKg - massStepKg));
            massUpButton.onClick.AddListener(() => pulley.SetMass(pulley.MassKg + massStepKg));
        }

        private void OnEnable()
        {
            pulley.AngleChanged += OnPulleyChanged;
            pulley.MassChanged += OnPulleyChanged;
            Refresh();
        }

        private void OnDisable()
        {
            pulley.AngleChanged -= OnPulleyChanged;
            pulley.MassChanged -= OnPulleyChanged;
        }

        private void OnPulleyChanged(Pulley changed) => Refresh();

        private void Refresh() =>
            label.text = $"{pulley.ObjectId}: {pulley.AngleDegrees:0} deg, {pulley.MassKg * 1000f:0} g";
    }
}