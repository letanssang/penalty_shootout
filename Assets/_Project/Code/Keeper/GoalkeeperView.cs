using System;
using UnityEngine;
using Unity.Mathematics;
using Eleven.Ball;
using Eleven.Keeper;

namespace Eleven.Keeper
{
    /// <summary>
    /// Component quản lý hiển thị và chuyển động bay người cản phá của Thủ môn trong Scene 3D.
    /// </summary>
    public sealed class GoalkeeperView : MonoBehaviour
    {
        [Header("Thông số hình thể & Vị trí")]
        [SerializeField] private Vector3 homePosition = new Vector3(0f, 0.95f, 11.0f);
        [SerializeField] private float diveSpeed = 5.5f;

        private Vector3 currentVelocity;
        private Vector3 targetDivePos;
        private bool isDiving;
        private BayesianKeeperBrain brain;
        private KeeperProfile profile;
        private ShotHistory history;

        public Vector3 CurrentPosition => transform.position;

        private void Awake()
        {
            ResetToHome();

            // Khởi tạo Não thủ môn với thông số độ khó chuẩn
            profile = ScriptableObject.CreateInstance<KeeperProfile>();
            profile.readAccuracy = 0.55f; // 55% đoán đúng hướng
            profile.reactionMs = 220f;    // 220ms phản xạ
            profile.commitOffsetMs = -80f;// Cam kết trước lúc bóng bay
            profile.memoryWeight = 0.4f;

            brain = new BayesianKeeperBrain();
            history = new ShotHistory();
        }

        public void ResetToHome()
        {
            isDiving = false;
            transform.position = homePosition;
            transform.rotation = Quaternion.Euler(0f, 180f, 0f); // Nhìn về phía chấm 11m
            targetDivePos = homePosition;
        }

        /// <summary>
        /// Kích hoạt phản xạ bay người cản phá khi người chơi sút bóng.
        /// </summary>
        public void ReactToShot(float3 launchVelocity, float3 spin, uint seed)
        {
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
            float diveTargetX = Mathf.Clamp(cellCenter.x * 0.85f, -3.2f, 3.2f);
            float diveTargetY = Mathf.Clamp(cellCenter.y, 0.4f, 2.2f);

            targetDivePos = new Vector3(diveTargetX, diveTargetY, 11.0f);
            isDiving = true;
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
                    0.20f,
                    diveSpeed
                );

                // Nghiêng người theo hướng bay
                float tiltAngle = (targetDivePos.x - homePosition.x) * -15.0f;
                transform.rotation = Quaternion.Euler(0f, 180f, tiltAngle);
            }
        }
    }
}
