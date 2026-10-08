using UnityEngine;

namespace PhysicsLab.Core.Telemetry
{
    /// <summary>
    /// One clock for every timestamp, counting in milliseconds since the app started.
    /// </summary>
    public static class SessionClock
    {
        public static long NowMs => (long)(Time.realtimeSinceStartupAsDouble * 1000.0);
    }
}