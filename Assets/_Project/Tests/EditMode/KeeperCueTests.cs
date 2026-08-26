using System;
using NUnit.Framework;
using Unity.Mathematics;
using Eleven.Keeper;
using UnityEngine;

namespace Eleven.Tests.EditMode
{
    /// <summary>
    /// Edit-mode unit tests for T17 — Keeper cue extraction.
    /// All tests run without play-mode, animation, or scene setup.
    /// </summary>
    [TestFixture]
    public sealed class KeeperCueTests
    {
        // =====================================================================
        //  HELPER: lightweight Transform hierarchy created purely for testing
        // =====================================================================

        private static GameObject MakeGO(string name, Vector3 pos, Quaternion rot)
        {
            var go = new GameObject(name);
            go.transform.SetPositionAndRotation(pos, rot);
            return go;
        }

        private static (KickerBoneCueSource src, Transform root, Transform plantFoot, Transform hips, GameObject container)
            CreateBoneSource(
                Vector3 rootPos,
                Quaternion rootRot,
                Vector3 footPos,
                Vector3 hipsPos,
                Quaternion hipsRot,
                float3 ballPos,
                float runUpDuration)
        {
            var container  = new GameObject("KickerContainer");
            var rootGo     = MakeGO("Root",      rootPos, rootRot);
            var footGo     = MakeGO("PlantFoot", footPos, Quaternion.identity);
            var hipsGo     = MakeGO("Hips",      hipsPos, hipsRot);

            rootGo.transform.SetParent(container.transform, true);
            footGo.transform.SetParent(container.transform, true);
            hipsGo.transform.SetParent(container.transform, true);

            var src = container.AddComponent<KickerBoneCueSource>();
            src.SetBones(rootGo.transform, footGo.transform, hipsGo.transform);
            src.ballPosition  = ballPos;
            src.runUpDuration = runUpDuration;

            return (src, rootGo.transform, footGo.transform, hipsGo.transform, container);
        }

        // =====================================================================
        //  1. MockCueSource — fixed mode returns configured values
        // =====================================================================

        [Test]
        public void MockCueSource_FixedMode_ReturnsConfiguredValues()
        {
            var mock = new MockCueSource
            {
                FixedCues = new KeeperCues
                {
                    plantFootLateralOffset = 0.15f,
                    hipYawDegrees          = 12f,
                    approachAngleDegrees   = -8f,
                    runUpLength            = 3.5f,
                    timeToContact          = 999f,
                    observability          = 999f
                },
                RunUpDuration = 1f,
                Interpolate   = false
            };

            KeeperCues c = mock.Sample(0.6f);

            Assert.AreEqual(0.15f, c.plantFootLateralOffset, 1e-5f);
            Assert.AreEqual(12f,   c.hipYawDegrees,          1e-5f);
            Assert.AreEqual(-8f,   c.approachAngleDegrees,   1e-5f);
            Assert.AreEqual(3.5f,  c.runUpLength,            1e-5f);
            Assert.AreEqual(0.6f,  c.timeToContact,          1e-5f, "timeToContact must echo the parameter");
        }

        // =====================================================================
        //  2. MockCueSource — observability formula
        // =====================================================================

        [Test]
        public void MockCueSource_Observability_MatchesFormula()
        {
            var mock = new MockCueSource { RunUpDuration = 2f, Interpolate = false };

            Assert.AreEqual(0f, mock.Sample(2f).observability, 1e-5f);
            Assert.AreEqual(1f, mock.Sample(0f).observability, 1e-5f);
            Assert.AreEqual(0.5f, mock.Sample(1f).observability, 1e-5f);
        }

        // =====================================================================
        //  3. Observability increases monotonically as timeToContact decreases
        // =====================================================================

        [Test]
        public void MockCueSource_Observability_MonotonicallyIncreases()
        {
            var mock = new MockCueSource { RunUpDuration = 1.5f, Interpolate = false };

            float prev = -1f;
            for (int i = 0; i <= 100; i++)
            {
                float ttc = 1.5f * (1f - i / 100f);
                float obs = mock.Sample(ttc).observability;
                Assert.GreaterOrEqual(obs, prev, $"observability must not decrease (ttc={ttc})");
                prev = obs;
            }
        }

