using UnityEngine;

namespace Eleven.Shooter {
    /// <summary>
    /// Toàn bộ số liệu ánh xạ cử chỉ → cú sút. <see cref="ShotMapper"/> KHÔNG được chứa
    /// một hằng số nào ngoài mấy epsilon chống chia 0 — mọi thứ đáng chỉnh đều ở đây,
    /// vì bạn sẽ chỉnh nó hàng chục lần trên máy thật.
    ///
    /// ĐƠN VỊ — đọc kỹ, đây là chỗ dễ sai nhất:
    /// • Đặc trưng cú vuốt đến từ <see cref="SwipeCollector"/> nên đã ở CENTIMET, không phải
    ///   pixel. Nhờ vậy cùng một cử chỉ vật lý cho cùng kết quả trên máy 264 và 460 ppi.
    /// • <see cref="lengthToSpeed"/> và <see cref="curvatureToSpin"/> có CẢ HAI TRỤC chuẩn hoá
    ///   0..1 — đầu vào chia cho maxSwipe*, đầu ra nhân với min/maxSpeed hoặc maxSpinRadPerSec.
    /// • <see cref="qualityToScatter"/> thì KHÁC: trục Y của nó là MÉT thật trên mặt phẳng
    ///   khung thành, không chuẩn hoá. Cố ý, để bạn đọc thẳng "lệch bao nhiêu mét" trong
    ///   Inspector mà không phải nhân nhẩm.
    /// </summary>
    [CreateAssetMenu(fileName = "ShotMappingConfig", menuName = "Eleven/Shot Mapping Config")]
    public sealed class ShotMappingConfig : ScriptableObject {
        [Header("Công suất — độ dài vuốt quyết định tốc độ")]
        [Tooltip("X: độ dài vuốt đã chuẩn hoá 0..1. Y: tốc độ chuẩn hoá 0..1.\n" +
                 "PHẢI đi qua (1,1) nếu muốn vuốt hết biên độ đạt đúng maxSpeed.")]
        public AnimationCurve lengthToSpeed = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Tooltip("m/s. Cú vuốt ngắn nhất vẫn ra được cú sút này.")]
        public float minSpeed = 12f;

        [Tooltip("m/s. Trần cứng — ShotMapper kẹp, không bao giờ vượt.\n" +
                 "T12 đo trong video eFootball: cú sút căng rời chân ở 28.9 ± 2.7 m/s.")]
        public float maxSpeed = 30f;

        [Tooltip("cm. Vuốt dài hơn mức này không mạnh thêm được nữa.\n" +
                 "8 cm ≈ nửa chiều cao màn hình điện thoại — vuốt bằng ngón cái vẫn với tới.")]
        public float maxSwipeLengthCm = 8f;

