using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using Eleven.Core;
using Eleven.Core.Diagnostics;

namespace Eleven.Presentation.Automation
{
    /// <summary>
    /// Trình thực thi bài test ngâm và kiểm soát suy giảm nhiệt độ trong 20 phút (T34).
    /// </summary>
    public static class SoakTestRunner
    {
        public const float StandardSoakDurationSeconds = 1200f; // 20 phút
        public const float SampleIntervalSeconds = 10f; // Lấy mẫu mỗi 10s -> 120 mẫu
        public const string SoakReportFileName = "soak_test_report.csv";

        [Serializable]
        public struct SoakSample
        {
            public float timeSeconds;
            public float frameTimeMs;
            public float fps;
            public int thermalState;
            public float memoryMB;
        }

        [Serializable]
        public struct SoakTestResult
        {
            public string deviceModel;
            public QualityTier qualityTier;
            public float totalDurationSeconds;
            public bool isChargingDetected;
            public float minFps;
            public float averageFps;
            public float p95FrameTimeMs;
            public float startMemoryMB;
            public float endMemoryMB;
            public float memoryGrowthRatio;
            public bool passedFps;
            public bool passedMemory;
            public bool isSuccessful => passedFps && passedMemory && !isChargingDetected;

            public List<SoakSample> samples;

            public string ToCsv()
            {
                var sb = new StringBuilder(2048);
                sb.AppendLine("# SOAK TEST SUMMARY");
                sb.AppendLine($"Device,{deviceModel}");
                sb.AppendLine($"QualityTier,{qualityTier}");
                sb.AppendLine($"TotalDurationSeconds,{totalDurationSeconds.ToString("F1", CultureInfo.InvariantCulture)}");
                sb.AppendLine($"ChargingDetected,{isChargingDetected}");
                sb.AppendLine($"MinFps,{minFps.ToString("F1", CultureInfo.InvariantCulture)}");
                sb.AppendLine($"AverageFps,{averageFps.ToString("F1", CultureInfo.InvariantCulture)}");
                sb.AppendLine($"P95FrameTimeMs,{p95FrameTimeMs.ToString("F2", CultureInfo.InvariantCulture)}");
                sb.AppendLine($"StartMemoryMB,{startMemoryMB.ToString("F2", CultureInfo.InvariantCulture)}");
                sb.AppendLine($"EndMemoryMB,{endMemoryMB.ToString("F2", CultureInfo.InvariantCulture)}");
                sb.AppendLine($"MemoryGrowthPercent,{(memoryGrowthRatio * 100f).ToString("F2", CultureInfo.InvariantCulture)}%");
                sb.AppendLine($"PassedFps,{passedFps}");
                sb.AppendLine($"PassedMemory,{passedMemory}");
                sb.AppendLine($"OverallPassed,{isSuccessful}");
                sb.AppendLine();
                sb.AppendLine("# TIME SERIES SAMPLES (Every 10s)");
                sb.AppendLine("TimeSec,FrameTimeMs,FPS,ThermalState,MemoryMB");

                if (samples != null)
                {
                    for (int i = 0; i < samples.Count; i++)
                    {
                        var s = samples[i];
                        sb.AppendLine($"{s.timeSeconds:F0},{s.frameTimeMs.ToString("F2", CultureInfo.InvariantCulture)},{s.fps.ToString("F1", CultureInfo.InvariantCulture)},{s.thermalState},{s.memoryMB.ToString("F2", CultureInfo.InvariantCulture)}");
                    }
                }

                return sb.ToString();
            }
        }

        /// <summary>
        /// Đánh giá kết quả kiểm thử ngâm theo tiêu chuẩn của từng bậc thiết bị.
        /// </summary>
        public static SoakTestResult EvaluateSamples(List<SoakSample> samples, QualityTier tier, bool isCharging, string device = "")
        {
            var result = new SoakTestResult
            {
                deviceModel = string.IsNullOrEmpty(device) ? (SystemInfo.deviceModel ?? "Device") : device,
                qualityTier = tier,
                isChargingDetected = isCharging,
                samples = samples ?? new List<SoakSample>()
            };

            if (samples == null || samples.Count == 0)
            {
                return result;
            }

            result.totalDurationSeconds = samples[samples.Count - 1].timeSeconds;
            result.startMemoryMB = samples[0].memoryMB;
            result.endMemoryMB = samples[samples.Count - 1].memoryMB;

            float minFps = float.MaxValue;
            float sumFps = 0f;
            var frameTimes = new List<float>(samples.Count);

            for (int i = 0; i < samples.Count; i++)
            {
                var s = samples[i];
                if (s.fps < minFps) minFps = s.fps;
                sumFps += s.fps;
                frameTimes.Add(s.frameTimeMs);
            }

            result.minFps = minFps;
            result.averageFps = sumFps / samples.Count;

            frameTimes.Sort();
            int rank95 = Mathf.Clamp(Mathf.CeilToInt(0.95f * frameTimes.Count) - 1, 0, frameTimes.Count - 1);
            result.p95FrameTimeMs = frameTimes[rank95];

            // Tăng trưởng bộ nhớ: (end - start) / start
            result.memoryGrowthRatio = result.startMemoryMB > 0.001f
                ? (result.endMemoryMB - result.startMemoryMB) / result.startMemoryMB
                : 0f;

            // Kiểm tra FPS theo bậc:
            // Bậc A / B: không dưới 55 FPS trong 20 phút
            // Bậc C: không dưới 30 FPS
            if (tier == QualityTier.C)
            {
                result.passedFps = result.minFps >= 30.0f;
            }
            else
            {
                result.passedFps = result.minFps >= 55.0f;
            }

            // Kiểm tra rò rỉ bộ nhớ: chênh lệch dưới 5%
            result.passedMemory = result.memoryGrowthRatio <= 0.05f;

            return result;
        }

        public static string SaveSoakReport(in SoakTestResult result)
        {
            string csv = result.ToCsv();
            string path = Path.Combine(Application.persistentDataPath, SoakReportFileName);

            try
            {
                File.WriteAllText(path, csv);
                Debug.Log($"[SoakTestRunner] Đã xuất báo cáo test ngâm: {path}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SoakTestRunner] Lỗi ghi file báo cáo test ngâm: {ex.Message}");
            }

            return path;
        }
    }
}
