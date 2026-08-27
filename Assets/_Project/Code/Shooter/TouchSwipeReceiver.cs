using System;
using UnityEngine;
using Unity.Mathematics;
using Eleven.Ball;

namespace Eleven.Shooter
{
    /// <summary>
    /// Component thu thập và phân tích cử chỉ vuốt hoàn chỉnh:
    /// - Sử dụng SwipeCollector chuẩn hoá DPI và thu thập mẫu thời gian thực
    /// - Chạy SwipeAnalyzer phân tích các đặc trưng cử chỉ (Curvature, Straightness, PeakSpeed)
    /// - Chiếu tia AimProjector qua Camera tới mặt phẳng khung thành (Z = 11.0m)
    /// - Sử dụng ShotMapper phân loại 4 kiểu sút (Má trong, Knuckleball, Lốp bóng Panenka, Mu bàn chân)
    /// - Bộ giải quỹ đạo bù trừ khí động học (Aerodynamic Inversion Solver) đảm bảo bóng bay đúng ý đồ
    /// </summary>
    public sealed class TouchSwipeReceiver : MonoBehaviour
    {
        public event Action<ShotIntent, float3> OnShotFired;

        [Header("Cấu hình ánh xạ cử chỉ")]
        [SerializeField] private ShotMappingConfig mappingConfig;

        [Header("Độ nhạy & Giới hạn")]
        [SerializeField] private float minSwipeDistCm = 0.5f;

        private SwipeCollector collector;
        private bool isSwiping;
        private bool isInputEnabled = true;
        private uint kickSeed = 1001;

        public bool IsInputEnabled
        {
            get => isInputEnabled;
            set => isInputEnabled = value;
        }

        public ShotMappingConfig Config
        {
            get => mappingConfig != null ? mappingConfig : (mappingConfig = ShotMappingConfig.CreateDefault());
            set => mappingConfig = value;
        }

        private void Awake()
        {
            collector = new SwipeCollector(256);
            if (mappingConfig == null)
            {
                mappingConfig = ShotMappingConfig.CreateDefault();
            }
        }

        private void OnDestroy()
        {
            collector?.Dispose();
            collector = null;
        }

        private void Update()
        {
            if (!isInputEnabled) return;

            float dpi = PhysicalUnits.Dpi;
            float time = Time.time;

            // 1. Bắt đầu vuốt (Touch Down / Mouse Down)
            if (Input.GetMouseButtonDown(0))
            {
                float2 screenPos = new float2(Input.mousePosition.x, Input.mousePosition.y);
                collector.Begin(screenPos, time, dpi);
                isSwiping = true;
            }
            // 2. Di chuyển ngón tay (Drag / Move)
            else if (Input.GetMouseButton(0) && isSwiping)
            {
                float2 screenPos = new float2(Input.mousePosition.x, Input.mousePosition.y);
                collector.Move(screenPos, time);
            }
            // 3. Nhấc ngón tay (Touch Up / Mouse Up)
            else if (Input.GetMouseButtonUp(0) && isSwiping)
            {
                isSwiping = false;
                float2 screenPos = new float2(Input.mousePosition.x, Input.mousePosition.y);
                SwipeResult result = collector.End(screenPos, time);

                if (result.valid && result.features.length >= minSwipeDistCm)
                {
                    ProcessShot(result.features, screenPos);
                }
            }
        }

        private void ProcessShot(in SwipeFeatures features, float2 endScreenPos)
        {
            kickSeed = (kickSeed * 1664525u + 1013904223u);

            // 1. Phép chiếu màn hình -> Điểm ngắm khung thành thế giới qua AimProjector
            var cam = Camera.main;
            float3 rawAimPoint;
            if (!AimProjector.TryScreenToGoalPlane(new Vector2(endScreenPos.x, endScreenPos.y), cam, 11.0f, out rawAimPoint))
            {
                rawAimPoint = new float3(0f, 1.22f, 11.0f);
            }

            // 2. Ánh xạ đặc trưng cử chỉ thành ShotIntent hoàn chỉnh
            var cfg = Config;
            ShotIntent intent = ShotMapper.Map(in features, rawAimPoint, cfg, timingError: 0f, seed: kickSeed);

            // 3. Tính toán vận tốc phóng ban đầu có bù trừ lực cản khí động học và trọng lực
            float3 launchVelocity = SolveLaunchVelocity(in intent, BallParams.Default);

            isInputEnabled = false; // Khóa input khi bóng đang bay
            OnShotFired?.Invoke(intent, launchVelocity);
        }

        /// <summary>
        /// Bộ giải vận tốc phóng ban đầu chuẩn xác:
        /// Kết hợp giải tích sơ bộ và 1 bước hiệu chỉnh vi phân qua TrajectoryPredictor
        /// </summary>
        public static float3 SolveLaunchVelocity(in ShotIntent intent, in BallParams p)
        {
            float3 origin = new float3(0f, p.radius, 0f);
            float3 target = intent.aimPoint;

            // Xử lý riêng cú Lốp bóng (Chip Shot)
            if (intent.type == ShotType.Chip)
            {
                float chipSpeed = math.clamp(intent.speed * 0.72f, 14f, 18f);
                float flightTime = 11.0f / math.max(8f, chipSpeed * 0.82f);
                float g = p.gravity;
                float vy = (target.y - origin.y + 0.5f * g * flightTime * flightTime + 0.65f) / flightTime;
                float vx = target.x / flightTime;
                float vz = 11.0f / flightTime;
                return new float3(vx, vy, vz);
            }

            // 1. Dự tính vận tốc tới trước trung bình
            float speed = math.clamp(intent.speed, 18f, 36f);
            float vzEst = speed * 0.94f;
            float tEst = 11.0f / vzEst;

            // 2. Bù trừ trọng lực có tính đến độ trễ rơi do lực cản
            float gComp = p.gravity * (1.0f + 0.08f * (tEst / 0.4f));
            float vyEst = (target.y - origin.y + 0.5f * gComp * tEst * tEst) / tEst;

            // 3. Bù trừ độ lệch ngang từ lực xoáy Magnus: F_m = 0.5 * rho * Cl * A * r * (spinY * vz)
            float area = math.PI * p.radius * p.radius;
            float magnusAccX = (0.5f * p.airDensity * p.liftCoefficient * area * p.radius / p.mass) * (intent.spin.y * vzEst);
            float magnusOffsetX = 0.5f * magnusAccX * tEst * tEst;
            float vxEst = (target.x - magnusOffsetX) / tEst;

            // Chuẩn hoá độ lớn vận tốc theo intent.speed
            float currentMagSq = vxEst * vxEst + vyEst * vyEst + vzEst * vzEst;
            float scale = currentMagSq > 0.01f ? speed / math.sqrt(currentMagSq) : 1f;
            float3 initVel = new float3(vxEst * scale, vyEst * scale, vzEst * scale);

            // 4. Bước hiệu chỉnh vi phân 1 lượt (Refinement Step qua TrajectoryPredictor)
            var testState = new BallState(origin, initVel, intent.spin);
            if (TrajectoryPredictor.FirstCrossing(in testState, in p, 11.0f, 1f / 120f, out float3 hitPoint, out float actualTime))
            {
                if (actualTime > 0.05f)
                {
                    float errX = target.x - hitPoint.x;
                    float errY = target.y - hitPoint.y;
                    initVel.x += errX / actualTime;
                    initVel.y += errY / actualTime;
                }
            }

            return initVel;
        }
    }
}
