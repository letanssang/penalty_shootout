using Unity.Mathematics;
using UnityEngine;

namespace Eleven.Shooter {
    /// <summary>
    /// CHỖ DUY NHẤT trong luồng sút được phép biết camera đang ở đâu. Nhiệm vụ: đổi một
    /// điểm trên màn hình thành điểm ngắm trong không gian thế giới, trên mặt phẳng khung thành.
    ///
    /// VÌ SAO TÁCH RA MỘT FILE RIÊNG (quyết định 2026-08-26, T26):
    /// Giai đoạn đầu camera đứng yên, nên phép chiếu này là một hằng số và ta hoàn toàn có
    /// thể nướng sẵn ma trận vào code cho nhanh. CỐ Ý KHÔNG LÀM THẾ. Camera được truyền vào
    /// như THAM SỐ và ma trận được đọc lại mỗi lần gọi, nên ngày camera bắt đầu lia theo bóng
    /// thì không có gì phải sửa — kể cả <see cref="ShotMapper"/>, vốn chỉ nhận điểm thế giới.
    /// Cái giá phải trả là vài phép nhân ma trận mỗi cú sút. Rẻ hơn nhiều so với đi truy
    /// một toạ độ ngắm sai khắp codebase sau này.
    ///
    /// Phần toán tách khỏi phần đọc engine để test được trong EditMode, nơi không có camera thật.
    /// </summary>
    public static class AimProjector {
        // Dưới ngưỡng này thì tia gần như song song mặt phẳng khung thành: giao điểm chạy ra
        // vô cực và mọi kết quả đều vô nghĩa. Không phải số điều chỉnh, là ngưỡng suy biến.
        const float MinDirectionZ = 1e-5f;

        /// <summary>
        /// Cắt tia với mặt phẳng z = <paramref name="planeZ"/>. Hàm thuần, không đụng engine.
        /// </summary>
        /// <param name="direction">Không cần chuẩn hoá.</param>
        /// <returns>
        /// false khi tia song song mặt phẳng, HOẶC khi mặt phẳng nằm phía SAU tia
        /// (t &lt; 0) — trường hợp đó nghĩa là người chơi bấm vào chỗ ngược hướng khung thành,
        /// trả về true với một điểm "sau lưng" sẽ là cái bẫy im lặng.
        /// </returns>
        public static bool TryRayToPlaneZ(float3 origin, float3 direction, float planeZ,
                                          out float3 hit) {
            hit = default;

            if (math.abs(direction.z) < MinDirectionZ) return false;

            float t = (planeZ - origin.z) / direction.z;
            if (t < 0f) return false;

            hit = origin + direction * t;
            return true;
        }

        /// <summary>
        /// Vỏ bọc engine: đổi điểm chạm trên màn hình (PIXEL, gốc dưới-trái — đúng quy ước
        /// <c>Camera.ScreenPointToRay</c>) thành điểm ngắm thế giới.
        ///
        /// LƯU Ý VỀ ĐƠN VỊ: <c>SwipeFeatures.end</c> đã ở CENTIMET, không dùng thẳng vào đây
        /// được. Hoặc giữ lại toạ độ pixel thô từ sự kiện chạm, hoặc đổi ngược bằng
        /// <c>PhysicalUnits.ToPixels(f.end, dpi)</c> với ĐÚNG cái dpi đã đưa cho
        /// <c>SwipeCollector.Begin</c> — phép quy đổi chỉ là một phép nhân nên đổi ngược khớp tuyệt đối.
        /// </summary>
        /// <param name="camera">Truyền vào, KHÔNG lấy từ Camera.main. Vừa để test được,
        /// vừa để chuyển sang nhiều camera sau này không phải sửa gì.</param>
        public static bool TryScreenToGoalPlane(Vector2 screenPixel, Camera camera, float planeZ,
                                                out float3 hit) {
            hit = default;
            if (camera == null) return false;

            Ray ray = camera.ScreenPointToRay(screenPixel);
            return TryRayToPlaneZ(ray.origin, ray.direction, planeZ, out hit);
        }
    }
}
