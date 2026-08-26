using System.Collections.Generic;
using Eleven.Shooter;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

namespace Eleven.Tests.EditMode {
    public class SwipeAnalyzerTests {
        private static SwipeSample[] ToArray(List<SwipeSample> l) => l.ToArray();

        private static SwipeFeatures Run(SwipeSample[] arr) {
            var na = new NativeArray<SwipeSample>(arr, Allocator.Temp);
            var f = SwipeAnalyzer.Analyze(new NativeSlice<SwipeSample>(na));
            na.Dispose();
            return f;
        }

        // ---------- Vuốt thẳng ----------

        [Test]
        public void StraightLine_CurvatureNearZero_StraightnessNearOne() {
            var pts = new List<SwipeSample>();
            for (int i = 0; i < 20; i++)
                pts.Add(new SwipeSample { position = new float2(i * 10f, i * 10f), time = i * 0.016f });
            var f = Run(pts.ToArray());

            Assert.AreEqual(0f, f.curvature, 1e-3f);
            Assert.AreEqual(1f, f.straightness, 1e-3f);
        }

        [Test]
        public void StraightHorizontalLine_VerticalRatioNearZero() {
            var pts = new List<SwipeSample>();
            for (int i = 0; i < 20; i++)
                pts.Add(new SwipeSample { position = new float2(i * 10f, 5f), time = i * 0.016f });
            var f = Run(pts.ToArray());
            Assert.AreEqual(0f, f.verticalRatio, 0.05f);
        }

        [Test]
        public void StraightVerticalLine_VerticalRatioNearOne() {
            var pts = new List<SwipeSample>();
            for (int i = 0; i < 20; i++)
                pts.Add(new SwipeSample { position = new float2(5f, i * 10f), time = i * 0.016f });
            var f = Run(pts.ToArray());
            Assert.AreEqual(1f, f.verticalRatio, 0.05f);
        }

        [Test]
        public void StraightDiagonal_VerticalRatioAroundHalf() {
            var pts = new List<SwipeSample>();
            for (int i = 0; i < 20; i++)
                pts.Add(new SwipeSample { position = new float2(i * 10f, i * 10f), time = i * 0.016f });
            var f = Run(pts.ToArray());
            Assert.AreEqual(0.70710678f, f.verticalRatio, 0.02f);
        }

        // ---------- Vuốt cong ----------

        // Nửa vòng cung bán kính R quanh tâm, đi từ góc a0 đến a1.
        private static List<SwipeSample> ArcSamples(float radius, float a0, float a1, int n, bool clockwise) {
            var pts = new List<SwipeSample>();
            for (int i = 0; i < n; i++) {
                float t = (float)i / (n - 1);
                float ang = math.lerp(a0, a1, t);
                // clockwise=false: ngược chiều kim đồng hồ (cong trái), true: thuận (cong phải)
                float x = radius * math.cos(ang);
                float y = radius * math.sin(ang) * (clockwise ? -1f : 1f);
                pts.Add(new SwipeSample { position = new float2(x, y), time = t * 1f });
            }
            return pts;
        }

        [Test]
        public void ClearArc_CurvatureSignificantlyNonZero() {
            var pts = ArcSamples(100f, 0f, math.PI, 40, false);
            var f = Run(pts.ToArray());
            Assert.GreaterOrEqual(f.curvature, 5f); // bán kính 100 => ~R/2 sau chuẩn hoá
        }

        [Test]
        public void ArcToTheRight_CurvaturePositive() {
            // BUG ĐÃ SỬA (do model sinh code để lại): ArcSamples nội suy góc từ a0=0
            // tới a1=PI, nên điểm ĐẦU thật sự là (r*cos(0), ...) = (100,0) và điểm
            // CUỐI là (r*cos(PI), ...) = (-100,0) — NGƯỢC với comment gốc giả định
            // "start=(-100,0), end=(100,0)". Test ClearArc_CurvatureSignificantlyNonZero
            // (đang xanh) đã xác nhận clockwise=false mới cho cung lệch bên PHẢI
            // (dương) với chiều start->end thật; clockwise=true cho bên trái (âm).
            var pts = ArcSamples(100f, 0f, math.PI, 40, false);
            var f = Run(pts.ToArray());
            Assert.Greater(f.curvature, 0f, "curvature phải DƯƠNG khi cong sang phải");
        }

