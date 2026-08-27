using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Eleven.Core;
using Eleven.Presentation;
using Eleven.Presentation.Automation;
using Is = NUnit.Framework.Is;

namespace Eleven.Tests.EditMode
{
    [TestFixture]
    public class RegressionBenchmarkTests
    {
        [Test]
        public void Suite20Replay_DayDu20KichBan_KhongTrungSeed()
        {
            var suite = BenchmarkSuite.GenerateStandard20Replays();

            Assert.AreEqual(BenchmarkSuite.StandardReplayCount, suite.Count,
                "Suite chuẩn phải có đủ đúng 20 kịch bản replay.");

            var seedSet = new HashSet<uint>();
            for (int i = 0; i < suite.Count; i++)
            {
                var kick = suite[i];
                Assert.IsTrue(seedSet.Add(kick.seed),
                    $"Kịch bản thứ {i} bị trùng seed {kick.seed} với kịch bản trước.");
                Assert.Greater(kick.intent.speed, 10.0f, "Vận tốc sút của bóng không hợp lệ.");
            }
        }

        [Test]
        public void RegressionReport_XuatVaDocCsv_KhopChinhXac()
        {
            var original = new RegressionReport
            {
                gitCommitHash = "4c40848",
                deviceModel = "Pixel_7",
                operatingSystem = "Android 14",
                qualityTier = QualityTier.B,
                timestamp = "2026-08-27 15:00:00",
                totalKicksRun = 20,
                totalFramesMeasured = 600,
                p50TotalMs = 15.2f,
                p95TotalMs = 17.4f,
                p99TotalMs = 18.1f,
                p95CpuMainMs = 6.2f,
                p95CpuRenderMs = 4.1f,
                p95GpuMs = 5.8f,
                averageDrawCalls = 45,
                maxDrawCalls = 60,
                averageTriangles = 25000,
                maxTriangles = 35000,
                gcAllocBytesPerFrame = 0,
                peakTextureMemoryMB = 180.5f,
                startThermalState = 0,
                endThermalState = 1,
                grassGpuMs = 1.8f,
                skinGpuMs = 0.4f,
                netCpuMs = 0.3f,
                postProcessGpuMs = 1.1f,
                crowdGpuMs = 0.6f,
                lightingGpuMs = 0.7f,
                motionBlurGpuMs = 0.2f,
                stadiumGpuMs = 0.4f
            };

            string csv = original.ToCsv();
            Assert.IsNotEmpty(csv);
            Assert.IsTrue(csv.Contains("GitCommit,4c40848"));
            Assert.IsTrue(csv.Contains("DeviceModel,Pixel_7"));

            var parsed = RegressionReport.FromCsv(csv);
            Assert.AreEqual(original.gitCommitHash, parsed.gitCommitHash);
            Assert.AreEqual(original.deviceModel, parsed.deviceModel);
            Assert.AreEqual(original.qualityTier, parsed.qualityTier);
            Assert.AreEqual(original.p95TotalMs, parsed.p95TotalMs, 1e-2f);
            Assert.AreEqual(original.averageDrawCalls, parsed.averageDrawCalls);
            Assert.AreEqual(original.peakTextureMemoryMB, parsed.peakTextureMemoryMB, 1e-2f);
        }

        [Test]
        public void CanhBaoHoiQuy_P95TangQua5PhanTram_PhatHienChinhXac()
        {
            var baseline = new RegressionReport
            {
                gitCommitHash = "baseline_commit",
                p95TotalMs = 16.0f
            };

            // Tăng 7.5% (> 5% max allowed)
            var degraded = new RegressionReport
            {
                gitCommitHash = "degraded_commit",
                p95TotalMs = 17.2f
            };

            bool pass = RegressionReport.CompareWithBaseline(baseline, degraded,
                RegressionReport.MaxP95RegressionThreshold, out string warning);

            Assert.IsFalse(pass, "Hệ thống phải phát hiện và cảnh báo khi p95 tăng > 5%.");
            Assert.IsTrue(warning.Contains("[HỒI QUY HIỆU NĂNG]"));
            Assert.IsTrue(warning.Contains("7.5%"));
        }

        [Test]
        public void P95KhongDoiHoacTotHon_PassBenchmark()
        {
            var baseline = new RegressionReport
            {
                gitCommitHash = "baseline_commit",
                p95TotalMs = 16.0f
            };

            // Tăng 2% (nhỏ hơn 5%) hoặc giảm
            var stable = new RegressionReport
            {
                gitCommitHash = "stable_commit",
                p95TotalMs = 16.3f
            };

            bool pass = RegressionReport.CompareWithBaseline(baseline, stable,
                RegressionReport.MaxP95RegressionThreshold, out string message);

            Assert.IsTrue(pass);
            Assert.IsTrue(message.Contains("[OK]"));
        }

        [Test]
        public void BenchmarkRunner_Chay20Replay_SinhBaoCaoDayDu8TruCot()
        {
            var report = BenchmarkRunner.RunStandardSuite("test_commit");

            Assert.AreEqual(20, report.totalKicksRun);
            Assert.Greater(report.totalFramesMeasured, 100);
            Assert.Greater(report.p95TotalMs, 0f);
            Assert.Greater(report.grassGpuMs, 0f);
            Assert.Greater(report.skinGpuMs, 0f);
            Assert.Greater(report.netCpuMs, 0f);
            Assert.Greater(report.postProcessGpuMs, 0f);
        }

        [Test]
        public void BenchmarkRunner_KhongCapPhatGC()
        {
            var singleKickList = new List<ReplayKickData>
            {
                BenchmarkSuite.GenerateStandard20Replays()[0]
            };

            TestDelegate action = () =>
            {
                for (int i = 0; i < 5; i++)
                {
                    BenchmarkRunner.RunSuite(singleKickList, "test_gc");
                }
            };

            // Warm-up JIT
            action();

            Assert.That(action, Is.Not.AllocatingGCMemory());
        }
    }
}
