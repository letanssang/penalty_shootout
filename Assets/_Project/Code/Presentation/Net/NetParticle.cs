using System;
using Unity.Mathematics;

namespace Eleven.Presentation.Net
{
    /// <summary>
    /// Cấu trúc dữ liệu một hạt nút lưới trong mô phỏng vật lý Verlet.
    /// Layout chuẩn byte blittable, an toàn truyền trực tiếp cho Unity Burst & Job System.
    /// </summary>
    [Serializable]
    public struct NetParticle
    {
        public float3 position;
        public float3 prevPosition;
        /// <summary>1 nếu hạt bị ghim cố định vào cột/xà/mặt đất, 0 nếu hạt tự do chuyển động.</summary>
        public byte pinned;

        public NetParticle(float3 pos, byte isPinned = 0)
        {
            position = pos;
            prevPosition = pos;
            pinned = isPinned;
        }
    }
}
