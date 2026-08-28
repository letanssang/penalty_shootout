// DebugHotkeys.cs — Phase 6 · Eleven Metres
// Phím tắt gỡ lỗi / đo hiệu năng dùng ngay trên máy thật mà không cần build riêng.
// Gắn component này vào bất kỳ GameObject nào tồn tại suốt vòng đời app
// (ví dụ: Bootstrap / PersistentRoot).

using UnityEngine;
using Eleven.Core;                          // DeviceTier, QualityTier
using Eleven.Core.Diagnostics;              // PerfHud
using Eleven.Presentation.Automation;       // BenchmarkRunner

namespace Eleven.Presentation.Diagnostics
{
    /// <summary>
    /// Component phím tắt gỡ lỗi / đo hiệu năng — Phase 6 demo trên máy thật.
    /// Keyboard (Editor / desktop) : F1 = TogglePerfHud · F2 = RunBenchmarkNow.
    /// Cảm ứng                     : 3 ngón đồng thời  = TogglePerfHud (nếu EnableTouchGesture).
    /// </summary>
    public sealed class DebugHotkeys : MonoBehaviour
    {
        // ──────────────────────────────────────────────────────────────────────
        // Trạng thái nội bộ — không cấp phát trong Update
        // ──────────────────────────────────────────────────────────────────────

        // Giữ trạng thái "3 ngón đang giữ" để chỉ kích hoạt MỘT LẦN
        // cho đến khi người dùng nhả hết ngón tay.
        private bool _touchGestureArmed = true;

        // Đường dẫn báo cáo benchmark gần nhất (rỗng nếu chưa chạy lần nào).
        private string _lastReportPath = string.Empty;

        // ──────────────────────────────────────────────────────────────────────
        // API công khai (hợp đồng bất biến)
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Bật / tắt nhận diện cử chỉ 3 ngón trên cảm ứng. Mặc định true.
        /// Tắt khi cần tránh xung đột với UI swipe của game trong một số scene.
        /// </summary>
        public bool EnableTouchGesture { get; set; } = true;

        /// <summary>Đường dẫn tuyệt đối của báo cáo benchmark cuối. Rỗng nếu chưa chạy.</summary>
        public string LastReportPath => _lastReportPath;

        // ──────────────────────────────────────────────────────────────────────
        // Vòng đời Unity
        // ──────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            // In thông tin máy ra logcat ngay khi khởi động để dễ tra cứu
            // mà không phải mở adb shell getprop. Không nối chuỗi runtime —
            // dòng này chỉ chạy đúng một lần nên cấp phát ở đây là chấp nhận được.
            string tierInfo = DeviceTier.CurrentProfile != null
                ? DeviceTier.Current.ToString()
                : DeviceTier.Current + " (profile chưa khởi tạo)";

            Debug.Log(
                "[DebugHotkeys] Máy: " + SystemInfo.deviceModel
                + " | Bậc: " + tierInfo
                + " | Độ phân giải: " + Screen.width + "x" + Screen.height
                + " | Refresh: " + Screen.currentResolution.refreshRateRatio.value.ToString("F1") + " Hz"
            );
        }

        private void Update()
        {
            // ── Bàn phím (nhất quán với Input.GetMouseButton dùng ở nơi khác) ──
            // Dùng GetKeyDown để chỉ kích hoạt đúng một frame thay vì liên tục.
            if (Input.GetKeyDown(KeyCode.F1))
                TogglePerfHud();

            if (Input.GetKeyDown(KeyCode.F2))
                RunBenchmarkNow();

            // ── Cử chỉ cảm ứng 3 ngón ──────────────────────────────────────
            // Chỉ kiểm tra khi tính năng được bật — tránh gọi Input.touchCount
            // không cần thiết trên desktop (chi phí thấp nhưng giữ sạch).
            if (EnableTouchGesture)
                HandleTouchGesture();
        }

