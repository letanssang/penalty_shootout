using System;
using System.Globalization;
using System.Text;
using Eleven.Core;

namespace Eleven.Presentation.Automation
{
    /// <summary>
    /// Báo cáo hiệu năng hoàn chỉnh của một phiên chạy benchmark hồi quy (T33).
    /// Hỗ trợ xuất và đọc file CSV có nhúng git commit hash, tên thiết bị và nhiệt độ.
    /// </summary>
    [Serializable]
    public struct RegressionReport
    {
        public string gitCommitHash;
        public string deviceModel;
        public string operatingSystem;
        public QualityTier qualityTier;
        public string timestamp;

        public int totalKicksRun;
        public int totalFramesMeasured;

        // Chỉ số Frame Time (ms)
        public float p50TotalMs;
        public float p95TotalMs;
        public float p99TotalMs;
        public float p95CpuMainMs;
        public float p95CpuRenderMs;
        public float p95GpuMs;

        // Chỉ số Dựng hình & Bộ nhớ
        public int averageDrawCalls;
        public int maxDrawCalls;
        public int averageTriangles;
        public int maxTriangles;
        public long gcAllocBytesPerFrame;
        public float peakTextureMemoryMB;

        // Trạng thái nhiệt
        public int startThermalState;
        public int endThermalState;

        // Bảng phân rã 8 trụ cột hình ảnh (ms)
        public float grassGpuMs;
        public float skinGpuMs;
        public float netCpuMs;
        public float postProcessGpuMs;
        public float crowdGpuMs;
        public float lightingGpuMs;
        public float motionBlurGpuMs;
        public float stadiumGpuMs;

        public const float MaxP95RegressionThreshold = 0.05f; // Cảnh báo nếu p95 tăng > 5%

        public string ToCsv()
        {
            var sb = new StringBuilder(1024);
            sb.AppendLine("Key,Value");
            sb.AppendLine($"GitCommit,{gitCommitHash}");
            sb.AppendLine($"DeviceModel,{deviceModel}");
            sb.AppendLine($"OS,{operatingSystem}");
            sb.AppendLine($"QualityTier,{qualityTier}");
            sb.AppendLine($"Timestamp,{timestamp}");
            sb.AppendLine($"TotalKicks,{totalKicksRun}");
            sb.AppendLine($"TotalFrames,{totalFramesMeasured}");
            sb.AppendLine($"P50TotalMs,{p50TotalMs.ToString("F3", CultureInfo.InvariantCulture)}");
            sb.AppendLine($"P95TotalMs,{p95TotalMs.ToString("F3", CultureInfo.InvariantCulture)}");
            sb.AppendLine($"P99TotalMs,{p99TotalMs.ToString("F3", CultureInfo.InvariantCulture)}");
            sb.AppendLine($"P95CpuMainMs,{p95CpuMainMs.ToString("F3", CultureInfo.InvariantCulture)}");
            sb.AppendLine($"P95CpuRenderMs,{p95CpuRenderMs.ToString("F3", CultureInfo.InvariantCulture)}");
            sb.AppendLine($"P95GpuMs,{p95GpuMs.ToString("F3", CultureInfo.InvariantCulture)}");
            sb.AppendLine($"AvgDrawCalls,{averageDrawCalls}");
            sb.AppendLine($"MaxDrawCalls,{maxDrawCalls}");
            sb.AppendLine($"AvgTriangles,{averageTriangles}");
            sb.AppendLine($"MaxTriangles,{maxTriangles}");
            sb.AppendLine($"GcAllocBytesPerFrame,{gcAllocBytesPerFrame}");
            sb.AppendLine($"PeakTextureMemoryMB,{peakTextureMemoryMB.ToString("F2", CultureInfo.InvariantCulture)}");
            sb.AppendLine($"StartThermalState,{startThermalState}");
            sb.AppendLine($"EndThermalState,{endThermalState}");
            sb.AppendLine($"Pillar_GrassGpuMs,{grassGpuMs.ToString("F3", CultureInfo.InvariantCulture)}");
            sb.AppendLine($"Pillar_SkinGpuMs,{skinGpuMs.ToString("F3", CultureInfo.InvariantCulture)}");
            sb.AppendLine($"Pillar_NetCpuMs,{netCpuMs.ToString("F3", CultureInfo.InvariantCulture)}");
            sb.AppendLine($"Pillar_PostProcessGpuMs,{postProcessGpuMs.ToString("F3", CultureInfo.InvariantCulture)}");
            sb.AppendLine($"Pillar_CrowdGpuMs,{crowdGpuMs.ToString("F3", CultureInfo.InvariantCulture)}");
            sb.AppendLine($"Pillar_LightingGpuMs,{lightingGpuMs.ToString("F3", CultureInfo.InvariantCulture)}");
            sb.AppendLine($"Pillar_MotionBlurGpuMs,{motionBlurGpuMs.ToString("F3", CultureInfo.InvariantCulture)}");
            sb.AppendLine($"Pillar_StadiumGpuMs,{stadiumGpuMs.ToString("F3", CultureInfo.InvariantCulture)}");
            return sb.ToString();
        }

