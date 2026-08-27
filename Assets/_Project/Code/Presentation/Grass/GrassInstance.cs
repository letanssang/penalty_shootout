using Unity.Mathematics;

namespace Eleven.Presentation.Grass
{
    /// <summary>Một túm cỏ ở phía CPU. Chỉ sinh một lần lúc dựng sân, sau đó không đổi.</summary>
    public struct GrassInstance
    {
        /// <summary>Gốc túm cỏ, y = 0 (mặt sân).</summary>
        public float3 position;

        /// <summary>Góc xoay quanh trục Y (radian). Để hai túm cạnh nhau không trùng hình.</summary>
        public float yaw;

        /// <summary>Chiều cao túm (mét). Cỏ sân bóng cắt ngắn: 0.06 – 0.11 m.</summary>
        public float height;

        /// <summary>Độ ngả sẵn của túm, trong [0, 1]. Sân thật không có túm nào thẳng đứng tuyệt đối.</summary>
        public float bend;

        /// <summary>Pha gió riêng, trong [0, 1). Đây là thứ giữ cho cả sân không lượn cùng một nhịp.</summary>
        public float windPhase;

        /// <summary>Sắc độ xanh riêng, trong [0, 1]. 0 = ngả vàng, 1 = xanh đậm.</summary>
        public float tint01;
    }

    /// <summary>
    /// Bản đóng gói cho GPU, 32 byte. Bố cục PHẢI khớp từng trường với struct cùng tên trong
    /// Grass.shader — lệch một float là cả sân cỏ mọc sai chỗ.
    ///
    /// Vì sao 32 byte: 24.000 túm × 32 B = 768 KB. Đây là bộ đệm đọc mỗi đỉnh, nên nó phải nằm
    /// gọn trong băng thông chứ không chỉ trong bộ nhớ.
    /// </summary>
    public struct GrassInstanceGpu
    {
        /// <summary>xyz = vị trí gốc, w = yaw (radian).</summary>
        public float4 positionYaw;

        /// <summary>x = height, y = bend, z = windPhase, w = tint01.</summary>
        public float4 shape;

        public const int SizeInBytes = 32;
    }

    public static class GrassInstanceExtensions
    {
        public static GrassInstanceGpu ToGpu(in GrassInstance instance)
        {
            return new GrassInstanceGpu
            {
                positionYaw = new float4(instance.position, instance.yaw),
                shape = new float4(instance.height, instance.bend, instance.windPhase, instance.tint01)
            };
        }
    }
}
