// Sinh boi mo hinh duoc giao viec (9router), da qua ra soat tinh cua Claude 2026-08-26.
// CHUA CHAY TEST SONG trong Unity -> chua tick muc nghiem thu nao trong backlog.
using Eleven.Shooter;
using NUnit.Framework;
using Unity.Mathematics;

namespace Eleven.Shooter.Tests {
    [TestFixture]
    public class PhysicalUnitsTests {
        [Test]
        [TestCase(0f, ExpectedResult = PhysicalUnits.DefaultDpi)]
        [TestCase(-100f, ExpectedResult = PhysicalUnits.DefaultDpi)]
        [TestCase(5f, ExpectedResult = PhysicalUnits.DefaultDpi)]
        [TestCase(5000f, ExpectedResult = PhysicalUnits.DefaultDpi)]
        [TestCase(float.NaN, ExpectedResult = PhysicalUnits.DefaultDpi)]
        [TestCase(float.PositiveInfinity, ExpectedResult = PhysicalUnits.DefaultDpi)]
        [TestCase(float.NegativeInfinity, ExpectedResult = PhysicalUnits.DefaultDpi)]
        public float SanitizeDpi_InvalidValues_FallbacksToDefault(float rawDpi) {
            return PhysicalUnits.SanitizeDpi(rawDpi);
        }

        [Test]
        [TestCase(100f, 100f)]
        [TestCase(264f, 264f)] // iPad
        [TestCase(326f, 326f)] // iPhone SE
        [TestCase(460f, 460f)] // iPhone Pro
        [TestCase(700f, 700f)]
        public void SanitizeDpi_ValidValues_RemainUnchanged(float validDpi, float expected) {
            Assert.AreEqual(expected, PhysicalUnits.SanitizeDpi(validDpi), 1e-4f);
        }

        [Test]
        public void ToCentimeters_CalculatesCorrectly() {
            float dpi = 254f; // 254 dpi -> 100 px = 1 inch = 2.54 cm -> 100 px = 1.0 cm
            float2 pixels = new float2(100f, 200f);

            float2 cm = PhysicalUnits.ToCentimeters(pixels, dpi);

            Assert.AreEqual(1.0f, cm.x, 1e-4f);
            Assert.AreEqual(2.0f, cm.y, 1e-4f);
        }

        [Test]
        public void ToPixels_CalculatesCorrectly() {
            float dpi = 254f;
            float2 cm = new float2(1.0f, 2.0f);

            float2 pixels = PhysicalUnits.ToPixels(cm, dpi);

            Assert.AreEqual(100f, pixels.x, 1e-4f);
            Assert.AreEqual(200f, pixels.y, 1e-4f);
        }
    }
}
