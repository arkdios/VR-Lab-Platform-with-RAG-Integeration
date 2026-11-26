using PhysicsLab.Lab.Rope;
using UnityEngine;

namespace PhysicsLab.Lab
{
    /// <summary>
    /// Runs the practice strings only while the player is near the practice bench.
    /// The practice bench is a full copy of the lab, so it has its own strings. A string costs work in every
    /// physics step. When the player is at the lab bench, the practice strings are far away and nobody can
    /// see them, so they are switched off. Then the lab costs the same as before the practice bench existed.
    /// Switching a string on again rebuilds its shape (RopeStrand does that in OnEnable).
    /// Put it on the PracticeBench root object. The Editor builder does that.
    /// </summary>
    public class PracticeStrandGate : MonoBehaviour
    {
        [Tooltip("The middle of the practice table. The distance to the player is measured to this point.")]
        [SerializeField] private Transform tableCentre;
        [Tooltip("The strings switch on when the player is closer than this, in metres.")]
        [SerializeField] private float switchOnMetres = 3f;
        [Tooltip("The strings switch off when the player is farther than this, in metres. A bit more than the on distance, so they never flicker.")]
        [SerializeField] private float switchOffMetres = 3.6f;
        [Tooltip("How often the distance is checked, in seconds.")]
        [SerializeField] private float checkEverySeconds = 0.5f;

        private RopeStrand[] _strands;
        private bool _stringsOn = true;
        private float _nextCheckTime;

        private void Awake() => _strands = GetComponentsInChildren<RopeStrand>(true);

        // At the start the player is far away, so the strings start switched off.
        private void Start() => SetStrings(false);

        private void Update()
        {
            if (Time.unscaledTime < _nextCheckTime) return;

            _nextCheckTime = Time.unscaledTime + checkEverySeconds;
            Camera head = Camera.main;
            if (head == null || tableCentre == null) return;

            Vector3 away = head.transform.position - tableCentre.position;
            away.y = 0f;   // only the distance along the floor counts, not the height of the head
            float distance = away.magnitude;

            if (_stringsOn && distance > switchOffMetres) SetStrings(false);
            else if (!_stringsOn && distance < switchOnMetres) SetStrings(true);
        }

        private void SetStrings(bool on)
        {
            _stringsOn = on;
            foreach (RopeStrand strand in _strands)
            {
                if (strand != null) strand.enabled = on;
            }
        }
    }
}
