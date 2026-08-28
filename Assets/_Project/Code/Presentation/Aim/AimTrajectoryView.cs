using Eleven.Ball;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Eleven.Presentation.Aim
{
    /// <summary>
    /// Vẽ đường bay DỰ KIẾN của quả sút, từ lúc người chơi nhả ngón tay tới lúc chân
    /// chạm bóng. Đây là phản hồi duy nhất cho biết cú vuốt vừa rồi được đọc thành cái gì —
    /// không có nó thì người chơi vuốt xong phải đợi hết đà chạy mới biết mình sút đi đâu,
    /// và mọi quả hỏng đều trông như lỗi của trò chơi chứ không phải của tay mình.
    ///
    /// KHÔNG mô phỏng song song: đường vẽ ra chạy đúng <see cref="BallSolver"/> mà cú sút
    /// thật sẽ chạy, với đúng vận tốc phóng mà <c>TouchSwipeReceiver.SolveLaunchVelocity</c>
    /// đã giải. Nếu đường vẽ và quả bóng đi khác nhau thì đó là lỗi, không phải "đường dự
    /// kiến chỉ mang tính minh hoạ".
    ///
    /// Không cấp phát mỗi khung: bộ đệm mẫu và mảng điểm dựng một lần trong <c>Awake</c>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AimTrajectoryView : MonoBehaviour
    {
        [Header("Tham chiếu")]
        [Tooltip("Đường bay dự kiến. Vẽ trong không gian thế giới.")]
        [SerializeField] private LineRenderer path;

        [Tooltip("Vòng tròn đánh dấu điểm bóng cắt mặt phẳng khung thành.")]
        [SerializeField] private LineRenderer impactRing;

        [Header("Lấy mẫu")]
        [Tooltip("Bước thời gian mô phỏng, giây. Nhỏ hơn thì đường mượt hơn nhưng tốn hơn.")]
        [SerializeField] private float sampleStep = 0.02f;

        [Tooltip("Mô phỏng tối đa bao nhiêu giây. Một quả 11m căng bay hết 11m trong ~0.45s.")]
        [SerializeField] private float maxSeconds = 1.40f;

        [Tooltip("Bán kính vòng tròn đánh dấu, mét.")]
        [SerializeField] private float impactRadius = 0.28f;

        const float GoalPlaneZ = 11.0f;
        const int MaxSamples = 96;
        const int RingSegments = 24;

        NativeArray<TrajectorySample> _buffer;
        Vector3[] _points;
        Vector3[] _ring;

        public bool IsVisible { get; private set; }

        private void Awake()
        {
            _buffer = new NativeArray<TrajectorySample>(MaxSamples, Allocator.Persistent);
            _points = new Vector3[MaxSamples];
            _ring = new Vector3[RingSegments + 1];

            if (path != null) path.useWorldSpace = true;
            if (impactRing != null)
            {
                impactRing.useWorldSpace = true;
                impactRing.positionCount = _ring.Length;
            }

            Hide();
        }

        private void OnDestroy()
        {
            if (_buffer.IsCreated) _buffer.Dispose();
        }

        /// <summary>
        /// Hiện đường bay cho một vận tốc phóng đã giải xong. Cắt đường ngay tại mặt phẳng
        /// khung thành: phần sau đó là bóng đã vào lưới hoặc ra ngoài, vẽ tiếp chỉ gây rối.
        /// </summary>
        public void Show(float3 origin, float3 velocity, float3 spin)
        {
            if (path == null || !_buffer.IsCreated) return;

            var start = new BallState(origin, velocity, spin);
            int count = TrajectoryPredictor.Predict(in start, BallParams.Default,
                                                    math.max(0.005f, sampleStep),
                                                    math.max(0.05f, maxSeconds),
                                                    _buffer);
            if (count < 2) return;

            // Cắt tại mặt phẳng khung thành, nội suy điểm cuối cho khớp đúng z = 11.
            int used = 1;
            _points[0] = (Vector3)_buffer[0].position;
            float3 crossing = _buffer[count - 1].position;

            for (int i = 1; i < count; i++)
            {
                float3 p = _buffer[i].position;
                float3 prev = _buffer[i - 1].position;

                if (p.z >= GoalPlaneZ)
                {
                    float denom = p.z - prev.z;
                    float frac = denom > 1e-5f ? math.saturate((GoalPlaneZ - prev.z) / denom) : 0f;
                    crossing = math.lerp(prev, p, frac);
                    _points[used++] = (Vector3)crossing;
                    break;
                }

                _points[used++] = (Vector3)p;
                crossing = p;

                if (used >= _points.Length) break;
            }

            path.positionCount = used;
            path.SetPositions(_points);
            path.enabled = true;

            BuildRing(crossing);
            IsVisible = true;
        }

        /// <summary>Tắt đường bay. Gọi đúng khoảnh khắc chân chạm bóng — từ đó bóng thật kể chuyện.</summary>
        public void Hide()
        {
            if (path != null) { path.positionCount = 0; path.enabled = false; }
            if (impactRing != null) impactRing.enabled = false;
            IsVisible = false;
        }

        /// <summary>Vòng tròn nằm trên mặt phẳng khung thành (pháp tuyến +z), nên nhìn từ sau lưng người sút là thấy tròn.</summary>
        private void BuildRing(float3 center)
        {
            if (impactRing == null) return;

            for (int i = 0; i <= RingSegments; i++)
            {
                float a = (math.PI * 2f) * i / RingSegments;
                _ring[i] = new Vector3(
                    center.x + math.cos(a) * impactRadius,
                    center.y + math.sin(a) * impactRadius,
                    center.z);
            }

            impactRing.positionCount = _ring.Length;
            impactRing.SetPositions(_ring);
            impactRing.enabled = true;
        }
    }
}
