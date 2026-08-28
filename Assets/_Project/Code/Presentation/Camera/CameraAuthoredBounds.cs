using Unity.Mathematics;

namespace Eleven.Presentation
{
    /// <summary>
    /// Định nghĩa và kiểm soát ranh giới hình học của vùng sân đã được tác giả dựng sẵn.
    /// Đảm bảo không có bất kỳ góc quay camera nào lộ ra phần ngoài biên chưa được dựng.
    /// </summary>
    public static class CameraAuthoredBounds
    {
        // Ranh giới không gian AABB của vùng sân đã dựng:
        // Chiều ngang X: [-8m, +8m]  (khung thành rộng 7.32m, tâm tại x=0)
        // Chiều cao Y:   [0m, 6m]     (khung thành cao 2.44m, camera trên cao không vượt quá 6m)
        // Chiều sâu Z:   [-11.5m, 15m] (chấm penalty tại z=0, vạch vôi tại z=11, sau lưới tới z=15)
        //
        // Biên sau NỚI TỪ -5 XUỐNG -11.5 ngày 2026-08-28. Con số -5 cũ là ước lượng thận
        // trọng chứ không phải mép đồ hoạ thật: mặt cỏ (PitchGround, Plane scale 4.2 tại
        // z=6) trải từ z=-15 tới z=27, khán đài hai bên từ z=-9 tới z=17. Camera sau lưng
        // người sút phải lùi tới z≈-10.5 mới lọt CẢ NGƯỜI vào khung (xem CameraRig.PoseFor);
        // ở đó ống kính vẫn nhìn về +z nên mọi thứ trong khung đều là phần đã dựng, và -11.5
        // còn chừa 3.5m cỏ trước khi chạm mép mặt cỏ.
        public static readonly float3 MinBounds = new float3(-8.0f, 0.0f, -11.5f);
        public static readonly float3 MaxBounds = new float3(8.0f, 6.0f, 15.0f);

        // Giới hạn góc quay của ReplayOrbit (tính bằng độ):
        // Tuyệt đối không cho phép xoay tự do 360 độ nhằm tránh nhìn ra khoảng trống sau lưng khán đài.
        public const float MinOrbitYawDegrees = -60.0f;
        public const float MaxOrbitYawDegrees = 60.0f;
        public const float MinOrbitPitchDegrees = 5.0f;
        public const float MaxOrbitPitchDegrees = 45.0f;
        public const float MinOrbitDistance = 2.0f;
        public const float MaxOrbitDistance = 5.0f;

        /// <summary>
        /// Kiểm tra một toạ độ bất kỳ có nằm hoàn toàn trong hình hộp tác giả đã dựng hay không.
        /// </summary>
        public static bool IsWithin(in float3 pos)
        {
            return pos.x >= MinBounds.x && pos.x <= MaxBounds.x &&
                   pos.y >= MinBounds.y && pos.y <= MaxBounds.y &&
                   pos.z >= MinBounds.z && pos.z <= MaxBounds.z;
        }

        /// <summary>
        /// Trả về toạ độ camera mặc định được thiết kế sẵn cho từng loại góc quay.
        /// </summary>
        public static float3 GetDefaultShotPosition(CameraShot shot)
        {
            switch (shot)
            {
                case CameraShot.Broadcast:
                    return new float3(5.5f, 3.5f, 5.5f);

                case CameraShot.BehindShooter:
                    return new float3(0.0f, 2.9f, -10.5f);

                case CameraShot.KeeperPOV:
                    return new float3(0.0f, 1.6f, 10.8f);

                case CameraShot.LowAngle:
                    return new float3(-2.0f, 0.35f, -0.5f);

                case CameraShot.NetCam:
                    return new float3(0.0f, 1.5f, 12.2f);

                case CameraShot.ReplayOrbit:
                    return ComputeOrbitPosition(0.0f, 15.0f, 3.5f, new float3(0.0f, 1.22f, 11.0f));

                default:
                    return new float3(0.0f, 2.9f, -10.5f);
            }
        }

        /// <summary>
        /// Tính toán toạ độ cho góc máy ReplayOrbit xung quanh một tâm đích,
        /// tự động kẹp góc yaw/pitch/khoảng cách để không vượt ranh giới an toàn.
        /// </summary>
        public static float3 ComputeOrbitPosition(float rawYawDeg, float rawPitchDeg, float rawDistance, float3 targetCenter)
        {
            float yaw = math.clamp(rawYawDeg, MinOrbitYawDegrees, MaxOrbitYawDegrees);
            float pitch = math.clamp(rawPitchDeg, MinOrbitPitchDegrees, MaxOrbitPitchDegrees);
            float dist = math.clamp(rawDistance, MinOrbitDistance, MaxOrbitDistance);

            float yawRad = math.radians(yaw);
            float pitchRad = math.radians(pitch);

            // Vector từ tâm đích lùi về phía sau theo góc yaw & pitch
            float cosPitch = math.cos(pitchRad);
            float sinPitch = math.sin(pitchRad);
            float sinYaw = math.sin(yawRad);
            float cosYaw = math.cos(yawRad);

            // Khi yaw = 0, pitch = 0, camera nằm ở -Z so với target (hướng từ sân nhìn vào cầu môn)
            float3 offset = new float3(
                sinYaw * cosPitch * dist,
                sinPitch * dist,
                -cosYaw * cosPitch * dist
            );

            float3 pos = targetCenter + offset;

            // Kẹp thêm một lần nữa vào AABB nếu targetCenter nằm sát biên
            pos = math.clamp(pos, MinBounds, MaxBounds);
            return pos;
        }
    }
}
