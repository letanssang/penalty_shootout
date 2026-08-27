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

        public Vector3 CurrentPosition => transform.position;

        private void Awake()
        {
            ResetToHome();

            // Khởi tạo Não thủ môn với thông số độ khó chuẩn
            profile = ScriptableObject.CreateInstance<KeeperProfile>();
            profile.readAccuracy = 0.55f; // 55% đoán đúng hướng
            profile.reactionMs = 220f;    // 220ms phản xạ
            profile.commitOffsetMs = -80f;// Cam kết trước lúc bóng bay

            brain = new BayesianKeeperBrain(profile);
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
            var cue = new KeeperCue
            {
                hipAngle = Mathf.Atan2(launchVelocity.x, launchVelocity.z),
                plantFootOffset = new float3(launchVelocity.x > 0 ? -0.3f : 0.3f, 0f, 0f),
                runUpAngle = 0.1f
            };

            var read = brain.Infer(cue, seed);

            // Dự đoán ô mục tiêu (0..8)
            int targetCell = read.targetCell;
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
