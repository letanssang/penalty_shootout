// Sinh boi mo hinh duoc giao viec (9router), da qua ra soat tinh cua Claude 2026-08-26.
// CHUA CHAY TEST SONG trong Unity -> chua tick muc nghiem thu nao trong backlog.
using Unity.Mathematics;
using UnityEngine;

namespace Eleven.Shooter {
    /// <summary>
    /// Lớp tiện ích quy đổi toạ độ pixel màn hình sang đơn vị vật lý (centimeter).
    /// Thiết kế tách biệt: phần toán nhận tham số DPI độc lập để chạy được trong EditMode Test / Burst job,
    /// phần truy cập UnityEngine.Screen được bọc ngoài cùng cơ chế cache kích thước màn hình.
    /// </summary>
    public static class PhysicalUnits {
        /// <summary>
        /// DPI dự phòng tiêu chuẩn (trung bình giữa iPhone SE 326 ppi và iPad ~264 ppi).
        /// </summary>
        public const float DefaultDpi = 290f;

        /// <summary>
        /// Ngưỡng DPI tối thiểu hợp lệ của thiết bị di động. Dưới ngưỡng này coi như dữ liệu lỗi.
        /// </summary>
        public const float MinDpi = 100f;

        /// <summary>
        /// Ngưỡng DPI tối đa hợp lệ. Vượt quá ngưỡng này coi như dữ liệu bất thường.
        /// </summary>
        public const float MaxDpi = 700f;

        /// <summary>
        /// Số cm trên 1 inch tiêu chuẩn quốc tế.
        /// </summary>
        public const float CmPerInch = 2.54f;

        private static int _lastScreenWidth = -1;
        private static int _lastScreenHeight = -1;
        private static float _cachedDpi = DefaultDpi;

        #region Thuần toán (Testable / Job-safe)

        /// <summary>
        /// Chuẩn hoá giá trị DPI thô: khử NaN, Infinity, giá trị âm hoặc vượt ngoài khoảng [MinDpi, MaxDpi].
        /// </summary>
        public static float SanitizeDpi(float rawDpi) {
            if (float.IsNaN(rawDpi) || float.IsInfinity(rawDpi) || rawDpi < MinDpi || rawDpi > MaxDpi) {
                return DefaultDpi;
            }
            return rawDpi;
        }

        /// <summary>
        /// Tính hệ số nhân chuyển đổi từ pixel sang centimeter: k = 2.54 / dpi.
        /// </summary>
        public static float GetPixelsToCmFactor(float dpi) {
            return CmPerInch / SanitizeDpi(dpi);
        }

        /// <summary>
        /// Quy đổi khoảng cách 1D từ pixel sang cm theo DPI truyền vào.
        /// </summary>
        public static float ToCentimeters(float pixels, float dpi) {
            return pixels * GetPixelsToCmFactor(dpi);
        }

        /// <summary>
        /// Quy đổi toạ độ/vector 2D từ pixel sang cm theo DPI truyền vào.
        /// </summary>
        public static float2 ToCentimeters(float2 pixels, float dpi) {
            return pixels * GetPixelsToCmFactor(dpi);
        }

        /// <summary>
        /// Quy đổi khoảng cách 1D từ cm sang pixel theo DPI truyền vào.
        /// </summary>
        public static float ToPixels(float centimeters, float dpi) {
            return centimeters * (SanitizeDpi(dpi) / CmPerInch);
        }

        /// <summary>
        /// Quy đổi toạ độ/vector 2D từ cm sang pixel theo DPI truyền vào.
        /// </summary>
        public static float2 ToPixels(float2 centimeters, float dpi) {
            return centimeters * (SanitizeDpi(dpi) / CmPerInch);
        }

        /// <summary>
        /// Lật trục y của toạ độ chạm về quy ước GỐC DƯỚI-TRÁI.
        /// </summary>
        /// <remarks>
        /// Input Manager cũ trả gốc dưới-trái, Input System mới trả gốc TRÊN-TRÁI cho vị trí
        /// pointer. Trộn hai hệ mà không quy về một mối là nguồn bug "vuốt ngược" kinh điển:
        /// <c>verticalRatio</c> đảo dấu, cú chip bị nhận nhầm thành cú sục và ngược lại.
        /// Lớp gọi PHẢI quy toạ độ về một gốc duy nhất TRƯỚC khi bơm vào
        /// <see cref="SwipeCollector"/> — bản thân collector không đoán được nó đang nhận hệ nào.
        /// Hàm này thuần, nhận chiều cao màn hình làm tham số để test được.
        /// </remarks>
        /// <param name="pixels">Toạ độ pixel theo gốc trên-trái.</param>
        /// <param name="screenHeightPixels">Chiều cao màn hình tính bằng pixel.</param>
        public static float2 FlipYToBottomLeft(float2 pixels, float screenHeightPixels) {
            return new float2(pixels.x, screenHeightPixels - pixels.y);
        }

        #endregion

        #region Vỏ bọc UnityEngine.Screen (Runtime cache)

        /// <summary>
        /// DPI màn hình hiện tại sau khi khử nhiễu, được cache và chỉ cập nhật lại khi kích thước màn hình thay đổi.
        /// </summary>
        public static float Dpi {
            get {
                int currentWidth = Screen.width;
                int currentHeight = Screen.height;

                if (currentWidth != _lastScreenWidth || currentHeight != _lastScreenHeight || _lastScreenWidth == -1) {
                    _lastScreenWidth = currentWidth;
                    _lastScreenHeight = currentHeight;
                    _cachedDpi = SanitizeDpi(Screen.dpi);
                }

                return _cachedDpi;
            }
        }

        /// <summary>
        /// Quy đổi toạ độ pixel sang cm sử dụng DPI màn hình runtime hiện tại.
        /// </summary>
        public static float2 ToCentimeters(float2 pixels) {
            return ToCentimeters(pixels, Dpi);
        }

        /// <summary>
        /// Quy đổi độ dài pixel sang cm sử dụng DPI màn hình runtime hiện tại.
        /// </summary>
        public static float ToCentimeters(float pixels) {
            return ToCentimeters(pixels, Dpi);
        }

        /// <summary>
        /// Quy đổi toạ độ cm sang pixel sử dụng DPI màn hình runtime hiện tại.
        /// </summary>
        public static float2 ToPixels(float2 centimeters) {
            return ToPixels(centimeters, Dpi);
        }

        /// <summary>
        /// Quy đổi độ dài cm sang pixel sử dụng DPI màn hình runtime hiện tại.
        /// </summary>
        public static float ToPixels(float centimeters) {
            return ToPixels(centimeters, Dpi);
        }

        #endregion
    }
}
