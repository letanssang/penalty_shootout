using System.Collections.Generic;
using Eleven.Core;
using NUnit.Framework;
using UnityEngine;

namespace Eleven.Tests.EditMode
{
    /// <summary>Nghiệm thu T03: ép bậc bằng PlayerPrefs, tính ổn định của Detect, OnTierChanged bắn một lần.</summary>
    public class DeviceTierTests
    {
        int savedOverride;
        bool hadOverride;

        [SetUp]
        public void SetUp()
        {
            hadOverride = PlayerPrefs.HasKey(DeviceTier.OverrideKey);
            savedOverride = PlayerPrefs.GetInt(DeviceTier.OverrideKey, -1);
        }

        [TearDown]
        public void TearDown()
        {
            if (hadOverride) PlayerPrefs.SetInt(DeviceTier.OverrideKey, savedOverride);
            else PlayerPrefs.DeleteKey(DeviceTier.OverrideKey);
        }

        [TestCase(0, QualityTier.A)]
        [TestCase(1, QualityTier.B)]
        [TestCase(2, QualityTier.C)]
        public void Override_ForcesTier(int value, QualityTier expected)
        {
            PlayerPrefs.SetInt(DeviceTier.OverrideKey, value);
            Assert.AreEqual(expected, DeviceTier.Detect());
        }

        [Test]
        public void Override_OutOfRange_FallsBackToHardwareDetection()
        {
            PlayerPrefs.SetInt(DeviceTier.OverrideKey, 99);
            var withBadOverride = DeviceTier.Detect();
            PlayerPrefs.DeleteKey(DeviceTier.OverrideKey);
            Assert.AreEqual(DeviceTier.Detect(), withBadOverride);
        }

        [Test]
        public void Detect_IsStableOnSameMachine()
        {
            PlayerPrefs.DeleteKey(DeviceTier.OverrideKey);
            Assert.AreEqual(DeviceTier.Detect(), DeviceTier.Detect());
        }

        [Test]
        public void OnTierChanged_FiresExactlyOncePerChange()
        {
            var fired = new List<QualityTier>();
            void Handler(QualityTier t) => fired.Add(t);

            DeviceTier.OnTierChanged += Handler;
            try
            {
                DeviceTier.Apply(QualityTier.A);
                fired.Clear();               // bỏ qua lần khởi tạo đầu tiên

                DeviceTier.Apply(QualityTier.C);
                DeviceTier.Apply(QualityTier.C); // lặp lại cùng bậc: không được bắn thêm
                DeviceTier.Apply(QualityTier.B);

                Assert.AreEqual(new[] { QualityTier.C, QualityTier.B }, fired.ToArray());
            }
            finally
            {
                DeviceTier.OnTierChanged -= Handler;
            }
        }

        [Test]
        public void Apply_SetsCurrent()
        {
            DeviceTier.Apply(QualityTier.B);
            Assert.AreEqual(QualityTier.B, DeviceTier.Current);
        }
    }
}