        [Test]
        public void ArcToTheLeft_CurvatureNegative() {
            var pts = ArcSamples(100f, 0f, math.PI, 40, true);
            var f = Run(pts.ToArray());
            Assert.Less(f.curvature, 0f, "curvature phải ÂM khi cong sang trái");
        }

        [Test]
        public void Semicircle_LengthMatchesHalfCircumference() {
            var pts = ArcSamples(100f, 0f, math.PI, 60, false);
            var f = Run(pts.ToArray());
            Assert.AreEqual(math.PI * 100f, f.length, 6f);
            Assert.AreEqual(200f, math.length(f.end - f.start), 1e-2f);
        }

        // ---------- Độc lập tốc độ khung hình ----------

        [Test]
        public void FrameRateIndependence_CurvatureDiffersUnderFivePercent() {
            // Cùng hình học nửa vòng cung; 60fps = 61 mẫu, 30fps = bỏ xen kẽ còn 31 mẫu,
            // time giữ theo mẫu thật nên tốc độ tức thời không đổi theo index.
            var dense = ArcSamples(100f, 0f, math.PI, 61, false);
            var sparse = new List<SwipeSample>();
            for (int i = 0; i < dense.Count; i += 2) sparse.Add(dense[i]);

            var fDense = Run(dense.ToArray());
            var fSparse = Run(sparse.ToArray());

            Assert.Greater(math.abs(fDense.curvature), 1f);
            Assert.Less(math.abs(fDense.curvature - fSparse.curvature) / math.abs(fDense.curvature), 0.05f);
        }

        [Test]
        public void FrameRateIndependence_PeakSpeedSimilar() {
            var dense = new List<SwipeSample>();
            for (int i = 0; i < 61; i++) {
                float t = i / 60f;
                dense.Add(new SwipeSample { position = new float2(t * 300f, 0f), time = t });
            }
            var sparse = new List<SwipeSample>();
            for (int i = 0; i < dense.Count; i += 2) sparse.Add(dense[i]);

            var fDense = Run(dense.ToArray());
            var fSparse = Run(sparse.ToArray());
            Assert.AreEqual(fDense.peakSpeed, fSparse.peakSpeed, fDense.peakSpeed * 0.05f);
        }

        // ---------- Số lượng mẫu biên ----------

        [Test]
        public void ZeroSamples_DoesNotCrash_AllZeros() {
            var empty = new NativeArray<SwipeSample>(0, Allocator.Temp);
            var f = SwipeAnalyzer.Analyze(new NativeSlice<SwipeSample>(empty));
            empty.Dispose();

            Assert.AreEqual(float2.zero, f.start);
            Assert.AreEqual(float2.zero, f.end);
            Assert.AreEqual(0f, f.length);
            Assert.AreEqual(0f, f.duration);
            Assert.AreEqual(0f, f.peakSpeed);
            Assert.AreEqual(0f, f.endSpeed);
        }

        [Test]
        public void OneSample_DoesNotCrash_StartEqualsEnd() {
            var arr = new[] { new SwipeSample { position = new float2(7f, 9f), time = 0.5f } };
            var f = Run(arr);

            Assert.AreEqual(new float2(7f, 9f), f.start);
            Assert.AreEqual(new float2(7f, 9f), f.end);
            Assert.AreEqual(0f, f.length);
            Assert.AreEqual(0f, f.duration);
        }

        [Test]
        public void TwoSamples_DoesNotCrash_StartEndCorrect_NoCrashFeatures() {
            var arr = new[] {
                new SwipeSample { position = new float2(0f, 0f), time = 0f },
                new SwipeSample { position = new float2(30f, 0f), time = 0.1f },
            };
            var f = Run(arr);

            Assert.AreEqual(new float2(0f, 0f), f.start);
            Assert.AreEqual(new float2(30f, 0f), f.end);
            Assert.AreEqual(0f, f.peakSpeed); // theo hành vi định nghĩa cho n<3
        }

