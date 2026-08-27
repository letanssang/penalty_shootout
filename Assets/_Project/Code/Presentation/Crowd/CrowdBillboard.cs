using Unity.Mathematics;

namespace Eleven.Presentation.Crowd
{
    /// <summary>
    /// Phép quay của tấm bảng khán giả: CHỈ xoay quanh trục Y.
    ///
    /// Vì sao không dùng billboard cầu (xoay đủ ba trục theo camera): cách làm phổ biến là
    /// <c>right = normalize(cross(worldUp, viewDir))</c>. Khi camera đi ngang qua và
    /// <c>viewDir</c> tiến gần trục Y, tích có hướng suy biến rồi ĐỔI DẤU — cả khán đài lật
    /// ngược trong đúng một khung hình. Đó chính là lỗi mà ô nghiệm thu "không lật khi camera
    /// đi qua ngang" nói tới.
    ///
    /// Xoay quanh Y bằng <c>atan2</c> của độ lệch NGANG thì không có điểm suy biến nào trên
    /// mặt phẳng ngang: <c>cos</c>/<c>sin</c> liên tục qua cả vòng ±π. Chỉ còn đúng một điểm
    /// hỏng là camera nằm THẲNG trên đầu (độ lệch ngang bằng 0) — chỗ đó giữ nguyên góc cũ
    /// thay vì để atan2(0,0) trả về 0 và làm cả khán đài giật một cái.
    /// </summary>
    public static class CrowdBillboard
    {
        /// <summary>Dưới ngưỡng này (m²) coi như camera nằm thẳng trên đầu — giữ góc cũ.</summary>
        public const float DegenerateHorizontalDistanceSq = 1e-6f;

        /// <summary>
        /// Góc yaw (radian) để tấm bảng quay mặt về phía camera.
        /// <paramref name="previousYaw"/> là giá trị trả về khi camera thẳng đỉnh đầu.
        /// </summary>
        public static float YawRadians(in float3 instancePosition, in float3 cameraPosition, float previousYaw)
        {
            float dx = cameraPosition.x - instancePosition.x;
            float dz = cameraPosition.z - instancePosition.z;

            float horizontalSq = dx * dx + dz * dz;
            if (horizontalSq < DegenerateHorizontalDistanceSq)
            {
                return previousYaw;
            }

            return math.atan2(dx, dz);
        }

        /// <summary>Vector phải của tấm bảng ứng với yaw. Luôn nằm ngang, độ dài 1.</summary>
        public static float3 Right(float yawRadians)
        {
            math.sincos(yawRadians, out float s, out float c);
            return new float3(c, 0f, -s);
        }

        /// <summary>
        /// Pháp tuyến của tấm bảng (hướng mặt người). Luôn nằm ngang: khán giả không bao giờ
        /// ngả ra sau để nhìn camera trên cao — họ chỉ xoay người.
        /// </summary>
        public static float3 Normal(float yawRadians)
        {
            math.sincos(yawRadians, out float s, out float c);
            return new float3(s, 0f, c);
        }

        /// <summary>Vector lên. Cố định tuyệt đối — đây là thứ khoá cứng, không cho phép lật.</summary>
        public static float3 Up => new float3(0f, 1f, 0f);

        /// <summary>
        /// Tỉ lệ rộng/cao của tấm bảng. Một người đứng rộng chừng 0.55m trên 1.7m chiều cao;
        /// bảng vuông thì phí gần một nửa diện tích cho pixel trong suốt, mà pixel trong suốt
        /// vẫn tốn băng thông đúng bằng pixel đặc trên GPU di động.
        /// </summary>
        public const float QuadAspect = 0.34f;

        /// <summary>
        /// Đưa một đỉnh của mesh vuông đơn vị ra không gian thế giới.
        /// Quy ước mesh: <c>quadVertex.x ∈ [-0.5, 0.5]</c>, <c>quadVertex.y ∈ [0, 1]</c> —
        /// gốc toạ độ nằm ở CHÂN người, nên đổi <c>scale</c> là đổi chiều cao mà chân vẫn
        /// dính mặt ghế.
        /// <paramref name="scale"/> là chiều cao người tính bằng mét; bề ngang suy ra từ
        /// <see cref="QuadAspect"/>.
        ///
        /// Công thức này PHẢI khớp từng dòng với hàm cùng tên trong CrowdImpostor.shader —
        /// đây là thứ cho phép test EditMode kiểm được đúng phép biến đổi mà GPU sẽ chạy.
        /// </summary>
        public static float3 TransformVertex(in float3 instancePosition, float scale, float yawRadians,
                                             in float2 quadVertex)
        {
            float3 right = Right(yawRadians);
            return instancePosition
                   + right * (quadVertex.x * scale * QuadAspect)
                   + Up * (quadVertex.y * scale);
        }
    }
}
