using System;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

namespace Eleven.Shooter {
    /// <summary>
    /// Biến một cú vuốt đã phân tích thành ý đồ sút. Hàm THUẦN: không đọc Time, không đọc
    /// Camera, không đọc Screen, không có trạng thái tĩnh. Cùng đầu vào + cùng seed luôn
    /// cho cùng kết quả — đây là điều kiện để phát lại trận đấu và để thủ môn AI (T21)
    /// tính trước quỹ đạo.
    ///
    /// CỬ CHỈ NÀO RA KIỂU SÚT NÀO (thứ tự xét đúng như dưới, ưu tiên từ trên xuống):
    ///   1. Lốp        — vuốt NGẮN mà NHANH. Cú giật dứt khoát, người chơi mò ra ngay.
    ///   2. Má trong   — vuốt CONG rõ rệt. Cong sang phải → bóng xoáy sang phải.
    ///   3. Knuckle    — vuốt THẲNG ĐÉT và MẠNH. Khó cố tình làm, nên nó đắt như phần thưởng.
    ///   4. Mu bàn chân— còn lại. Đây là cú mặc định, người chơi mới luôn rơi vào đây.
    /// Không cử chỉ nào cần nút bấm, và ba cử chỉ đầu loại trừ nhau bằng ngưỡng trong config.
    ///
    /// VÌ SAO KHÔNG ĐỌC CAMERA: quyết định 2026-08-26 (T26) chốt camera đứng yên giai đoạn
    /// đầu, nhưng phép chiếu màn hình → điểm ngắm vẫn được tách hẳn ra
    /// <see cref="AimProjector"/>. ShotMapper nhận <c>aimPoint</c> đã ở không gian thế giới.
    /// Khi camera bắt đầu chạy, file này không phải sửa một dòng nào.
    /// </summary>
    public static class ShotMapper {
        // Chỉ dùng để chặn chia cho 0 khi config bị đặt về 0. Không phải hằng số điều chỉnh.
        const float Epsilon = 1e-6f;

        /// <summary>
        /// </summary>
        /// <param name="f">Đặc trưng cú vuốt, ĐƠN VỊ CENTIMET (do <see cref="SwipeCollector"/> quy đổi).</param>
        /// <param name="aimPoint">Điểm ngắm THÔ trong không gian thế giới, chưa cộng tản mát.
        /// Do <see cref="AimProjector"/> tính. Tản mát được cộng vào trong hàm này.</param>
        /// <param name="cfg">Không được null — thiếu config là lỗi lập trình, không phải
        /// trường hợp chạy bình thường, nên ném luôn thay vì âm thầm dùng số bịa.</param>
        /// <param name="timingError">Giây, CÓ DẤU. Lệch bao xa so với thời điểm chuẩn.
        /// Chỉ độ lớn được dùng — sớm hay muộn phạt như nhau.</param>
        /// <param name="seed">Hạt ngẫu nhiên của riêng cú sút này. Cùng seed = cùng tản mát.</param>
        public static ShotIntent Map(in SwipeFeatures f, float3 aimPoint,
                                     ShotMappingConfig cfg, float timingError, uint seed) {
            if (cfg == null) throw new ArgumentNullException(nameof(cfg));

            // ---- 1. Công suất ----------------------------------------------------------
            float speedT = SpeedT(f, cfg);
            float speed = math.clamp(math.lerp(cfg.minSpeed, cfg.maxSpeed, speedT),
                                     cfg.minSpeed, cfg.maxSpeed);

            // ---- 2. Loại sút -----------------------------------------------------------
            ShotType type = Classify(f, cfg, speedT);

            // ---- 3. Xoáy ---------------------------------------------------------------
            float3 spin = float3.zero;
            bool unstable = false;

            if (type == ShotType.Knuckle) {
                // Xoáy để NGUYÊN 0 và bật cờ riêng. Knuckle bay loạn vì bóng KHÔNG xoáy nên
                // dòng khí tách ra không đối xứng và điểm tách nhảy quanh — không phải vì nó
                // có xoáy lung tung. Gán xoáy ngẫu nhiên sẽ ra một cú vòng cung mượt, tức là
                // sai hẳn cái cảm giác cần có. T15 đọc cờ này và bơm nhiễu vào lực.
                unstable = true;
            } else {
                float curvT = cfg.maxSwipeCurvatureCm > Epsilon
                    ? math.saturate(math.abs(f.curvature) / cfg.maxSwipeCurvatureCm)
                    : 0f;
                float mag = math.saturate(cfg.curvatureToSpin.Evaluate(curvT)) * cfg.maxSpinRadPerSec;

                // math.sign(0) = 0, nên cú vuốt thẳng tuyệt đối cho xoáy đúng bằng 0 chứ
                // không phải một số hạt tiêu.
                // Trục Y: bóng bay +Z, Magnus ∝ cross(spin, v), cross(+Y, +Z) = +X.
                // Mà +X là bên PHẢI theo mắt người sút (khớp GoalGeometry.CellOf).
                // Nên vuốt cong phải (curvature > 0) → xoáy +Y → bóng cong sang phải. Khớp trực giác.
                spin = new float3(0f, math.sign(f.curvature) * mag, 0f);
            }

            // ---- 4. Chất lượng và tản mát ----------------------------------------------
            float quality = cfg.maxTimingErrorSeconds > Epsilon
                ? 1f - math.saturate(math.abs(timingError) / cfg.maxTimingErrorSeconds)
                : 1f;

            // Đường cong này trả thẳng ra MÉT (xem ghi chú đơn vị trong ShotMappingConfig).
            float scatterRadius = math.max(0f, cfg.qualityToScatter.Evaluate(quality));

            // Random.CreateFromIndex chứ không phải new Random(seed): nó băm seed nên
            // seed = 0 không làm hỏng bộ sinh, và các seed liên tiếp (1,2,3...) không cho
            // ra chuỗi số giống nhau đến đáng ngờ.
            var rng = Random.CreateFromIndex(seed);
            float angle = rng.NextFloat(0f, 2f * math.PI);
            // sqrt(u) cho phân bố ĐỀU trên hình tròn. Không có sqrt thì các cú sút dồn cục
            // vào tâm và bán kính tản mát trong Inspector không còn nghĩa gì.
            float radius = scatterRadius * math.sqrt(rng.NextFloat());

            // Lệch trong mặt phẳng khung thành (X ngang, Y dọc), giữ nguyên Z. Đúng chừng nào
            // khung thành còn vuông góc với trục Z — đang đúng, xem GoalGeometry.
            float3 scattered = aimPoint + new float3(math.cos(angle) * radius,
                                                     math.sin(angle) * radius,
                                                     0f);

            return new ShotIntent {
                aimPoint      = scattered,
                spin          = spin,
                speed         = speed,
                type          = type,
                quality       = quality,
                unstable      = unstable,
                scatterRadius = scatterRadius,
            };
        }

