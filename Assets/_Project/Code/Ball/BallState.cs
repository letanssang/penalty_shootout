using System;
using Unity.Mathematics;

namespace Eleven.Ball
{
    /// <summary>
    /// Trạng thái đầy đủ của quả bóng tại một thời điểm. Struct thuần, không tham chiếu —
    /// sao chép được tự do, nằm gọn trong NativeArray, an toàn cho Burst.
    /// </summary>
    [Serializable]
    public struct BallState
    {
        /// <summary>Vị trí (m). +Z hướng từ chấm phạt đền tới khung thành, +Y lên trên, +X sang phải.</summary>
        public float3 position;

        /// <summary>Vận tốc (m/s).</summary>
        public float3 velocity;

        /// <summary>Vận tốc góc (rad/s), quy tắc bàn tay phải quanh chính trục của vector.</summary>
        public float3 spin;

        public BallState(float3 position, float3 velocity, float3 spin)
        {
            this.position = position;
            this.velocity = velocity;
            this.spin = spin;
        }
    }
}
