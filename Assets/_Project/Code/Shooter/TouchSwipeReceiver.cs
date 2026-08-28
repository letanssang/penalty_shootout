using System;
using Unity.Mathematics;
using UnityEngine;
using Eleven.Ball;

namespace Eleven.Shooter
{
    /// <summary>
    /// Tầng NHẬP LIỆU thuần: thu cử chỉ vuốt (T13) và cung cấp phép ánh xạ sang cú sút (T14).
    /// Lớp này KHÔNG quyết định lúc nào được sút — vòng lặp trận đấu quyết định, vì thời điểm
    /// nhả ngón tay còn phải đối chiếu với cửa sổ thời điểm chạy đà (T15).
    ///
    /// Vì sao tách sự kiện "nhả ngón" khỏi việc dựng ShotIntent: sai số thời điểm là tham số
    /// VÀO của <see cref="ShotMapper.Map"/>. Nếu component này tự dựng intent ngay lúc nhả tay
    /// thì nó buộc phải truyền timingError = 0, và toàn bộ cơ chế canh nhịp trở thành đồ trang trí.
    /// </summary>
    public sealed class TouchSwipeReceiver : MonoBehaviour
    {
        /// <summary>Ngón tay chạm xuống. Tham số: toạ độ pixel.</summary>
        public event Action<Vector2> OnAimBegin;

        /// <summary>Ngón tay đang kéo. Tham số: toạ độ pixel hiện tại.</summary>
        public event Action<Vector2> OnAimMove;

        /// <summary>Nhả ngón tay với một cú vuốt hợp lệ.</summary>
        public event Action<SwipeFeatures, Vector2> OnSwipeReleased;

        /// <summary>Nhả ngón tay nhưng vuốt quá ngắn / không hợp lệ — coi như huỷ, không mất lượt.</summary>
        public event Action OnSwipeCancelled;

        [Header("Cấu hình ánh xạ cử chỉ")]
        [SerializeField] private ShotMappingConfig mappingConfig;

        [Header("Độ nhạy & Giới hạn")]
        [Tooltip("Vuốt ngắn hơn ngần này (centimet vật lý) thì bỏ qua, coi như chạm nhầm.")]
        [SerializeField] private float minSwipeDistCm = 0.5f;

        private SwipeCollector collector;
        private bool isSwiping;
        private float swipeStartTime;

        public bool IsInputEnabled { get; set; } = true;
        public bool IsSwiping => isSwiping;

        /// <summary>Giây đã trôi kể từ lúc ngón tay chạm xuống. 0 khi không vuốt.</summary>
        public float SwipeElapsed => isSwiping ? Time.time - swipeStartTime : 0f;

        public ShotMappingConfig Config
        {
            get => mappingConfig != null ? mappingConfig : (mappingConfig = ShotMappingConfig.CreateDefault());
            set => mappingConfig = value;
        }

        private void Awake()
        {
            collector = new SwipeCollector(256);
            if (mappingConfig == null) mappingConfig = ShotMappingConfig.CreateDefault();
        }

        private void OnDestroy()
        {
            collector?.Dispose();
            collector = null;
        }

        /// <summary>Huỷ cú vuốt đang dở (ví dụ hết giờ ngắm) mà không bắn sự kiện sút.</summary>
        public void CancelSwipe()
        {
            isSwiping = false;
        }

        private void Update()
        {
            if (!IsInputEnabled)
            {
                isSwiping = false;
                return;
            }

            // Ưu tiên API cảm ứng thật: nó cho biết có mấy ngón đang chạm, nhờ đó cử chỉ
            // ba ngón để bật HUD hiệu năng không bị hiểu nhầm thành một cú sút.
            int touchCount = Input.touchCount;
            if (touchCount > 0)
            {
                if (touchCount > 1)
                {
                    if (isSwiping) { isSwiping = false; OnSwipeCancelled?.Invoke(); }
                    return;
                }

                Touch t = Input.GetTouch(0);
                Vector2 p = t.position;
                switch (t.phase)
                {
                    case TouchPhase.Began: Begin(p); break;
                    case TouchPhase.Moved:
                    case TouchPhase.Stationary: Move(p); break;
                    case TouchPhase.Ended: End(p); break;
                    case TouchPhase.Canceled:
                        if (isSwiping) { isSwiping = false; OnSwipeCancelled?.Invoke(); }
                        break;
                }
                return;
            }

            // Chuột — chỉ dùng trong Editor và bản desktop.
            if (Input.GetMouseButtonDown(0)) Begin(Input.mousePosition);
            else if (Input.GetMouseButton(0) && isSwiping) Move(Input.mousePosition);
            else if (Input.GetMouseButtonUp(0) && isSwiping) End(Input.mousePosition);
        }

        private void Begin(Vector2 screenPos)
        {
            collector.Begin(new float2(screenPos.x, screenPos.y), Time.time, PhysicalUnits.Dpi);
            isSwiping = true;
            swipeStartTime = Time.time;
            OnAimBegin?.Invoke(screenPos);
        }

        private void Move(Vector2 screenPos)
        {
            if (!isSwiping) return;
            collector.Move(new float2(screenPos.x, screenPos.y), Time.time);
            OnAimMove?.Invoke(screenPos);
        }

        private void End(Vector2 screenPos)
        {
            if (!isSwiping) return;
            isSwiping = false;

            SwipeResult result = collector.End(new float2(screenPos.x, screenPos.y), Time.time);
            if (result.valid && result.features.length >= minSwipeDistCm)
            {
                OnSwipeReleased?.Invoke(result.features, screenPos);
            }
            else
            {
                OnSwipeCancelled?.Invoke();
            }
        }

