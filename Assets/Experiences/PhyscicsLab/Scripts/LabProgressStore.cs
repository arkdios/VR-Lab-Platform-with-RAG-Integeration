using PhysicsLab.Core.Web;
using System;
using System.IO;
using UnityEngine;

namespace PhysicsLab.Lab
{
    /// <summary>
    /// Saves lab progress (current step, ring and spring scale position, pulley angles and masses)
    /// to a JSON file on the device, and puts it back at the next start.
    /// A save is queued when something changes and written once the changes stop, so a long
    /// drag writes the file one time. Nothing here is sent to the backend: the backend sees the
    /// same values in the normal payload (current_step, ring_position, pulleys, masses).
    /// </summary>
    public class LabProgressStore : MonoBehaviour
    {
        [Header("What to save")]
        [SerializeField] private LabStepTracker steps;
        [SerializeField] private LabStateTracker stateTracker;
        [SerializeField] private Transform tableCenter;
        [SerializeField] private Rigidbody ring;
        [SerializeField] private LabObject ringObject;
        [SerializeField] private Rigidbody springScaleBody;
        [SerializeField] private LabObject springScaleObject;
        [SerializeField] private Pulley[] pulleys;

        [Header("File")]
        [SerializeField] private string fileName = "lab_progress.json";
        [SerializeField] private bool restoreOnStart = true;
        [Tooltip("Wait this long after the last change before writing the file.")]
        [SerializeField] private float saveDelaySeconds = 1f;

        private string _path;
        private bool _dirty;
        private bool _restoring;
        private float _saveAtTime;

        public string FilePath => _path;

        private void Awake()
        {
            _path = Path.Combine(Application.persistentDataPath, fileName);
        }

        private void OnEnable() => SetSubscriptions(true);

        private void OnDisable() => SetSubscriptions(false);

        // Runs before Unity destroys anything, so the scene objects can still be read.
        // It also runs when you leave Play mode in the Editor.
        private void OnApplicationQuit()
        {
            if (_dirty) SaveNow();
        }

        private void Start()
        {
            if (restoreOnStart) TryRestore();
        }

        private void Update()
        {
            if (_dirty && Time.unscaledTime >= _saveAtTime) SaveNow();
        }

        // Headsets and phones pause the app instead of closing it.
        private void OnApplicationPause(bool paused)
        {
            if (paused && _dirty) SaveNow();
        }

        /// <summary>
        /// Writes the file now. Also used by the F7 hotkey.
        /// </summary>
        public void SaveNow()
        {
            _dirty = false;
            try
            {
                File.WriteAllText(_path, JsonUtility.ToJson(BuildData(), true));
                BrowserBridge.FlushFiles();
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                Debug.LogWarning($"[LabProgressStore] Could not save: {exception.Message}", this);
            }
        }

        /// <summary>
        /// Deletes the saved file, so the next start begins from the defaults.
        /// </summary>
        [ContextMenu("Clear saved progress")]
        public void ClearSavedFile()
        {
            _dirty = false;
            if (File.Exists(_path)) File.Delete(_path);
            Debug.Log($"[LabProgressStore] Cleared {_path}", this);
        }

        [ContextMenu("Log file path")]
        private void LogFilePath() => Debug.Log($"[LabProgressStore] {_path}", this);

        private void SetSubscriptions(bool subscribe)
        {
            if (subscribe)
            {
                steps.StepChanged += OnStepChanged;
                ringObject.HeldChanged += OnHeldChanged;
                springScaleObject.HeldChanged += OnHeldChanged;
                foreach (Pulley pulley in pulleys) pulley.AngleChanged += OnPulleyChanged;
                foreach (Pulley pulley in pulleys) pulley.MassChanged += OnPulleyChanged;
                return;
            }

            steps.StepChanged -= OnStepChanged;
            ringObject.HeldChanged -= OnHeldChanged;
            springScaleObject.HeldChanged -= OnHeldChanged;
            foreach (Pulley pulley in pulleys) pulley.AngleChanged -= OnPulleyChanged;
            foreach (Pulley pulley in pulleys) pulley.MassChanged -= OnPulleyChanged;
        }