        // =====================================================================
        //  4. MockCueSource — interpolated mode
        // =====================================================================

        [Test]
        public void MockCueSource_InterpolatedMode_LerpsBetweenStartAndEnd()
        {
            var mock = new MockCueSource
            {
                Interpolate   = true,
                RunUpDuration = 2f,
                StartCues = new KeeperCues
                {
                    plantFootLateralOffset = 0f,
                    hipYawDegrees          = 0f,
                    approachAngleDegrees   = 0f,
                    runUpLength            = 0f
                },
                FixedCues = new KeeperCues
                {
                    plantFootLateralOffset = 0.20f,
                    hipYawDegrees          = 10f,
                    approachAngleDegrees   = -6f,
                    runUpLength            = 4f
                }
            };

            KeeperCues start = mock.Sample(2f);
            Assert.AreEqual(0f, start.plantFootLateralOffset, 1e-5f);
            Assert.AreEqual(0f, start.hipYawDegrees,          1e-5f);

            KeeperCues end = mock.Sample(0f);
            Assert.AreEqual(0.20f, end.plantFootLateralOffset, 1e-5f);
            Assert.AreEqual(10f,   end.hipYawDegrees,          1e-5f);

            KeeperCues mid = mock.Sample(1f);
            Assert.AreEqual(0.10f, mid.plantFootLateralOffset, 1e-4f);
            Assert.AreEqual(5f,    mid.hipYawDegrees,          1e-4f);
        }

        // =====================================================================
        //  5. MockCueSource — observability clamped at 0 and 1
        // =====================================================================

        [Test]
        public void MockCueSource_Observability_ClampedAtBoundaries()
        {
            var mock = new MockCueSource { RunUpDuration = 1f };

            Assert.AreEqual(0f, mock.Sample(5f).observability, 1e-5f);
            Assert.AreEqual(1f, mock.Sample(-1f).observability, 1e-5f);
        }

        // =====================================================================
        //  6. MockCueSource — deterministic (same input → same output)
        // =====================================================================

        [Test]
        public void MockCueSource_Deterministic_SameInputSameOutput()
        {
            var mock = new MockCueSource
            {
                FixedCues = new KeeperCues
                {
                    plantFootLateralOffset = 0.12f,
                    hipYawDegrees          = 7f,
                    approachAngleDegrees   = -3f,
                    runUpLength            = 2.5f
                },
                RunUpDuration = 1.2f
            };

            KeeperCues a = mock.Sample(0.4f);
            KeeperCues b = mock.Sample(0.4f);

            Assert.AreEqual(a.plantFootLateralOffset, b.plantFootLateralOffset, 1e-6f);
            Assert.AreEqual(a.hipYawDegrees,          b.hipYawDegrees,          1e-6f);
            Assert.AreEqual(a.approachAngleDegrees,   b.approachAngleDegrees,   1e-6f);
            Assert.AreEqual(a.runUpLength,            b.runUpLength,            1e-6f);
            Assert.AreEqual(a.timeToContact,          b.timeToContact,          1e-6f);
            Assert.AreEqual(a.observability,          b.observability,          1e-6f);
        }

        // =====================================================================
        //  7. KickerBoneCueSource — plant foot lateral offset
        // =====================================================================

