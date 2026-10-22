using TMPro;
using UnityEngine;

namespace PhysicsLab.Lab
{
    /// <summary>
    /// Shows the spring scale reading on a world-space TextMeshPro label, refreshed 10 times per second.
    /// The label is a child of the scale's Model so it turns with it.
    /// </summary>
    public class SpringScaleReadout : MonoBehaviour
    {
        [SerializeField] private SpringScale scale;
        [SerializeField] private TMP_Text label;
        [SerializeField] private float refreshSeconds = 0.1f;

        private float _secondsUntilRefresh;

        private void Awake()
        {
            if (scale != null && label != null) return;
            Debug.LogError("[SpringScaleReadout] Assign Scale and Label.", this);
            enabled = false;
        }

        private void OnEnable() => _secondsUntilRefresh = 0f;

        private void Update()
        {
            _secondsUntilRefresh -= Time.unscaledDeltaTime;
            if (_secondsUntilRefresh > 0f) return;
            _secondsUntilRefresh = refreshSeconds;
            // The float overload formats without creating a string, so no garbage.
            label.SetText("{0:2} N", scale.ReadingNewtons);
        }
    }
}
