using System;
using PhysicsLab.Core.Telemetry;

namespace PhysicsLab.Core.State
{
    /// <summary>
    /// Record the event that happened. Field names are the wire format.
    /// </summary>
    [Serializable]
    public class EventRecord
    {
        public long t_ms;
        public string type = string.Empty;
        public string subject = string.Empty;
        public string detail = string.Empty;
    }

    /// <summary>
    /// Fixed-size log that keeps the newest events and overwrites the oldest.
    /// Plain C# class so it has no per-frame cost.
    /// </summary>
    public class EventLog
    {
        private readonly EventRecord[] _slots;
        private int _nextIndex;

        public EventLog(int capacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _slots = new EventRecord[capacity];
        }

        public int Count { get; private set; }

        public void Add(string type, string subject, string detail = "")
        {
            _slots[_nextIndex] = new EventRecord
            {
                t_ms = SessionClock.NowMs,
                type = type,
                subject = subject,
                detail = detail
            };
            _nextIndex = (_nextIndex + 1) % _slots.Length;
            Count = Math.Min(Count + 1, _slots.Length);
        }

        /// <summary>
        /// Returns false instead of null for an empty log.
        /// </summary>
        /// <param name="latest" is the latest event that has just happened></param>
        /// <returns>Boolean value, false if it is an empty log</returns>
        public bool TryGetLatest(out EventRecord latest)
        {
            latest = Count == 0 ? null : _slots[(_nextIndex - 1 + _slots.Length) % _slots.Length];
            return latest != null;
        }

        /// <summary>
        /// Returns up to <paramref name="maxCount"/> newest events, oldest first.
        /// In short, the method copies the newest N entries such that the backend
        /// reads events in the order they happened.
        /// </summary>
        public EventRecord[] SnapshotLast(int maxCount)
        {
            int take = Math.Min(maxCount, Count);
            var result = new EventRecord[take];
            int firstIndex = (_nextIndex - take + _slots.Length) % _slots.Length;
            for (int i = 0; i < take; i++)
            {
                result[i] = _slots[(firstIndex + i) % _slots.Length];
            }
            return result;
        }
    }
}