        private void OnStepChanged(string previousId, string currentId) => MarkDirty();

        // Only a release changes where an object rests, so a grab does not queue a save.
        private void OnHeldChanged(LabObject labObject, bool held)
        {
            if (!held) MarkDirty();
        }

        private void OnPulleyChanged(Pulley pulley) => MarkDirty();

        private void MarkDirty()
        {
            if (_restoring) return; // putting values back is not a new change
            _dirty = true;
            _saveAtTime = Time.unscaledTime + saveDelaySeconds;
        }

        private LabProgressData BuildData() => new LabProgressData
        {
            step_index = steps.CurrentIndex,
            saved_at_unix_ms = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            bodies = new[]
            {
                CaptureBody(ringObject, ring),
                CaptureBody(springScaleObject, springScaleBody)
            },
            pulleys = CapturePulleys()
        };

        private SavedBody CaptureBody(LabObject labObject, Rigidbody body) => new SavedBody
        {
            id = labObject.ObjectId,
            table_position = tableCenter.InverseTransformPoint(body.position)
        };

        private SavedPulley[] CapturePulleys()
        {
            var saved = new SavedPulley[pulleys.Length];
            for (int i = 0; i < pulleys.Length; i++)
            {
                saved[i] = new SavedPulley
                {
                    id = pulleys[i].ObjectId,
                    angle_deg = pulleys[i].AngleDegrees,
                    mass_kg = pulleys[i].MassKg
                };
            }
            return saved;
        }

        /// <summary>
        /// Reads the file and applies it. A missing, broken or old file is skipped,
        /// so the lab always starts.
        /// </summary>
        private void TryRestore()
        {
            LabProgressData data = ReadFile();
            if (data == null) return;

            _restoring = true;
            Apply(data);
            _restoring = false;
            stateTracker.ResetLog(); // restored values are not student actions
            stateTracker.RecordEvent(LabEventTypes.ProgressRestored, "lab", steps.CurrentStepId);
        }

        private LabProgressData ReadFile()
        {
            if (!File.Exists(_path)) return null;

            try
            {
                LabProgressData data = JsonUtility.FromJson<LabProgressData>(File.ReadAllText(_path));
                if (data != null && data.version == LabProgressData.CurrentVersion) return data;
                Debug.LogWarning("[LabProgressStore] Saved file has another version. Ignored.", this);
            }
            catch (Exception exception) when (exception is ArgumentException || exception is IOException)
            {
                Debug.LogWarning($"[LabProgressStore] Saved file unreadable. Ignored. {exception.Message}", this);
            }
            return null;
        }

        private void Apply(LabProgressData data)
        {
            steps.TrySetIndex(data.step_index);
            if (data.bodies != null)
            {
                foreach (SavedBody saved in data.bodies) RestoreBody(saved);
            }
            if (data.pulleys != null)
            {
                foreach (SavedPulley saved in data.pulleys) RestorePulley(saved);
            }
        }

        private void RestoreBody(SavedBody saved)
        {
            Rigidbody body = BodyFor(saved.id);
            if (body == null) return; // an id from an older file

            Vector3 world = tableCenter.TransformPoint(saved.table_position);
            body.transform.position = world; // moves the drawn object and resets interpolation
            body.position = world;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        private Rigidbody BodyFor(string id)
        {
            if (id == ringObject.ObjectId) return ring;
            if (id == springScaleObject.ObjectId) return springScaleBody;
            return null;
        }

        private void RestorePulley(SavedPulley saved)
        {
            foreach (Pulley pulley in pulleys)
            {
                if (pulley.ObjectId != saved.id) continue;
                pulley.SetAngle(saved.angle_deg);
                pulley.SetMass(saved.mass_kg);
                return;
            }
        }
    }
}
