namespace Eleven.Keeper
{
    /// <summary>
    /// Value-type snapshot of visual cues the goalkeeper can read from the kicker's body
    /// during the run-up phase. All values are computed from world-space bone transforms
    /// — never from ShotIntent or any other "answer key".
    /// </summary>
    public struct KeeperCues
    {
        /// <summary>Lateral offset (metres) of the plant foot from the ball,
        /// projected onto the axis perpendicular to the shot direction.</summary>
        public float plantFootLateralOffset;

        /// <summary>Yaw of the hips (degrees) relative to the straight shot direction (+Z).</summary>
        public float hipYawDegrees;

        /// <summary>Angle (degrees) of the run-up velocity vector relative to straight-on (+Z).</summary>
        public float approachAngleDegrees;

        /// <summary>Total run-up distance (metres).</summary>
        public float runUpLength;

        /// <summary>Seconds remaining until foot-ball contact.</summary>
        public float timeToContact;

        /// <summary>How much of the cue information is currently visible (0 = nothing, 1 = fully revealed).
        /// Monotonically increases as timeToContact decreases.</summary>
        public float observability;
    }
}