        [Test]
        public void BoneCueSource_PlantFootLateralOffset_CorrectSign()
        {
            var (src, _, _, _, container) = CreateBoneSource(
                rootPos:       new Vector3(0f, 0f, -3f),
                rootRot:       Quaternion.identity,
                footPos:       new Vector3(0.2f, 0f, 0f),
                hipsPos:       new Vector3(0f, 1f, -1f),
                hipsRot:       Quaternion.identity,
                ballPos:       float3.zero,
                runUpDuration: 1f);

            try
            {
                KeeperCues c = src.Sample(0.5f);
                Assert.AreEqual(0.2f, c.plantFootLateralOffset, 0.01f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(container);
            }
        }

        [Test]
        public void BoneCueSource_PlantFootLateralOffset_NegativeWhenLeft()
        {
            var (src, _, _, _, container) = CreateBoneSource(
                rootPos:       new Vector3(0f, 0f, -3f),
                rootRot:       Quaternion.identity,
                footPos:       new Vector3(-0.15f, 0f, 0f),
                hipsPos:       new Vector3(0f, 1f, -1f),
                hipsRot:       Quaternion.identity,
                ballPos:       float3.zero,
                runUpDuration: 1f);

            try
            {
                KeeperCues c = src.Sample(0.5f);
                Assert.AreEqual(-0.15f, c.plantFootLateralOffset, 0.01f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(container);
            }
        }

        // =====================================================================
        //  8. KickerBoneCueSource — hip yaw
        // =====================================================================

        [Test]
        public void BoneCueSource_HipYaw_ZeroWhenFacingShotDirection()
        {
            var (src, _, _, _, container) = CreateBoneSource(
                rootPos:       new Vector3(0f, 0f, -3f),
                rootRot:       Quaternion.identity,
                footPos:       Vector3.zero,
                hipsPos:       new Vector3(0f, 1f, -1f),
                hipsRot:       Quaternion.LookRotation(Vector3.forward, Vector3.up),
                ballPos:       float3.zero,
                runUpDuration: 1f);

            try
            {
                KeeperCues c = src.Sample(0.5f);
                Assert.AreEqual(0f, c.hipYawDegrees, 1f, "Hip yaw should be ~0 when facing +Z");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(container);
            }
        }

        [Test]
        public void BoneCueSource_HipYaw_NonZeroWhenRotated()
        {
            float angle = 30f;
            Quaternion hipsRot = Quaternion.Euler(0f, angle, 0f);

            var (src, _, _, _, container) = CreateBoneSource(
                rootPos:       new Vector3(0f, 0f, -3f),
                rootRot:       Quaternion.identity,
                footPos:       Vector3.zero,
                hipsPos:       new Vector3(0f, 1f, -1f),
                hipsRot:       hipsRot,
                ballPos:       float3.zero,
                runUpDuration: 1f);

            try
            {
                KeeperCues c = src.Sample(0.5f);
                Assert.AreEqual(angle, math.abs(c.hipYawDegrees), 2f, "Hip yaw magnitude should be approximately 30°");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(container);
            }
        }

        // =====================================================================
        //  9. KickerBoneCueSource — approach angle
        // =====================================================================

        [Test]
        public void BoneCueSource_ApproachAngle_ZeroWhenStraightOn()
        {
            var (src, _, _, _, container) = CreateBoneSource(
                rootPos:       new Vector3(0f, 0f, -4f),
                rootRot:       Quaternion.identity,
                footPos:       Vector3.zero,
                hipsPos:       new Vector3(0f, 1f, -2f),
                hipsRot:       Quaternion.identity,
                ballPos:       float3.zero,
                runUpDuration: 1f);

            try
            {
                KeeperCues c = src.Sample(0.5f);
                Assert.AreEqual(0f, c.approachAngleDegrees, 2f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(container);
            }
        }

        [Test]
        public void BoneCueSource_ApproachAngle_NonZeroWhenAngled()
        {
            var (src, _, _, _, container) = CreateBoneSource(
                rootPos:       new Vector3(-3f, 0f, -3f),
                rootRot:       Quaternion.identity,
                footPos:       Vector3.zero,
                hipsPos:       new Vector3(-1.5f, 1f, -1.5f),
                hipsRot:       Quaternion.identity,
                ballPos:       float3.zero,
                runUpDuration: 1f);

            try
            {
                KeeperCues c = src.Sample(0.5f);
                Assert.AreNotEqual(0f, c.approachAngleDegrees, "Approach angle should be non-zero for angled run-up");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(container);
            }
        }

        // =====================================================================
        //  10. KickerBoneCueSource — run-up length
        // =====================================================================

        [Test]
        public void BoneCueSource_RunUpLength_ZeroBeforeStartRunUp()
        {
            var (src, _, _, _, container) = CreateBoneSource(
                rootPos:       new Vector3(0f, 0f, -5f),
                rootRot:       Quaternion.identity,
                footPos:       Vector3.zero,
                hipsPos:       new Vector3(0f, 1f, -2f),
                hipsRot:       Quaternion.identity,
                ballPos:       float3.zero,
                runUpDuration: 1f);

            try
            {
                KeeperCues c = src.Sample(0.5f);
                Assert.AreEqual(0f, c.runUpLength, 1e-5f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(container);
            }
        }

        [Test]
        public void BoneCueSource_RunUpLength_MeasuresDistanceFromOrigin()
        {
            var (src, root, _, _, container) = CreateBoneSource(
                rootPos:       new Vector3(0f, 0f, -5f),
                rootRot:       Quaternion.identity,
                footPos:       Vector3.zero,
                hipsPos:       new Vector3(0f, 1f, -2f),
                hipsRot:       Quaternion.identity,
                ballPos:       float3.zero,
                runUpDuration: 1f);

            try
            {
                src.StartRunUp();
                root.position = new Vector3(0f, 0f, -2f);
                KeeperCues c = src.Sample(0.3f);
                Assert.AreEqual(3f, c.runUpLength, 0.05f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(container);
            }
        }

        // =====================================================================
        //  11. KickerBoneCueSource — observability
        // =====================================================================

        [Test]
        public void BoneCueSource_Observability_MatchesFormula()
        {
            var (src, _, _, _, container) = CreateBoneSource(
                rootPos:       new Vector3(0f, 0f, -3f),
                rootRot:       Quaternion.identity,
                footPos:       Vector3.zero,
                hipsPos:       new Vector3(0f, 1f, -1f),
                hipsRot:       Quaternion.identity,
                ballPos:       float3.zero,
                runUpDuration: 2f);

            try
            {
                Assert.AreEqual(0f,   src.Sample(2f).observability, 1e-5f);
                Assert.AreEqual(0.5f, src.Sample(1f).observability, 1e-5f);
                Assert.AreEqual(1f,   src.Sample(0f).observability, 1e-5f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(container);
            }
        }

        // =====================================================================
        //  12. KickerBoneCueSource — timeToContact echoed
        // =====================================================================

        [Test]
        public void BoneCueSource_TimeToContact_EchoesParameter()
        {
            var (src, _, _, _, container) = CreateBoneSource(
                rootPos:       new Vector3(0f, 0f, -3f),
                rootRot:       Quaternion.identity,
                footPos:       Vector3.zero,
                hipsPos:       new Vector3(0f, 1f, -1f),
                hipsRot:       Quaternion.identity,
                ballPos:       float3.zero,
                runUpDuration: 1f);

            try
            {
                Assert.AreEqual(0.42f, src.Sample(0.42f).timeToContact, 1e-6f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(container);
            }
        }

        // =====================================================================
        //  13. Determinism — same transforms yield identical cues
        // =====================================================================

        [Test]
        public void BoneCueSource_Deterministic_SameTransformsSameResult()
        {
            var (src, _, _, _, container) = CreateBoneSource(
                rootPos:       new Vector3(-1f, 0f, -4f),
                rootRot:       Quaternion.identity,
                footPos:       new Vector3(0.1f, 0f, -0.05f),
                hipsPos:       new Vector3(-0.5f, 1f, -2f),
                hipsRot:       Quaternion.Euler(0f, 15f, 0f),
                ballPos:       float3.zero,
                runUpDuration: 1.5f);

            try
            {
                src.StartRunUp();

                KeeperCues a = src.Sample(0.7f);
                KeeperCues b = src.Sample(0.7f);

                Assert.AreEqual(a.plantFootLateralOffset, b.plantFootLateralOffset, 1e-6f);
                Assert.AreEqual(a.hipYawDegrees,          b.hipYawDegrees,          1e-6f);
                Assert.AreEqual(a.approachAngleDegrees,   b.approachAngleDegrees,   1e-6f);
                Assert.AreEqual(a.runUpLength,            b.runUpLength,            1e-6f);
                Assert.AreEqual(a.timeToContact,          b.timeToContact,          1e-6f);
                Assert.AreEqual(a.observability,          b.observability,          1e-6f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(container);
            }
        }

        // =====================================================================
        //  14. ICueSource interface fulfilled by both implementations
        // =====================================================================

        [Test]
        public void MockCueSource_Implements_ICueSource()
        {
            ICueSource source = new MockCueSource();
            Assert.IsNotNull(source);
            KeeperCues c = source.Sample(0.5f);
            Assert.AreEqual(0.5f, c.timeToContact, 1e-6f);
        }

        [Test]
        public void BoneCueSource_Implements_ICueSource()
        {
            var (src, _, _, _, container) = CreateBoneSource(
                rootPos:       Vector3.zero,
                rootRot:       Quaternion.identity,
                footPos:       Vector3.zero,
                hipsPos:       Vector3.up,
                hipsRot:       Quaternion.identity,
                ballPos:       float3.zero,
                runUpDuration: 1f);

            try
            {
                ICueSource source = src;
                Assert.IsNotNull(source);
                KeeperCues c = source.Sample(0.3f);
                Assert.AreEqual(0.3f, c.timeToContact, 1e-6f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(container);
            }
        }

        // =====================================================================
        //  15. KeeperCues is a value type (struct)
        // =====================================================================

        [Test]
        public void KeeperCues_IsValueType()
        {
            Assert.IsTrue(typeof(KeeperCues).IsValueType, "KeeperCues must be a struct");
        }

        // =====================================================================
        //  16. Observability monotonically increases on BoneCueSource too
        // =====================================================================

        [Test]
        public void BoneCueSource_Observability_MonotonicallyIncreases()
        {
            var (src, _, _, _, container) = CreateBoneSource(
                rootPos:       new Vector3(0f, 0f, -3f),
                rootRot:       Quaternion.identity,
                footPos:       Vector3.zero,
                hipsPos:       new Vector3(0f, 1f, -1f),
                hipsRot:       Quaternion.identity,
                ballPos:       float3.zero,
                runUpDuration: 1.2f);

            try
            {
                float prev = -1f;
                for (int i = 0; i <= 60; i++)
                {
                    float ttc = 1.2f * (1f - i / 60f);
                    float obs = src.Sample(ttc).observability;
                    Assert.GreaterOrEqual(obs, prev, $"monotonicity violated at ttc={ttc}");
                    prev = obs;
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(container);
            }
        }

        // =====================================================================
        //  17. Edge case: RunUpDuration near zero does not produce NaN / Inf
        // =====================================================================

        [Test]
        public void MockCueSource_TinyRunUpDuration_NoNaN()
        {
            var mock = new MockCueSource { RunUpDuration = 0f };
            KeeperCues c = mock.Sample(0f);

            Assert.IsFalse(float.IsNaN(c.observability));
            Assert.IsFalse(float.IsInfinity(c.observability));
        }

        [Test]
        public void BoneCueSource_TinyRunUpDuration_NoNaN()
        {
            var (src, _, _, _, container) = CreateBoneSource(
                rootPos:       Vector3.zero,
                rootRot:       Quaternion.identity,
                footPos:       Vector3.zero,
                hipsPos:       Vector3.up,
                hipsRot:       Quaternion.identity,
                ballPos:       float3.zero,
                runUpDuration: 0f);

            try
            {
                KeeperCues c = src.Sample(0f);
                Assert.IsFalse(float.IsNaN(c.observability));
                Assert.IsFalse(float.IsInfinity(c.observability));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(container);
            }
        }
    }
}