        [Header("Xoáy — độ cong vuốt quyết định xoáy ngang")]
        [Tooltip("X: |độ cong| đã chuẩn hoá 0..1. Y: độ lớn xoáy chuẩn hoá 0..1.")]
        public AnimationCurve curvatureToSpin = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Tooltip("rad/s. ~60 rad/s ≈ 9.5 vòng/giây, cỡ một cú đá má trong xoáy mạnh.")]
        public float maxSpinRadPerSec = 60f;

        [Tooltip("cm. Độ cong = độ lệch TRUNG BÌNH có dấu của đường vuốt so với dây cung.\n" +
                 "Đường vuốt cong parabol phình ra h cm cho độ cong ≈ 2h/3, nên 1.0 cm ở đây\n" +
                 "tương ứng cú vuốt phình ra khoảng 1.5 cm — đã là cong rất rõ mắt.")]
        public float maxSwipeCurvatureCm = 1f;

        [Header("Sai số thời điểm — quyết định tản mát")]
        [Tooltip("X: quality 0..1 (1 = bấm chuẩn). Y: BÁN KÍNH TẢN MÁT theo MÉT.\n" +
                 "Phải về 0 ở quality = 1, nếu không cú hoàn hảo vẫn lệch.")]
        public AnimationCurve qualityToScatter = AnimationCurve.EaseInOut(0f, 1.2f, 1f, 0f);

        [Tooltip("giây. Sai số thời điểm bằng hoặc lớn hơn mức này cho quality = 0.\n" +
                 "Đây là mức TỆ NHẤT, không phải cửa sổ bấm chuẩn — cửa sổ đó do T15 giữ.")]
        public float maxTimingErrorSeconds = 0.2f;

        [Header("Nhận dạng loại sút (không nút bấm, chỉ cử chỉ)")]
        [Tooltip("cm. Vuốt NGẮN hơn mức này mà vẫn nhanh thì tính là cú lốp.")]
        public float chipMaxLengthCm = 3f;

        [Tooltip("cm/s. Ngưỡng 'giật' — để cú vuốt ngắn mà CHẬM không bị nhầm thành lốp,\n" +
                 "nó phải ra cú sút nhẹ bình thường.")]
        public float chipMinPeakSpeedCmPerSec = 40f;

        [Tooltip("cm. Cong từ mức này trở lên là cố ý đá má trong.")]
        public float insideFootMinCurvatureCm = 0.25f;

        [Tooltip("cm. Cong DƯỚI mức này coi như thẳng tuyệt đối.\n" +
                 "Phải nhỏ hơn hẳn insideFootMinCurvatureCm để hai vùng không đụng nhau.")]
        public float knuckleMaxCurvatureCm = 0.05f;

        [Tooltip("0..1. So với SwipeFeatures.straightnessSmooth (đo trên đường ĐÃ LÀM MƯỢT,\n" +
                 "không phải straightness thô). Chặn cú vuốt ngoằn ngoèo mà tình cờ\n" +
                 "có độ cong tổng ≈ 0 vì hai bướu trái/phải triệt tiêu nhau.")]
        // 0.97 không phải số chọn đại. Đo trên cú vuốt 7.5 cm / 25 mẫu, straightnessSmooth:
        //     tay run 13 px (mức tệ nhất còn thực tế) -> 0.979   PHẢI CHẤP NHẬN
        //     cố ý vuốt chữ S biên độ 0.5 cm          -> 0.961   PHẢI TỪ CHỐI
        // nên ngưỡng hợp lệ nằm trong khoảng (0.961, 0.979]; 0.97 là giữa khoảng đó.
        // Giá trị cũ 0.985 lấy trên straightness THÔ và đó là lỗi: chỉ cần tay run 5 px thì
        // độ thẳng thô đã tụt còn 0.970, tức cú knuckle nằm ngoài tầm với của gần như mọi
        // người chơi — trong khi checklist T14 đòi cả 4 ShotType đều đạt tới được bằng cử chỉ.
        // Cửa sổ này hẹp; nếu sau này chỉnh, chỉnh kèm KnuckleReachabilityTests bên EditMode.
        public float knuckleMinStraightness = 0.97f;

        [Tooltip("0..1, so với tốc độ đã chuẩn hoá. Knuckle chỉ ra khi vuốt MẠNH —\n" +
                 "cú nhẹ mà thẳng vẫn là mu bàn chân.")]
        public float knuckleMinPower = 0.8f;

        /// <summary>
        /// Bản config mặc định dựng bằng code. Dùng cho test và cho việc so sánh
        /// "asset trong dự án đã bị chỉnh lệch bao nhiêu so với gốc".
        /// </summary>
        public static ShotMappingConfig CreateDefault() {
            return CreateInstance<ShotMappingConfig>();
        }

        /// <summary>
        /// Sửa lại các giá trị vô nghĩa ngay trong Inspector thay vì để chúng lặng lẽ
        /// bò vào gameplay. Đường cong rỗng bị dựng lại vì <c>AnimationCurve.Evaluate</c>
        /// trên đường cong 0 khoá trả 0 mà không báo lỗi gì.
        /// </summary>
        void OnValidate() {
            if (lengthToSpeed == null || lengthToSpeed.length == 0)
                lengthToSpeed = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
            if (curvatureToSpin == null || curvatureToSpin.length == 0)
                curvatureToSpin = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
            if (qualityToScatter == null || qualityToScatter.length == 0)
                qualityToScatter = AnimationCurve.EaseInOut(0f, 1.2f, 1f, 0f);

            minSpeed = Mathf.Max(0f, minSpeed);
            maxSpeed = Mathf.Max(minSpeed, maxSpeed);
            maxSwipeLengthCm = Mathf.Max(0.01f, maxSwipeLengthCm);
            maxSwipeCurvatureCm = Mathf.Max(0.001f, maxSwipeCurvatureCm);
            maxSpinRadPerSec = Mathf.Max(0f, maxSpinRadPerSec);
            maxTimingErrorSeconds = Mathf.Max(0.001f, maxTimingErrorSeconds);

            // Hai vùng knuckle / má trong không được chồng lên nhau, nếu không thứ tự
            // phân loại sẽ quyết định kết quả thay vì cử chỉ của người chơi.
            knuckleMaxCurvatureCm = Mathf.Clamp(knuckleMaxCurvatureCm, 0f, insideFootMinCurvatureCm);
            knuckleMinStraightness = Mathf.Clamp01(knuckleMinStraightness);
            knuckleMinPower = Mathf.Clamp01(knuckleMinPower);
        }
    }
}
