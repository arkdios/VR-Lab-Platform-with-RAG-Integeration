using UnityEngine;

namespace PhysicsLab.Lab
{
    /// <summary>
    /// Shows the real hooked-mass model (50 g, 100 g or 200 g) that is nearest to the pulley's mass.
    /// Visual only. The physics body keeps the exact mass. Put it on the mass object (the one the string ties to).
    /// </summary>
    public class HookedMassVisual : MonoBehaviour
    {
        private const float NominalSmallKg = 0.05f;
        private const float NominalMediumKg = 0.10f;
        private const float NominalLargeKg = 0.20f;
        private const float HideBelowKg = 0.025f;

        [SerializeField] private Pulley pulley;
        [SerializeField] private GameObject model50g;
        [SerializeField] private GameObject model100g;
        [SerializeField] private GameObject model200g;

        private void OnEnable()
        {
            pulley.MassChanged += OnMassChanged;
            Refresh();
        }

        private void OnDisable() => pulley.MassChanged -= OnMassChanged;

        private void OnMassChanged(Pulley changed) => Refresh();

        private void Refresh()
        {
            float kg = pulley.MassKg;
            GameObject chosen = null;
            if (kg >= HideBelowKg)
            {
                float toSmall = Mathf.Abs(kg - NominalSmallKg);
                float toMedium = Mathf.Abs(kg - NominalMediumKg);
                float toLarge = Mathf.Abs(kg - NominalLargeKg);
                if (toSmall <= toMedium && toSmall <= toLarge) chosen = model50g;
                else chosen = toMedium <= toLarge ? model100g : model200g;
            }

            model50g.SetActive(chosen == model50g);
            model100g.SetActive(chosen == model100g);
            model200g.SetActive(chosen == model200g);
        }
    }
}
