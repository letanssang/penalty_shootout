using Unity.Mathematics;
using UnityEngine;

namespace Eleven.Keeper
{
    /// <summary>
    /// Reads real bone transforms every frame and produces a deterministic
    /// <see cref="KeeperCues"/> snapshot. Zero GC allocations in <see cref="Sample"/>.
    ///
    /// Attach to the kicker GameObject (or a dedicated cue reader object) and wire
    /// the bone references in the Inspector.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KickerBoneCueSource : MonoBehaviour, ICueSource
    {
        // ── Inspector references ────────────────────────────────────────
        [Header("Kicker Bone References")]
        [Tooltip("Root transform of the kicker (used for approach angle & run-up length).")]
        [SerializeField] private Transform root;

        [Tooltip("Plant-foot bone (e.g. left foot for a right-footed kicker).")]
        [SerializeField] private Transform plantFoot;

        [Tooltip("Hips / pelvis bone.")]
        [SerializeField] private Transform hips;

        [Header("Ball & Run-Up")]
        [Tooltip("World-space position of the ball.")]
        public float3 ballPosition;

        [Tooltip("Total run-up duration in seconds.")]
        public float runUpDuration = 1f;

        [Header("Debug Visualisation")]
        [Tooltip("Draw Gizmos in the Scene view.")]
        public bool drawDebugGizmos;

        [Tooltip("Draw an on-screen GUI overlay with numeric cue values.")]
        public bool drawDebugGUI;

        // ── Internal state (no heap allocs) ─────────────────────────────
        private float3 _runUpOrigin;
        private bool   _runUpStarted;
        private KeeperCues _lastCues;

        // ── Public helpers ──────────────────────────────────────────────

        /// <summary>Call once at the moment the kicker begins their run-up
        /// so that runUpLength can be measured.</summary>
        public void StartRunUp()
        {
            _runUpOrigin  = WorldPos(root);
            _runUpStarted = true;
        }

        /// <summary>Assign bone references from code (useful for runtime spawning / tests).</summary>
        public void SetBones(Transform rootBone, Transform plantFootBone, Transform hipsBone)
        {
            root      = rootBone;
            plantFoot = plantFootBone;
            hips      = hipsBone;
        }

        // ── ICueSource ──────────────────────────────────────────────────

        /// <summary>
        /// Produces a <see cref="KeeperCues"/> snapshot from current world-space bone transforms.
        /// Zero GC allocation.
        /// </summary>
        public KeeperCues Sample(float timeToContact)
        {
            float3 shotDir = new float3(0f, 0f, 1f);
            float3 lateralAxis = new float3(1f, 0f, 0f);

            // ── Plant-foot lateral offset ───────────────────────────────
            float3 footPos = WorldPos(plantFoot);
            float3 footToBall = footPos - ballPosition;
            float lateralOffset = math.dot(footToBall, lateralAxis);

            // ── Hip yaw ─────────────────────────────────────────────────
            float3 hipsForward = Forward(hips);
            float hipYaw = SignedAngleXZDeg(hipsForward, shotDir);

            // ── Approach angle ──────────────────────────────────────────
            float3 rootPos = WorldPos(root);
            float3 rootToBall = math.normalizesafe(ballPosition - rootPos, shotDir);
            float approachAngle = SignedAngleXZDeg(rootToBall, shotDir);

            // ── Run-up length ───────────────────────────────────────────
            float runLength = 0f;
            if (_runUpStarted)
            {
                runLength = math.distance(_runUpOrigin, rootPos);
            }

            // ── Observability ───────────────────────────────────────────
            float totalDur = math.max(runUpDuration, math.EPSILON);
            float obs = math.saturate(1f - timeToContact / totalDur);

            // ── Assemble struct ─────────────────────────────────────────
            KeeperCues cues;
            cues.plantFootLateralOffset = lateralOffset;
            cues.hipYawDegrees          = hipYaw;
            cues.approachAngleDegrees   = approachAngle;
            cues.runUpLength            = runLength;
            cues.timeToContact          = timeToContact;
            cues.observability          = obs;

            _lastCues = cues;
            return cues;
        }

        // ── Deterministic math helpers (no GC) ─────────────────────────

        private static float3 WorldPos(Transform t)
        {
            if (t == null) return float3.zero;
            Vector3 p = t.position;
            return new float3(p.x, p.y, p.z);
        }

        private static float3 Forward(Transform t)
        {
            if (t == null) return new float3(0f, 0f, 1f);
            Vector3 f = t.forward;
            return new float3(f.x, f.y, f.z);
        }

        private static float SignedAngleXZDeg(float3 from, float3 to)
        {
            float2 a = math.normalizesafe(new float2(from.x, from.z));
            float2 b = math.normalizesafe(new float2(to.x, to.z));

            float cross = a.x * b.y - a.y * b.x;
            float dot   = math.dot(a, b);

            return math.degrees(math.atan2(cross, dot));
        }

        // ── Debug: Gizmos (Scene view) ──────────────────────────────────

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!drawDebugGizmos) return;
            if (root == null || plantFoot == null || hips == null) return;

