using System;
using Unity.Mathematics;
using UnityEngine;

namespace Eleven.Ball
{
    /// <summary>
    /// Cầu nối giữa solver thuần (T06) và thế giới Unity. Solver chạy ở đồng hồ riêng cố
    /// định 1/120s bằng bộ tích luỹ trong Update() — KHÔNG dùng FixedUpdate/Time.fixedDeltaTime,
    /// vì đổi Time.fixedDeltaTime toàn cục sẽ khiến mọi vật lý khác trong game chạy gấp đôi số
    /// bước. Transform hiển thị nội suy giữa hai trạng thái sim liền kề để mượt ở mọi tần số
    /// khung hình dù sim chạy 120Hz cố định.
    ///
    /// GHI CHÚ DIỄN GIẢI HỢP ĐỒNG (cần người xác nhận): T09 không nói BallParams truyền vào
    /// bằng cách nào — Launch chỉ nhận BallState. Thêm property Parameters (mặc định
    /// BallParams.Default) làm nơi cấu hình khí động cho instance này. Đây là thành viên MỚI,
    /// không đổi chữ ký của bất kỳ thành viên nào trong hợp đồng gốc.
    ///
    /// BỔ SUNG CHO BẢN CHƠI ĐƯỢC (cũng chỉ THÊM, không sửa chữ ký cũ):
    /// <see cref="ExternalAcceleration"/>, <see cref="FlightTime"/> và <see cref="Override"/>.
    /// Lý do: va chạm cột dọc, tay thủ môn và bất ổn định knuckle đều phải tác động vào bóng
    /// Ở TẦN SỐ SOLVER (120Hz) chứ không phải tần số khung hình — nếu không, cùng một cú sút
    /// sẽ ra kết quả khác nhau giữa máy 60fps và máy 30fps.
    /// </summary>
    public class BallDriver : MonoBehaviour
    {
        public const float SimDt = 1f / 120f;

        /// <summary>Trần số bước sim mỗi khung hình — chặn xoáy chết khi máy khựng một khung rất dài.</summary>
        const int MaxStepsPerFrame = 8;

        [SerializeField] BallParams parameters = BallParams.Default;
        public BallParams Parameters { get => parameters; set => parameters = value; }

        public BallState State => currentState;
        public bool IsLive { get; private set; }

        /// <summary>
        /// Gia tốc phụ (m/s²) do GAMEPLAY áp thêm ngoài mô hình khí động — hiện chỉ dùng cho
        /// bất ổn định knuckle (T15). Cố ý để ngoài <see cref="BallSolver"/>: đây không phải
        /// vật lý, và trộn vào solver sẽ làm hỏng tính thuần của nó.
        /// </summary>
        public float3 ExternalAcceleration { get; set; }

        /// <summary>Thời gian bay tích luỹ theo ĐỒNG HỒ SOLVER, tính từ lần Launch gần nhất.</summary>
        public float FlightTime { get; private set; }

        /// <summary>Trạng thái ngay TRƯỚC bước solver gần nhất — dùng để dò giao cắt trong bước.</summary>
        public BallState PreviousState => previousState;

        public event Action<BallState> OnSimStep;

        BallState currentState;
        BallState previousState;
        float accumulator;

        public void Launch(in BallState initial)
        {
            currentState = initial;
            previousState = initial;
            accumulator = 0f;
            FlightTime = 0f;
            ExternalAcceleration = float3.zero;
            IsLive = true;
        }

        public void Freeze()
        {
            IsLive = false;
        }

        public void ResetTo(float3 position)
        {
            currentState = new BallState(position, float3.zero, float3.zero);
            previousState = currentState;
            accumulator = 0f;
            FlightTime = 0f;
            ExternalAcceleration = float3.zero;
            IsLive = false;
            transform.position = position;
        }

        /// <summary>
        /// Ghi đè trạng thái bóng giữa chừng (bật cột, chạm tay thủ môn) mà KHÔNG chạm vào
        /// đồng hồ solver. Dùng Launch cho việc này sẽ xoá luôn thời gian bay và bộ tích luỹ,
        /// khiến mọi mốc thời gian sau va chạm bị lệch.
        /// </summary>
        public void Override(in BallState s)
        {
            currentState = s;
            previousState = s;
        }

        void Update()
        {
            if (!IsLive)
                return;

            accumulator += Time.deltaTime;

            int steps = 0;
            while (accumulator >= SimDt && steps < MaxStepsPerFrame)
            {
                previousState = currentState;
                currentState = BallSolver.Step(currentState, parameters, SimDt);

                // Gia tốc gameplay áp SAU solver: solver vẫn là nguồn sự thật duy nhất về
                // khí động, phần cộng thêm nhìn thấy rõ ràng là phần cộng thêm.
                float3 extra = ExternalAcceleration;
                if (math.lengthsq(extra) > 0f)
                {
                    currentState.velocity += extra * SimDt;
                    currentState.position += 0.5f * extra * SimDt * SimDt;
                }

                accumulator -= SimDt;
                FlightTime += SimDt;
                steps++;
                OnSimStep?.Invoke(currentState);
            }

            // Máy khựng nặng hơn trần bước: bỏ phần nợ còn lại thay vì dồn cho các khung sau
            // (dồn vô hạn là chính "xoáy chết" mà trần bước phải chặn).
            if (steps == MaxStepsPerFrame && accumulator >= SimDt)
                accumulator = 0f;

            float alpha = math.saturate(accumulator / SimDt);
            transform.position = math.lerp(previousState.position, currentState.position, alpha);
        }
    }
}