        /// <summary>
        /// Công suất chuẩn hoá [0,1] suy từ chiều dài cú vuốt. Tách ra khỏi <see cref="Map"/>
        /// vì <see cref="Classify"/> cần đúng con số này. Hai chỗ tự tính lại công thức là
        /// hai chỗ sẽ lệch nhau vào một ngày nào đó.
        ///
        /// Kẹp đầu ra đường cong: AnimationCurve có thể vọt ra ngoài [0,1] nếu tiếp tuyến bị
        /// kéo mạnh trong Inspector. Không kẹp thì lời hứa "không bao giờ vượt maxSpeed" sẽ
        /// vỡ vì một thao tác kéo chuột.
        /// </summary>
        public static float SpeedT(in SwipeFeatures f, ShotMappingConfig cfg) {
            if (cfg == null) throw new ArgumentNullException(nameof(cfg));
            float lengthT = cfg.maxSwipeLengthCm > Epsilon
                ? math.saturate(f.length / cfg.maxSwipeLengthCm)
                : 0f;
            return math.saturate(cfg.lengthToSpeed.Evaluate(lengthT));
        }

        /// <summary>
        /// Tách riêng để test được từng nhánh mà không phải dựng cả ShotIntent, và để
        /// công cụ chỉnh tay (T11-style) vẽ được bản đồ "cử chỉ nào rơi vào vùng nào".
        /// </summary>
        public static ShotType Classify(in SwipeFeatures f, ShotMappingConfig cfg, float speedT) {
            if (cfg == null) throw new ArgumentNullException(nameof(cfg));

            float absCurvature = math.abs(f.curvature);

            // Lốp xét TRƯỚC: nó là cử chỉ ngắn nên gần như luôn thoả điều kiện "thẳng",
            // xét sau thì mọi cú lốp sẽ bị nuốt mất thành cú mu bàn chân yếu.
            if (f.length <= cfg.chipMaxLengthCm && f.peakSpeed >= cfg.chipMinPeakSpeedCmPerSec)
                return ShotType.Chip;

            if (absCurvature >= cfg.insideFootMinCurvatureCm)
                return ShotType.InsideFoot;

            // Dùng straightnessSmooth chứ KHÔNG dùng straightness thô. Đây là hình dáng cử chỉ,
            // mà hình dáng thì phải đọc trên đường đã lọc nhiễu: đo được là với straightness thô,
            // chỉ cần tay run 5 px thì độ thẳng đã tụt xuống 0.970 và cú knuckle biến mất khỏi
            // tầm với của người chơi — trong khi họ vuốt thẳng thật.
            // straightness chặn cú vuốt hình chữ S: hai bướu ngược chiều triệt tiêu nhau nên
            // độ cong tổng ≈ 0, nhưng đó rõ ràng không phải cú vuốt thẳng.
            if (absCurvature <= cfg.knuckleMaxCurvatureCm
                && f.straightnessSmooth >= cfg.knuckleMinStraightness
                && speedT >= cfg.knuckleMinPower)
                return ShotType.Knuckle;

            return ShotType.Instep;
        }
    }
}