            Vector3 ballV3    = new Vector3(ballPosition.x, ballPosition.y, ballPosition.z);
            Vector3 footV3    = plantFoot.position;
            Vector3 rootV3    = root.position;
            Vector3 hipsV3    = hips.position;
            Vector3 hipsForV3 = hips.forward;

            // Ball
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(ballV3, 0.11f);

            // Plant foot → ball lateral line
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(footV3, ballV3);
            Gizmos.DrawSphere(footV3, 0.05f);

            // Hip forward
            Gizmos.color = Color.magenta;
            Gizmos.DrawRay(hipsV3, hipsForV3 * 0.6f);

            // Approach direction (root → ball)
            Gizmos.color = Color.yellow;
            Vector3 approach = (ballV3 - rootV3).normalized;
            Gizmos.DrawRay(rootV3, approach * 1.2f);

            // Shot direction (+Z)
            Gizmos.color = Color.green;
            Gizmos.DrawRay(ballV3, Vector3.forward * 1.5f);

            // Run-up origin
            if (_runUpStarted)
            {
                Gizmos.color = Color.red;
                Vector3 originV3 = new Vector3(_runUpOrigin.x, _runUpOrigin.y, _runUpOrigin.z);
                Gizmos.DrawWireSphere(originV3, 0.08f);
                Gizmos.DrawLine(originV3, rootV3);
            }
        }
#endif

        // ── Debug: On-screen GUI overlay ────────────────────────────────

        private void OnGUI()
        {
            if (!drawDebugGUI) return;

            const float w = 340f;
            const float h = 160f;
            Rect rect = new Rect(10f, 10f, w, h);

            GUI.Box(rect, "Keeper Cues (T17)");

            float y = 24f;
            const float lineH = 20f;
            const float x = 16f;

            GUI.Label(new Rect(x, y, w, lineH), $"plantFootLateralOffset: {_lastCues.plantFootLateralOffset:F3} m");
            y += lineH;
            GUI.Label(new Rect(x, y, w, lineH), $"hipYawDegrees:          {_lastCues.hipYawDegrees:F2}°");
            y += lineH;
            GUI.Label(new Rect(x, y, w, lineH), $"approachAngleDegrees:   {_lastCues.approachAngleDegrees:F2}°");
            y += lineH;
            GUI.Label(new Rect(x, y, w, lineH), $"runUpLength:            {_lastCues.runUpLength:F3} m");
            y += lineH;
            GUI.Label(new Rect(x, y, w, lineH), $"timeToContact:          {_lastCues.timeToContact:F3} s");
            y += lineH;
            GUI.Label(new Rect(x, y, w, lineH), $"observability:          {_lastCues.observability:F3}");
        }
    }
}
