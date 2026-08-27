using System;
using UnityEngine;
using Unity.Mathematics;
using Eleven.Ball;
using Eleven.Keeper;

namespace Eleven.Keeper
{
    /// <summary>
    /// Component quản lý hiển thị và chuyển động bay người cản phá của Thủ môn trong Scene 3D.
    /// Tích hợp Bộ não suy luận Bayesian và xử lý va chạm đẩy bóng (Deflection).
    /// </summary>
    public sealed class GoalkeeperView : MonoBehaviour
    {
        [Header("Thông số hình thể & Vị trí")]
        [SerializeField] private Vector3 homePosition = new Vector3(0f, 0.95f, 11.0f);
        [SerializeField] private float diveSpeed = 6.5f;

        private Vector3 currentVelocity;
        private Vector3 targetDivePos;
        private bool isDiving;
        private BayesianKeeperBrain brain;
        private KeeperProfile profile;
        private ShotHistory history;
        private bool hasDeflected = false;

        public Vector3 CurrentPosition => transform.position;
        public bool HasDeflected => hasDeflected;

        private void Awake()
        {
            ResetToHome();

            // Khởi tạo Não thủ môn với thông số độ khó chuẩn
            profile = ScriptableObject.CreateInstance<KeeperProfile>();
            profile.readAccuracy = 0.55f; // 55% đoán đúng hướng
            profile.reactionMs = 200f;    // 200ms phản xạ
            profile.commitOffsetMs = -80f;// Cam kết trước lúc bóng bay
            profile.memoryWeight = 0.4f;

            brain = new BayesianKeeperBrain();
            history = new ShotHistory();
        }

        public void ResetToHome()
        {
            isDiving = false;
            hasDeflected = false;
            transform.position = homePosition;
            transform.rotation = Quaternion.Euler(0f, 180f, 0f); // Nhìn về phía chấm 11m
            targetDivePos = homePosition;
            currentVelocity = Vector3.zero;
        }

        /// <summary>
        /// Kích hoạt phản xạ bay người cản phá khi người chơi sút bóng.
        /// </summary>
        public void ReactToShot(float3 launchVelocity, float3 spin, uint seed)
        {
            hasDeflected = false;

            // Trích xuất tín hiệu và suy luận góc đổ người
            float latOffset = launchVelocity.x > 0 ? 0.18f : -0.18f;
            float hipYaw = Mathf.Atan2(launchVelocity.x, launchVelocity.z) * Mathf.Rad2Deg;

            var cues = new KeeperCues
            {
                plantFootLateralOffset = latOffset,
                hipYawDegrees = hipYaw,
                approachAngleDegrees = hipYaw * 0.8f,
                runUpLength = 3.5f,
                timeToContact = 0.05f,
                observability = 0.9f
            };

            var read = brain.Infer(cues, history, profile, seed);

            // Dự đoán ô mục tiêu (0..8)
            int targetCell = read.bestCell;
            float3 cellCenter = GoalFrame.CellCenter(targetCell);

            // Xác định điểm bay người mục tiêu
            float diveTargetX = Mathf.Clamp(cellCenter.x * 0.88f, -3.2f, 3.2f);
            float diveTargetY = Mathf.Clamp(cellCenter.y, 0.4f, 2.2f);

            targetDivePos = new Vector3(diveTargetX, diveTargetY, 11.0f);
            isDiving = true;
        }

        /// <summary>
        /// Kiểm tra và xử lý va chạm đẩy bóng (Parry/Deflect) khi bóng tới gần thủ môn
        /// </summary>
        public bool TryDeflectBall(ref float3 ballPos, ref float3 ballVel, float ballRadius = 0.11f)
        {
            if (hasDeflected) return false;

            float dist = Vector3.Distance((Vector3)(float3)ballPos, transform.position);
            if (dist <= 0.85f)
            {
                hasDeflected = true;
                float3 normal = math.normalize(ballPos - (float3)transform.position);
                if (math.lengthsq(normal) < 0.001f) normal = new float3(0f, 0.5f, -0.866f);

                // Đẩy bóng văng ngược ra ngoài và lệch hướng
                float speed = math.length(ballVel);
                ballVel = normal * (speed * 0.65f) + new float3(normal.x * 4f, 3.5f, -6.0f);
                return true;
            }

            return false;
        }

        private void Update()
        {
            if (isDiving)
            {
                // Di chuyển mượt mà tới vị trí bay người
                transform.position = Vector3.SmoothDamp(
                    transform.position,
                    targetDivePos,
                    ref currentVelocity,
                    0.18f,
                    diveSpeed
                );

                // Nghiêng người theo hướng bay
                float tiltAngle = (targetDivePos.x - homePosition.x) * -16.0f;
                transform.rotation = Quaternion.Euler(0f, 180f, tiltAngle);
            }
        }
    }
}
