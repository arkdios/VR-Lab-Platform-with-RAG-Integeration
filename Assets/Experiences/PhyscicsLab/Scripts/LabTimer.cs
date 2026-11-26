using System;
using TMPro;
using UnityEngine;

namespace PhysicsLab.Lab
{
    /// <summary>
    /// The lab countdown. It starts when the student presses the Student button on the role screen,
    /// and counts down to 0:00:00. It shows the time in a label on the student screen.
    /// When it reaches zero it raises Expired once. LabFinishController listens and shows the "time is up" notice.
    /// It uses unscaled time, so it does not stop if the game speed changes.
    /// It is not saved. If the page is reloaded, the time starts again from the full time.
    /// </summary>
    public class LabTimer : MonoBehaviour
    {
        /// <summary>2 hours 50 minutes in seconds, shown as 2:50:00. The guide boards read this number too.</summary>
        public const float DefaultTotalSeconds = 10200f;

        [SerializeField] private RoleScreens roleScreens;
        [SerializeField] private TMP_Text timerLabel;
        [Tooltip("The lab time in seconds. 10200 is 2 hours 50 minutes.")]
        [SerializeField] private float totalSeconds = DefaultTotalSeconds;
        [Tooltip("The label changes colour when this many seconds are left.")]
        [SerializeField] private float warningSeconds = 600f;
        [SerializeField] private Color normalColour = Color.white;
        [SerializeField] private Color warningColour = new Color(1f, 0.55f, 0.25f, 1f);

        private bool _started;       // the countdown began (it begins only once)
        private bool _running;
        private float _endTime;      // the Time.unscaledTime value when the time runs out
        private float _remaining;
        private int _shownSeconds = -1;   // the whole seconds the label shows now, so the text changes once per second

        /// <summary>Raised once when the time reaches zero.</summary>
        public event Action Expired;

        public bool IsRunning => _running;
        public bool HasExpired { get; private set; }
        public float RemainingSeconds => _remaining;
        public float UsedSeconds => totalSeconds - _remaining;

        private void Awake() => _remaining = totalSeconds;

        private void OnEnable()
        {
            if (roleScreens != null) roleScreens.RoleChosen += OnRoleChosen;
            ShowTime();
        }

        private void OnDisable()
        {
            if (roleScreens != null) roleScreens.RoleChosen -= OnRoleChosen;
        }

        // Only the first press of Student starts it. Going back to the role screen and choosing Student again does not restart it.
        private void OnRoleChosen(UserRole role)
        {
            if (role == UserRole.Student) Begin();
        }

        /// <summary>Starts the countdown. It does nothing when it already started.</summary>
        public void Begin()
        {
            if (_started) return;

            _started = true;
            _running = true;
            _endTime = Time.unscaledTime + totalSeconds;
        }

        /// <summary>Stops the countdown and keeps the time that was left.</summary>
        public void Stop()
        {
            if (!_running) return;

            UpdateRemaining();
            _running = false;
            ShowTime();
        }

        private void Update()
        {
            if (!_running) return;

            UpdateRemaining();
            ShowTime();
            if (_remaining > 0f) return;

            _running = false;
            HasExpired = true;
            Expired?.Invoke();
        }

        private void UpdateRemaining() => _remaining = Mathf.Max(0f, _endTime - Time.unscaledTime);

        // The text is made again only when the whole second changes.
        private void ShowTime()
        {
            if (timerLabel == null) return;

            int wholeSeconds = Mathf.CeilToInt(_remaining);
            if (wholeSeconds == _shownSeconds) return;

            _shownSeconds = wholeSeconds;
            timerLabel.text = "Time left  " + FormatClock(wholeSeconds);
            timerLabel.color = _remaining <= warningSeconds ? warningColour : normalColour;
        }

        /// <summary>Writes seconds as H:MM:SS, for example 10200 gives 2:50:00.</summary>
        public static string FormatClock(int seconds)
        {
            seconds = Mathf.Max(0, seconds);
            return $"{seconds / 3600}:{seconds % 3600 / 60:00}:{seconds % 60:00}";
        }
    }
}
