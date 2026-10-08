namespace PhysicsLab.Lab
{
    /// <summary>
    /// Event type names sent to the backend.
    /// </summary>
    public static class LabEventTypes
    {
        public const string Grab = "grab";
        public const string Release = "release";
        public const string Click = "click";
        public const string RingMoved = "ring_moved";
        public const string PulleyAngleChanged = "pulley_angle_changed";
        public const string MassChanged = "mass_changed";
        public const string StepChanged = "step_changed";
        public const string MeasurementRecorded = "measurement_recorded";
    }
}