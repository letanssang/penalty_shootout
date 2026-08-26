using Unity.Mathematics;

namespace Eleven.Keeper
{
    /// <summary>
    /// A pure-C# fake <see cref="ICueSource"/> that requires no GameObject, no
    /// MonoBehaviour, and no animation. Useful for deterministic unit / edit-mode tests.
    ///
    /// Two modes of operation:
    /// 1. Fixed mode – returns exactly the FixedCues snapshot with timeToContact overwritten by parameter.
    /// 2. Interpolated mode – linearly interpolates fields between StartCues and FixedCues.
    /// </summary>
    public sealed class MockCueSource : ICueSource
    {
        // ── Fixed-mode configuration ────────────────────────────────────
        /// <summary>Baseline cues returned in fixed mode or used as the "end" snapshot
        /// (timeToContact == 0) in interpolated mode.</summary>
        public KeeperCues FixedCues;

        // ── Interpolated-mode configuration ─────────────────────────────
        /// <summary>When true, Sample linearly interpolates between StartCues and FixedCues based on progress.</summary>
        public bool Interpolate;

        /// <summary>Cue snapshot at the very beginning of the run-up (timeToContact == RunUpDuration).</summary>
        public KeeperCues StartCues;

        /// <summary>Total run-up duration in seconds. Used both for observability and for progress calculation.</summary>
        public float RunUpDuration = 1f;

        // ── ICueSource ──────────────────────────────────────────────────
        public KeeperCues Sample(float timeToContact)
        {
            float totalDuration = math.max(RunUpDuration, math.EPSILON);
            float t = math.saturate(1f - timeToContact / totalDuration); // 0 → 1

            KeeperCues cues;

            if (Interpolate)
            {
                cues.plantFootLateralOffset = math.lerp(StartCues.plantFootLateralOffset, FixedCues.plantFootLateralOffset, t);
                cues.hipYawDegrees          = math.lerp(StartCues.hipYawDegrees, FixedCues.hipYawDegrees, t);
                cues.approachAngleDegrees   = math.lerp(StartCues.approachAngleDegrees, FixedCues.approachAngleDegrees, t);
                cues.runUpLength            = math.lerp(StartCues.runUpLength, FixedCues.runUpLength, t);
            }
            else
            {
                cues.plantFootLateralOffset = FixedCues.plantFootLateralOffset;
                cues.hipYawDegrees          = FixedCues.hipYawDegrees;
                cues.approachAngleDegrees   = FixedCues.approachAngleDegrees;
                cues.runUpLength            = FixedCues.runUpLength;
            }

            cues.timeToContact = timeToContact;
            cues.observability = math.saturate(1f - timeToContact / totalDuration);

            return cues;
        }
    }
}
