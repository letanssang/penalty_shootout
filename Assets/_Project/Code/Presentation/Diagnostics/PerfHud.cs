using System.Text;
using Unity.Profiling;
using UnityEngine;

namespace Eleven.Core.Diagnostics
{
    /// <summary>
    /// HUD đo hiệu năng trên máy thật. Bật bằng PerfHud.Visible = true.
    ///
    /// Quy ước GC: đường ĐO (sampling, ring buffer, percentile) không cấp phát gì mỗi khung.
    /// Chỉ phần CHỮ hiển thị được làm mới 4 lần/giây thay vì mỗi khung — đó là điểm cấp phát
    /// duy nhất của HUD.
    /// </summary>
    public static class PerfHud
    {
        public const int HistoryLength = 600; // p95 tính trên 600 frame gần nhất

        static readonly FrameStats[] history = new FrameStats[HistoryLength];
        static readonly float[] scratch = new float[HistoryLength];
        static int head;
        static int count;
        static bool capturing;

        static PerfHudSampler sampler;   // component giữ vòng lặp mỗi khung
        static PerfHudRenderer renderer; // component vẽ chữ

        /// <summary>Bật/tắt HUD vẽ lên màn hình. Việc đo vẫn chạy nền để Percentile() luôn có dữ liệu.</summary>
        public static bool Visible
        {
            get => renderer != null && renderer.IsShown;
            set
            {
                EnsureComponents();
                renderer.SetVisible(value);
            }
        }

        public static FrameStats Current { get; private set; }

        /// <summary>Persentil tổng frame time trên HistoryLength khung gần nhất. p ∈ (0, 1].</summary>
        public static FrameStats Percentile(float p)
        {
            int n = System.Math.Min(count, HistoryLength);
            if (n == 0) return default;

            for (int i = 0; i < n; i++)
                scratch[i] = history[i].totalMs;
            System.Array.Sort(scratch, 0, n); // sort mảng primitive — không cấp phát

            // nearest-rank: p95 của 600 mẫu là mẫu thứ 570
            int rank = Mathf.Clamp(Mathf.CeilToInt(p * n) - 1, 0, n - 1);
            float target = scratch[rank];

            int best = 0; float bestErr = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                float err = Mathf.Abs(history[i].totalMs - target);
                if (err < bestErr) { bestErr = err; best = i; }
            }
            return history[best];
        }

        /// <summary>Bắt đầu một phiên capture. Xoá lịch sử cũ.</summary>
        public static void BeginCapture(string label)
        {
            EnsureComponents();
            head = 0;
            count = 0;
            capturing = true;
            CaptureLabel = string.IsNullOrEmpty(label) ? "capture" : label;
        }

        public static string CaptureLabel { get; private set; } = "capture";

        /// <summary>Kết thúc capture, trả nội dung CSV (đồng thời ghi file vào persistentDataPath).</summary>
        public static string EndCapture()
        {
            capturing = false;
            return WriteCsv();
        }

        internal static void Record(in FrameStats stats)
        {
            Current = stats;
            if (!capturing)
            {
                // ngoài phiên capture vẫn giữ lịch sử trượt để Percentile() hoạt động
                history[head] = stats;
                head = (head + 1) % HistoryLength;
                if (count < HistoryLength) count++;
                return;
            }
            history[head] = stats;
            head = (head + 1) % HistoryLength;
            count++;
        }

        static string WriteCsv()
        {
            var sb = new StringBuilder(64 + count * 128);
            sb.Append("frame,total_ms,gpu_ms,cpu_main_ms,cpu_render_ms,draw_calls,set_pass_calls,");
            sb.Append("triangles,gc_alloc_bytes,texture_memory_bytes,battery_level,thermal_state\n");

            int n = System.Math.Min(count, HistoryLength);
            for (int i = 0; i < n; i++)
            {
                var s = history[i];
                sb.Append(i).Append(',')
                  .Append(s.totalMs.ToString("F3")).Append(',')
                  .Append(s.gpuMs.ToString("F3")).Append(',')
                  .Append(s.cpuMainMs.ToString("F3")).Append(',')
                  .Append(s.cpuRenderMs.ToString("F3")).Append(',')
                  .Append(s.drawCalls).Append(',')
                  .Append(s.setPassCalls).Append(',')
                  .Append(s.triangles).Append(',')
                  .Append(s.gcAllocBytes).Append(',')
                  .Append(s.textureMemoryBytes).Append(',')
                  .Append(s.batteryLevel.ToString("F2")).Append(',')
                  .Append(s.thermalState).Append('\n');
            }

            string csv = sb.ToString();
            string fileName = $"eleven_{CaptureLabel}_{System.DateTime.Now:yyyyMMdd_HHmmss}.csv";
            string path = System.IO.Path.Combine(Application.persistentDataPath, fileName);
            try
            {
                System.IO.File.WriteAllText(path, csv);
                Debug.Log($"[PerfHud] CSV đã ghi: {path}");
            }
            catch (System.IO.IOException e)
            {
                Debug.LogWarning($"[PerfHud] Ghi CSV thất bại ({e.Message}) — vẫn trả CSV qua return value.");
            }
            return csv;
        }

        internal static PerfHudSampler EnsureComponents()
        {
            if (sampler != null) return sampler;
            var go = new GameObject("Eleven.PerfHud");
            Object.DontDestroyOnLoad(go);
            sampler = go.AddComponent<PerfHudSampler>();
            renderer = go.AddComponent<PerfHudRenderer>();
            return sampler;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            // Đo ngầm ngay khi app chạy — HUD mặc định tắt, dữ liệu p95/capture luôn sẵn sàng.
            EnsureComponents();
        }
    }
}
