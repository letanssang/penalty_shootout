// Sinh boi mo hinh duoc giao viec (9router), da qua ra soat tinh cua Claude 2026-08-26.
// CHUA CHAY TEST SONG trong Unity -> chua tick muc nghiem thu nao trong backlog.
using System;
using Eleven.Shooter;
using NUnit.Framework;
using Unity.Mathematics;

namespace Eleven.Shooter.Tests {
    [TestFixture]
    public class SwipeCollectorTests {
        private SwipeCollector _collector;

        [TearDown]
        public void TearDown() {
            _collector?.Dispose();
            _collector = null;
        }

        [Test]
        public void PhysicalInvariance_SamePhysicalSwipeOnDifferentDevices_YieldsIdenticalFeatures() {
            // Kiểm tra bất biến quan trọng nhất:
            // Cùng một cử chỉ vuốt dài 4.0 cm trong 0.2 giây (10 mẫu)
            // Thiết bị 1: iPhone SE (DPI 326)
            // Thiết bị 2: iPad (DPI 264)
            const float physicalLengthCm = 4.0f;
            const float duration = 0.2f;
            const int sampleCount = 10;

            float dpiIPhone = 326f;
            float dpiIPad = 264f;

            float totalPxIPhone = PhysicalUnits.ToPixels(physicalLengthCm, dpiIPhone);
            float totalPxIPad = PhysicalUnits.ToPixels(physicalLengthCm, dpiIPad);

            // Thu thập trên iPhone SE
            using (var collectorIPhone = new SwipeCollector(32)) {
                collectorIPhone.Begin(new float2(100f, 100f), 0f, dpiIPhone);
                for (int i = 1; i < sampleCount - 1; i++) {
                    float t = (float)i / (sampleCount - 1);
                    float2 px = new float2(100f + totalPxIPhone * t, 100f + math.sin(t * math.PI) * (totalPxIPhone * 0.1f));
                    collectorIPhone.Move(px, t * duration);
                }
                var resultIPhone = collectorIPhone.End(new float2(100f + totalPxIPhone, 100f), duration);

                Assert.IsTrue(resultIPhone.valid);
                Assert.AreEqual(SwipeEndReason.Completed, resultIPhone.reason);

                // Thu thập trên iPad
                using (var collectorIPad = new SwipeCollector(32)) {
                    collectorIPad.Begin(new float2(200f, 200f), 0f, dpiIPad);
                    for (int i = 1; i < sampleCount - 1; i++) {
                        float t = (float)i / (sampleCount - 1);
                        float2 px = new float2(200f + totalPxIPad * t, 200f + math.sin(t * math.PI) * (totalPxIPad * 0.1f));
                        collectorIPad.Move(px, t * duration);
                    }
                    var resultIPad = collectorIPad.End(new float2(200f + totalPxIPad, 200f), duration);

                    Assert.IsTrue(resultIPad.valid);
                    Assert.AreEqual(SwipeEndReason.Completed, resultIPad.reason);

                    // Sai khác độ dài giữa 2 máy phải dưới 1% (0.04 cm)
                    float lengthDifference = math.abs(resultIPhone.features.length - resultIPad.features.length);
                    float maxAllowedDiff = physicalLengthCm * 0.01f;
                    Assert.LessOrEqual(lengthDifference, maxAllowedDiff, "Độ dài cú vuốt (cm) trên 2 thiết bị khác nhau vượt quá sai số 1%.");

                    // Kiểm tra độ cong và độ thẳng tương đồng
                    Assert.AreEqual(resultIPhone.features.curvature, resultIPad.features.curvature, 0.05f);
                    Assert.AreEqual(resultIPhone.features.straightness, resultIPad.features.straightness, 0.05f);
                }
            }
        }

        [Test]
        public void End_WithLessThanThreeSamples_ReturnsTooFewSamples() {
            _collector = new SwipeCollector(16);

            // 1 mẫu
            _collector.Begin(new float2(0f, 0f), 0.0f, 300f);
            var res1 = _collector.End(new float2(0f, 0f), 0.0f);
            Assert.IsFalse(res1.valid);
            Assert.AreEqual(SwipeEndReason.TooFewSamples, res1.reason);

            // 2 mẫu
            _collector.Begin(new float2(0f, 0f), 0.0f, 300f);
            _collector.Move(new float2(10f, 10f), 0.05f);
            var res2 = _collector.End(new float2(10f, 10f), 0.05f);
            Assert.IsFalse(res2.valid);
            Assert.AreEqual(SwipeEndReason.TooFewSamples, res2.reason);
        }