        /// <summary>
        /// Phân loại TẠM cú vuốt đang dở, để lớp hoạt ảnh chọn được clip sút trước khi người
        /// chơi nhả ngón. Trả về <c>false</c> khi chưa đủ dữ liệu để đoán tử tế — gọi bên
        /// nhận cứ giữ nguyên phán đoán trước đó.
        ///
        /// ĐÂY KHÔNG PHẢI Ý ĐỒ SÚT. Vector phóng vẫn do <see cref="BuildIntent"/> tính lúc
        /// nhả ngón, đúng luật Phase 7: hoạt ảnh nhận loại cú sút, không quyết định nó. Nếu
        /// người chơi đổi ý giữa chừng thì bóng đi theo bản chính thức; chỉ hoạt ảnh là đã
        /// cam kết — hệt như ngoài đời, cú vung chân đang giữa chừng thì không đổi kiểu được.
        /// </summary>
        public bool TryPeekShotType(out ShotType type)
        {
            type = ShotType.Instep;
            if (collector == null || !isSwiping) return false;
            if (!collector.TryPeek(out SwipeFeatures f)) return false;

            // Cùng ngưỡng mà End() dùng để loại chạm nhầm. Dưới ngưỡng thì hình dáng cử chỉ
            // chưa có nghĩa gì: 4 mm đầu tiên của MỌI cú vuốt đều trông như nhau.
            if (f.length < minSwipeDistCm) return false;

            // Dùng THẲNG bộ phân loại chính thức, không thêm luật riêng cho bản tạm. Đã thử
            // chặn nhánh lốp khi cử chỉ kéo dài hơn mọi cú lốp có thể (2026-08-28) và bỏ đi:
            // luật chơi bắt người chơi GIỮ ngón rồi nhả đúng lúc, nên một cú lốp thật hoàn
            // toàn có thể bị giữ nửa giây trước khi nhả — chặn theo thời lượng là cướp mất
            // hoạt ảnh lốp của đúng người vuốt lốp. Hai bộ luật song song còn là hai bộ luật
            // sẽ lệch nhau.
            ShotMappingConfig cfg = Config;
            type = ShotMapper.Classify(f, cfg, ShotMapper.SpeedT(f, cfg));
            return true;
        }

        /// <summary>Chiếu điểm chạm lên mặt phẳng khung thành (z = 11m).</summary>
        public static float3 AimPointFromScreen(Vector2 screenPos, Camera cam)
        {
            if (AimProjector.TryScreenToGoalPlane(screenPos, cam, 11.0f, out float3 aim)) return aim;
            return new float3(0f, 1.22f, 11.0f); // tia song song mặt phẳng: ngắm tâm khung
        }

        /// <summary>
        /// Dựng ý đồ cú sút từ cử chỉ + sai số thời điểm. timingError tính bằng GIÂY, có dấu.
        /// </summary>
        public ShotIntent BuildIntent(in SwipeFeatures f, Vector2 endScreenPos, Camera cam, float timingError, uint seed)
        {
            float3 aim = AimPointFromScreen(endScreenPos, cam);
            return ShotMapper.Map(in f, aim, Config, timingError, seed);
        }

        /// <summary>
        /// Bộ giải vận tốc phóng ban đầu: kết hợp ước lượng giải tích và MỘT bước hiệu chỉnh
        /// bằng <see cref="TrajectoryPredictor"/>, để bóng thật sự bay qua điểm người chơi ngắm
        /// dù có lực cản và Magnus.
        /// </summary>
        public static float3 SolveLaunchVelocity(in ShotIntent intent, in BallParams p)
        {
            float3 origin = new float3(0f, p.radius, 0f);
            float3 target = intent.aimPoint;

            // Cú lốp bóng (Panenka) giải riêng: nó cần đỉnh quỹ đạo cao, không phải đường căng.
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

            float speed = math.clamp(intent.speed, 18f, 36f);
            float vzEst = speed * 0.94f;
            float tEst = 11.0f / vzEst;

            // Bù trọng lực, có tính thêm phần rơi trội ra do bóng bị cản chậm dần.
            float gComp = p.gravity * (1.0f + 0.08f * (tEst / 0.4f));
            float vyEst = (target.y - origin.y + 0.5f * gComp * tEst * tEst) / tEst;

            // Bù độ lệch ngang do Magnus: F_m = 0.5 * rho * Cl * A * r * (omegaY * vz)
            float area = math.PI * p.radius * p.radius;
            float magnusAccX = (0.5f * p.airDensity * p.liftCoefficient * area * p.radius / p.mass) * (intent.spin.y * vzEst);
            float magnusOffsetX = 0.5f * magnusAccX * tEst * tEst;
            float vxEst = (target.x - magnusOffsetX) / tEst;

            float currentMagSq = vxEst * vxEst + vyEst * vyEst + vzEst * vzEst;
            float scale = currentMagSq > 0.01f ? speed / math.sqrt(currentMagSq) : 1f;
            float3 initVel = new float3(vxEst * scale, vyEst * scale, vzEst * scale);

            var testState = new BallState(origin, initVel, intent.spin);
            if (TrajectoryPredictor.FirstCrossing(in testState, in p, 11.0f, 1f / 120f, out float3 hitPoint, out float actualTime))
            {
                if (actualTime > 0.05f)
                {
                    initVel.x += (target.x - hitPoint.x) / actualTime;
                    initVel.y += (target.y - hitPoint.y) / actualTime;
                }
            }

            return initVel;
        }
    }
}