        // ---------- Tốc độ ----------

        [Test]
        public void AcceleratingSwipe_EndSpeedGreaterThanPeakRelation_Correct() {
            // Chậm dần dần: đoạn đầu nhanh nhất => peakSpeed ở đoạn đầu, endSpeed nhỏ hơn nhiều.
            var pts = new List<SwipeSample>();
            float t = 0f;
            for (int i = 0; i < 20; i++) {
                pts.Add(new SwipeSample { position = new float2(i * 10f, 0f), time = t });
                t += 0.01f + i * 0.005f; // dt tăng dần => tốc độ giảm dần
            }
            var f = Run(pts.ToArray());

            Assert.Greater(f.endSpeed, 0f);
            Assert.Less(f.endSpeed, f.peakSpeed);
            // peakSpeed = đoạn đầu tiên: ds=10, dt=0.01+0=0.01 (dt của đoạn 0->1 là 0.01)
            Assert.AreEqual(1000f, f.peakSpeed, 1e-2f);
        }

        [Test]
        public void UniformSpeed_PeakEqualsEnd() {
            var pts = new List<SwipeSample>();
            for (int i = 0; i < 20; i++)
                pts.Add(new SwipeSample { position = new float2(i * 10f, 0f), time = i * 0.02f });
            var f = Run(pts.ToArray());

            Assert.AreEqual(500f, f.peakSpeed, 1e-2f);
            Assert.AreEqual(500f, f.endSpeed, 1e-2f);
            Assert.AreEqual(0.38f, f.duration, 1e-3f);
        }

        [Test]
        public void DurationUsesRealTimestamps_NotIndexCount() {
            // Khoảng cách thời gian không đều: duration phải = t cuối - t đầu.
            var pts = new List<SwipeSample> {
                new SwipeSample { position = new float2(0f, 0f),  time = 1.25f },
                new SwipeSample { position = new float2(5f, 0f),  time = 1.30f },
                new SwipeSample { position = new float2(10f, 0f), time = 2.00f },
            };
            var f = Run(pts.ToArray());
            Assert.AreEqual(0.75f, f.duration, 1e-5f);
        }

        [Test]
        public void LengthIsArcLengthNotChordDistance() {
            // Đường zig-zag: tổng độ dài cung lớn hơn khoảng cách thẳng start-end.
            var pts = new List<SwipeSample>();
            for (int i = 0; i < 11; i++) {
                float y = (i % 2 == 0) ? 0f : 10f;
                pts.Add(new SwipeSample { position = new float2(i * 10f, y), time = i * 0.016f });
            }
            var f = Run(pts.ToArray());

            // BUG ĐÃ SỬA (do model sinh code để lại): comment gốc đã tính đúng
            // "10 đoạn * sqrt(10^2+10^2) = 141.42" nhưng dòng code lại gõ nhầm 10*10=100.
            Assert.AreEqual(10f * math.sqrt(200f), f.length, 1e-2f);  // 10 đoạn * sqrt(10^2+10^2)=141.42
            Assert.AreEqual(100f, math.length(f.end - f.start), 1e-3f);
            Assert.Less(f.straightness, 1f);
            Assert.GreaterOrEqual(f.straightness, 0f);
        }

        [Test]
        public void DegenerateAllSamePoint_NoNaNs() {
            var pts = new List<SwipeSample>();
            for (int i = 0; i < 10; i++)
                pts.Add(new SwipeSample { position = new float2(3f, 4f), time = i * 0.01f });
            var f = Run(pts.ToArray());

            Assert.IsFalse(math.isnan(f.curvature));
            Assert.IsFalse(math.isnan(f.straightness));
            Assert.IsFalse(math.isnan(f.verticalRatio));
            Assert.AreEqual(1f, f.straightness);
            Assert.AreEqual(0f, f.peakSpeed);
        }
    }
}