        // ──────────────────────────────────────────────────────────────────────
        // Phương thức công khai
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Bật / tắt PerfHud overlay. Phép đo nền vẫn tiếp tục chạy.
        /// </summary>
        /// <summary>
        /// Đếm thật số lá cỏ và số khán giả được gửi xuống GPU, in ra log sau 3 giây.
        ///
        /// Vì sao cần: hai hệ thống này vẽ bằng Graphics.DrawMeshInstanced — không có
        /// Renderer nào trong Hierarchy để nhìn, không có gì báo lỗi khi chúng vẽ 0 instance.
        /// Trên máy thật chỉ có đúng một cách biết chúng có sống hay không: hỏi thẳng.
        /// </summary>
        private void LogRenderCounts()
        {
            var grass = FindFirstObjectByType<Eleven.Presentation.Grass.GrassFieldRenderer>();
            var crowd = FindFirstObjectByType<Eleven.Presentation.Crowd.CrowdRenderer>();

            int grassCount = grass != null ? grass.DrawnInstanceCount : -1;
            int crowdCount = crowd != null ? crowd.DrawnInstanceCount : -1;

            Debug.Log($"[DebugHotkeys] Cỏ vẽ {grassCount} lá | Khán giả vẽ {crowdCount} người " +
                      $"(-1 nghĩa là không có component trong scene)");
        }

        private void Start()
        {
            Invoke(nameof(LogRenderCounts), 3f);
        }

        public void TogglePerfHud()
        {
            // Đảo trạng thái hiển thị — PerfHud.Visible là property đơn giản, không cấp phát.
            PerfHud.Visible = !PerfHud.Visible;
        }

        /// <summary>
        /// Chạy suite benchmark chuẩn 20 kịch bản, lưu báo cáo CSV,
        /// gán LastReportPath và in đường dẫn ra Debug.Log.
        /// Bọc try/catch: lỗi chỉ in ra Debug.LogError, KHÔNG ném lên làm treo game.
        /// </summary>
        public void RunBenchmarkNow()
        {
            // try/catch bao trọn toàn bộ — RunStandardSuite có thể ném nếu
            // BenchmarkSuite chưa sẵn sàng (thiếu asset, scene sai, …).
            try
            {
                // Không truyền gitCommit — bản demo thực tế không có git CLI.
                RegressionReport report = BenchmarkRunner.RunStandardSuite();

                // SaveReport trả đường dẫn tuyệt đối; BenchmarkRunner đã log nội bộ.
                string path = BenchmarkRunner.SaveReport(in report);
                _lastReportPath = path;

                // Log thêm một dòng tóm tắt dễ nhìn trong adb logcat / Console.
                // Ghép chuỗi ở đây chấp nhận được vì chỉ xảy ra khi người dùng bấm F2.
                Debug.Log(
                    "[DebugHotkeys] Benchmark xong. Báo cáo: " + path
                    + " | p95=" + report.p95TotalMs.ToString("F2") + " ms"
                    + " | frames=" + report.totalFramesMeasured
                );
            }
            catch (System.Exception ex)
            {
                // Không để exception truyền lên — game vẫn chạy bình thường.
                Debug.LogError("[DebugHotkeys] RunBenchmarkNow thất bại: " + ex.Message + "\n" + ex.StackTrace);
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // Nội bộ
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Nhận diện cử chỉ 3 ngón đồng thời: kích hoạt TogglePerfHud đúng một lần
        /// cho đến khi người dùng nhả hết tay — tránh trigger liên tục.
        /// Không cấp phát: chỉ đọc int từ Input API.
        /// </summary>
        private void HandleTouchGesture()
        {
            int count = Input.touchCount;

            if (count >= 3)
            {
                // _touchGestureArmed = false sau lần đầu kích hoạt,
                // ngăn TogglePerfHud bị gọi nhiều lần trong khi ngón vẫn giữ.
                if (_touchGestureArmed)
                {
                    _touchGestureArmed = false;
                    TogglePerfHud();
                }
            }
            else
            {
                // Khi không còn đủ 3 ngón, "nạp lại" để lần chạm tới hoạt động.
                _touchGestureArmed = true;
            }
        }
    }
}
