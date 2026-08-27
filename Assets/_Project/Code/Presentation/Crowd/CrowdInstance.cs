using Unity.Mathematics;

namespace Eleven.Presentation.Crowd
{
    /// <summary>
    /// Một chỗ ngồi trên khán đài. Là <c>struct</c> và nằm trong mảng liên tục — toàn bộ
    /// khán đài phải đi lên GPU trong đúng một buffer, một draw call.
    /// </summary>
    public struct CrowdInstance
    {
        /// <summary>Toạ độ chân của tấm bảng (billboard) trong không gian thế giới, mét.</summary>
        public float3 position;

        /// <summary>
        /// Độ lệch pha animation, [0,1). Sinh bằng băm chỉ số ghế + seed nên cố định giữa các
        /// lần chạy. Đây là thứ giữ cho khán đài không nhảy đồng loạt như robot.
        /// </summary>
        public float phase01;

        /// <summary>Hệ số cao thấp, quanh 1.0 — người lớn/trẻ con ngồi lẫn nhau.</summary>
        public float scale;

        /// <summary>Chỉ số màu áo trong <see cref="CrowdPalette"/>.</summary>
        public byte colorIndex;

        /// <summary>Nhịp animation riêng, [0.85, 1.15] — hai người cạnh nhau không cùng tốc độ.</summary>
        public float speedScale;
    }

    /// <summary>
    /// Bản sao đúng 32 byte của một instance để nạp thẳng vào <c>GraphicsBuffer</c>
    /// (<c>StructuredBuffer&lt;CrowdInstanceGpu&gt;</c> phía shader).
    /// Bố cục PHẢI khớp từng trường với struct cùng tên trong CrowdImpostor.shader.
    /// </summary>
    public struct CrowdInstanceGpu
    {
        /// <summary>xyz = vị trí chân, w = tỉ lệ.</summary>
        public float4 positionScale;

        /// <summary>x = pha [0,1), y = nhịp, zw = chưa dùng (giữ chỗ cho 16-byte alignment).</summary>
        public float4 phaseSpeed;

        /// <summary>rgb = màu áo tuyến tính, a = chưa dùng.</summary>
        public float4 tint;

        public const int SizeInBytes = 48;
    }

    public static class CrowdInstanceExtensions
    {
        public static CrowdInstanceGpu ToGpu(in CrowdInstance instance)
        {
            float3 tint = CrowdPalette.GetColor(instance.colorIndex);
            return new CrowdInstanceGpu
            {
                positionScale = new float4(instance.position, instance.scale),
                phaseSpeed = new float4(instance.phase01, instance.speedScale, 0f, 0f),
                tint = new float4(tint, 1f)
            };
        }
    }
}
