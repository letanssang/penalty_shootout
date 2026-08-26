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

        public event Action<BallState> OnSimStep;

        BallState currentState;
        BallState previousState;
        float accumulator;

        public void Launch(in BallState initial)
        {
            currentState = initial;
            previousState = initial;
            accumulator = 0f;
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
            IsLive = false;
            transform.position = position;
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
                accumulator -= SimDt;
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
