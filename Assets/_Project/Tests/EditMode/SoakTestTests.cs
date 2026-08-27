using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Eleven.Core;
using Eleven.Presentation.Automation;
using Is = NUnit.Framework.Is;

namespace Eleven.Tests.EditMode
{
    [TestFixture]
    public class SoakTestTests
    {
        [Test]
        public void SoakTest_BacB_DatTren55Fps_VaBoNhoDuoi5PhanTram_Pass()
        {
            var samples = new List<SoakTestRunner.SoakSample>();

            // Sinh 120 mẫu (20 phút = 1200 giây, mỗi 10s lấy 1 mẫu)
            for (int i = 0; i < 120; i++)
            {
                samples.Add(new SoakTestRunner.SoakSample
                {
                    timeSeconds = i * 10f,
                    frameTimeMs = 16.8f,
                    fps = 59.5f,
                    thermalState = (i < 60) ? 0 : 1,
                    memoryMB = 180.0f + (i * 0.02f) // Tăng từ 180MB lên 182.4MB (+1.3% < 5%)
                });
            }

            var result = SoakTestRunner.EvaluateSamples(samples, QualityTier.B, isCharging: false, "Pixel_7");

            Assert.IsTrue(result.passedFps, "Bậc B phải duy trì FPS >= 55 trong suốt 20 phút.");
            Assert.IsTrue(result.passedMemory, "Mức tăng trưởng bộ nhớ phải < 5%.");
            Assert.IsFalse(result.isChargingDetected, "Không được cắm sạc khi test ngâm.");
            Assert.IsTrue(result.isSuccessful, "Bài test ngâm 20 phút Bậc B phải đạt toàn diện.");
            Assert.GreaterOrEqual(result.minFps, 55.0f);
        }

        [Test]
        public void SoakTest_BacC_DatTren30Fps_Pass()
        {
            var samples = new List<SoakTestRunner.SoakSample>();

            for (int i = 0; i < 120; i++)
            {
                samples.Add(new SoakTestRunner.SoakSample
                {
                    timeSeconds = i * 10f,
                    frameTimeMs = 31.5f,
                    fps = 31.8f,
                    thermalState = 0,
                    memoryMB = 120.0f + (i * 0.01f) // +1% < 5%
                });
            }

            var result = SoakTestRunner.EvaluateSamples(samples, QualityTier.C, isCharging: false, "LowEndPhone");

            Assert.IsTrue(result.passedFps, "Bậc C phải duy trì FPS >= 30.");
            Assert.IsTrue(result.isSuccessful);
        }

        [Test]
        public void SoakTest_PhatHienMayDangCamSac_BaoLoi()
        {
            var samples = new List<SoakTestRunner.SoakSample>
            {
                new SoakTestRunner.SoakSample { timeSeconds = 0f, fps = 60f, memoryMB = 180f },
                new SoakTestRunner.SoakSample { timeSeconds = 1200f, fps = 60f, memoryMB = 181f }
            };

            // Test với cờ isCharging = true
            var result = SoakTestRunner.EvaluateSamples(samples, QualityTier.B, isCharging: true, "Pixel_7");

            Assert.IsTrue(result.isChargingDetected, "Hệ thống phải phát hiện máy đang cắm sạc.");
            Assert.IsFalse(result.isSuccessful, "Test ngâm bắt buộc KHÔNG ĐƯỢC CẮM SẠC (sạc làm sai lệch nhiệt).");
        }

        [Test]
        public void SoakTest_PhatHienRoRiBoNhoTren5PhanTram_BaoLoi()
        {
            var samples = new List<SoakTestRunner.SoakSample>
            {
                new SoakTestRunner.SoakSample { timeSeconds = 0f, fps = 60f, memoryMB = 100f },
                new SoakTestRunner.SoakSample { timeSeconds = 1200f, fps = 60f, memoryMB = 110f } // Tăng 10% > 5%
            };

            var result = SoakTestRunner.EvaluateSamples(samples, QualityTier.B, isCharging: false);

            Assert.IsFalse(result.passedMemory, "Tăng trưởng bộ nhớ 10% phải bị đánh rớt.");
            Assert.IsFalse(result.isSuccessful);
            Assert.Greater(result.memoryGrowthRatio, 0.05f);
        }

        [Test]
        public void SoakTest_PhatHienTutFpsDuoiNguong_BaoLoi()
        {
            var samples = new List<SoakTestRunner.SoakSample>
            {
                new SoakTestRunner.SoakSample { timeSeconds = 0f, fps = 60f, memoryMB = 180f },
                new SoakTestRunner.SoakSample { timeSeconds = 600f, fps = 48f, memoryMB = 181f } // Tụt xuống 48fps (< 55fps ở Tier B)
            };

            var result = SoakTestRunner.EvaluateSamples(samples, QualityTier.B, isCharging: false);

            Assert.IsFalse(result.passedFps, "Bậc B tụt xuống 48 FPS phải bị đánh rớt.");
            Assert.IsFalse(result.isSuccessful);
        }

        [Test]
        public void SoakReport_XuatCsv_DungDinhDangTimeSeries()
        {
            var samples = new List<SoakTestRunner.SoakSample>
            {
                new SoakTestRunner.SoakSample { timeSeconds = 0f, frameTimeMs = 16.6f, fps = 60.0f, thermalState = 0, memoryMB = 180.5f },
                new SoakTestRunner.SoakSample { timeSeconds = 10f, frameTimeMs = 16.7f, fps = 59.8f, thermalState = 0, memoryMB = 180.6f }
            };

            var result = SoakTestRunner.EvaluateSamples(samples, QualityTier.B, isCharging: false, "Pixel_7");
            string csv = result.ToCsv();

            Assert.IsNotEmpty(csv);
            Assert.IsTrue(csv.Contains("# SOAK TEST SUMMARY"));
            Assert.IsTrue(csv.Contains("# TIME SERIES SAMPLES"));
            Assert.IsTrue(csv.Contains("TimeSec,FrameTimeMs,FPS,ThermalState,MemoryMB"));
            Assert.IsTrue(csv.Contains("10,16.70,59.8,0,180.60"));
        }
    }
}