        public static RegressionReport FromCsv(string csv)
        {
            var report = new RegressionReport();
            if (string.IsNullOrEmpty(csv)) return report;

            string[] lines = csv.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                int commaIdx = line.IndexOf(',');
                if (commaIdx <= 0 || commaIdx >= line.Length - 1) continue;

                string key = line.Substring(0, commaIdx).Trim();
                string val = line.Substring(commaIdx + 1).Trim();

                switch (key)
                {
                    case "GitCommit": report.gitCommitHash = val; break;
                    case "DeviceModel": report.deviceModel = val; break;
                    case "OS": report.operatingSystem = val; break;
                    case "QualityTier":
                        if (Enum.TryParse(val, out QualityTier q)) report.qualityTier = q;
                        break;
                    case "Timestamp": report.timestamp = val; break;
                    case "TotalKicks": int.TryParse(val, out report.totalKicksRun); break;
                    case "TotalFrames": int.TryParse(val, out report.totalFramesMeasured); break;
                    case "P50TotalMs": float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out report.p50TotalMs); break;
                    case "P95TotalMs": float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out report.p95TotalMs); break;
                    case "P99TotalMs": float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out report.p99TotalMs); break;
                    case "P95CpuMainMs": float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out report.p95CpuMainMs); break;
                    case "P95CpuRenderMs": float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out report.p95CpuRenderMs); break;
                    case "P95GpuMs": float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out report.p95GpuMs); break;
                    case "AvgDrawCalls": int.TryParse(val, out report.averageDrawCalls); break;
                    case "MaxDrawCalls": int.TryParse(val, out report.maxDrawCalls); break;
                    case "AvgTriangles": int.TryParse(val, out report.averageTriangles); break;
                    case "MaxTriangles": int.TryParse(val, out report.maxTriangles); break;
                    case "GcAllocBytesPerFrame": long.TryParse(val, out report.gcAllocBytesPerFrame); break;
                    case "PeakTextureMemoryMB": float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out report.peakTextureMemoryMB); break;
                    case "StartThermalState": int.TryParse(val, out report.startThermalState); break;
                    case "EndThermalState": int.TryParse(val, out report.endThermalState); break;
                    case "Pillar_GrassGpuMs": float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out report.grassGpuMs); break;
                    case "Pillar_SkinGpuMs": float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out report.skinGpuMs); break;
                    case "Pillar_NetCpuMs": float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out report.netCpuMs); break;
                    case "Pillar_PostProcessGpuMs": float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out report.postProcessGpuMs); break;
                    case "Pillar_CrowdGpuMs": float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out report.crowdGpuMs); break;
                    case "Pillar_LightingGpuMs": float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out report.lightingGpuMs); break;
                    case "Pillar_MotionBlurGpuMs": float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out report.motionBlurGpuMs); break;
                    case "Pillar_StadiumGpuMs": float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out report.stadiumGpuMs); break;
                }
            }

            return report;
        }

        /// <summary>
        /// So sánh kết quả hiện tại với kết quả chuẩn Baseline.
        /// Trả về false và xuất cảnh báo nếu p95 bị suy giảm quá 5%.
        /// </summary>
        public static bool CompareWithBaseline(in RegressionReport baseline, in RegressionReport current,
                                               float maxAllowedRatio, out string message)
        {
            if (baseline.p95TotalMs <= 0.001f)
            {
                message = "Baseline hợp lệ chưa được thiết lập.";
                return true;
            }

            float ratio = (current.p95TotalMs - baseline.p95TotalMs) / baseline.p95TotalMs;
            float percent = ratio * 100f;

            if (ratio > maxAllowedRatio)
            {
                message = $"[HỒI QUY HIỆU NĂNG] p95 frame time tăng {percent:F1}% " +
                          $"({baseline.p95TotalMs:F2}ms -> {current.p95TotalMs:F2}ms), " +
                          $"vượt trần cho phép {maxAllowedRatio * 100f:F1}%! " +
                          $"Commit: {current.gitCommitHash} so với Baseline: {baseline.gitCommitHash}";
                return false;
            }

            message = $"[OK] Hiệu năng ổn định: p95 biến động {percent:+0.0;-0.0;0.0}% " +
                      $"({baseline.p95TotalMs:F2}ms -> {current.p95TotalMs:F2}ms).";
            return true;
        }
    }
}