        [Test]
        public void Discard_ResetsCollectionState_AllowsSubsequentSwipe() {
            _collector = new SwipeCollector(16);

            _collector.Begin(new float2(10f, 10f), 0.0f, 300f);
            _collector.Move(new float2(20f, 20f), 0.05f);
            Assert.IsTrue(_collector.IsCollecting);

            _collector.Discard();
            Assert.IsFalse(_collector.IsCollecting);

            // Cú vuốt tiếp theo vẫn hoàn thành bình thường
            _collector.Begin(new float2(0f, 0f), 1.0f, 300f);
            _collector.Move(new float2(50f, 50f), 1.05f);
            _collector.Move(new float2(100f, 100f), 1.10f);
            var result = _collector.End(new float2(150f, 150f), 1.15f);

            Assert.IsTrue(result.valid);
            Assert.AreEqual(SwipeEndReason.Completed, result.reason);
        }

        [Test]
        public void BufferOverflow_DoesNotCrash_PreservesTrajectoryAndMarksReason() {
            // Bộ đệm chỉ chứa tối đa 5 mẫu
            _collector = new SwipeCollector(capacity: 5);

            _collector.Begin(new float2(0f, 0f), 0.0f, 300f);
            for (int i = 1; i <= 10; i++) {
                _collector.Move(new float2(i * 10f, i * 10f), i * 0.02f);
            }
            var result = _collector.End(new float2(200f, 200f), 0.25f);

            Assert.IsTrue(result.valid);
            Assert.AreEqual(SwipeEndReason.BufferOverflow, result.reason);
            Assert.AreEqual(5, result.sampleCount);
        }

        [Test]
        public void OutOfOrderCalls_DoNotThrowExceptions() {
            _collector = new SwipeCollector(16);

            // Move hoặc End khi chưa Begin
            Assert.DoesNotThrow(() => _collector.Move(new float2(10f, 10f), 0.1f));
            SwipeResult resBeforeBegin = default;
            Assert.DoesNotThrow(() => resBeforeBegin = _collector.End(new float2(10f, 10f), 0.1f));
            Assert.IsFalse(resBeforeBegin.valid);

            // Gọi Begin 2 lần liên tiếp
            Assert.DoesNotThrow(() => {
                _collector.Begin(new float2(0f, 0f), 0.0f, 300f);
                _collector.Begin(new float2(10f, 10f), 0.1f, 300f);
                _collector.Move(new float2(20f, 20f), 0.2f);
                _collector.Move(new float2(30f, 30f), 0.3f);
            });

            // Gọi End 2 lần liên tiếp
            SwipeResult firstEnd = default;
            SwipeResult secondEnd = default;
            Assert.DoesNotThrow(() => firstEnd = _collector.End(new float2(40f, 40f), 0.4f));
            Assert.DoesNotThrow(() => secondEnd = _collector.End(new float2(50f, 50f), 0.5f));

            Assert.IsTrue(firstEnd.valid);
            Assert.IsFalse(secondEnd.valid);
        }

        [Test]
        public void NonMonotonicTimeSamples_AreIgnored() {
            _collector = new SwipeCollector(16);

            _collector.Begin(new float2(0f, 0f), 1.0f, 300f);
            // Mẫu thời gian bằng hoặc nhỏ hơn mẫu trước sẽ bị bỏ qua
            _collector.Move(new float2(10f, 10f), 1.0f);
            _collector.Move(new float2(15f, 15f), 0.9f);
            _collector.Move(new float2(20f, 20f), 1.05f);
            _collector.Move(new float2(30f, 30f), 1.10f);
            var result = _collector.End(new float2(40f, 40f), 1.15f);

            Assert.IsTrue(result.valid);
            Assert.AreEqual(4, result.sampleCount); // Begin (1) + 2 Moves hợp lệ + End (1) = 4
        }

        [Test]
        public void DoubleDispose_DoesNotThrowException() {
            _collector = new SwipeCollector(16);

            Assert.DoesNotThrow(() => {
                _collector.Dispose();
                _collector.Dispose();
            });

            // Gọi các hàm khác sau khi Dispose không được crash
            Assert.DoesNotThrow(() => _collector.Begin(float2.zero, 0f, 300f));
            Assert.DoesNotThrow(() => _collector.Move(float2.zero, 0.1f));
            Assert.DoesNotThrow(() => _collector.End(float2.zero, 0.2f));
            Assert.DoesNotThrow(() => _collector.Discard());
            Assert.IsFalse(_collector.IsCollecting);
        }
    }
}
