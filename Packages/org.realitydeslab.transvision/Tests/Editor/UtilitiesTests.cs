using NUnit.Framework;
using UnityEngine;

namespace TransVision.Tests
{
    public class UtilitiesTests
    {
        const float Tolerance = 1e-5f;

        [TestCase(5f, 0f, 10f, 0f, 1f, 0.5f)]
        [TestCase(0f, 0f, 10f, 100f, 200f, 100f)]
        [TestCase(10f, 0f, 10f, 100f, 200f, 200f)]
        [TestCase(2.5f, 0f, 10f, 1f, 0f, 0.75f)]
        [TestCase(15f, 0f, 10f, 0f, 1f, 1.5f)]
        public void Remap_IsLinear_AndExtrapolatesWithoutClamp(float v, float srcMin, float srcMax, float dstMin, float dstMax, float expected)
        {
            Assert.That(Utilities.Remap(v, srcMin, srcMax, dstMin, dstMax), Is.EqualTo(expected).Within(Tolerance));
        }

        [TestCase(-5f, 0f)]
        [TestCase(15f, 1f)]
        public void Remap_WithClamp_StaysInsideTheDestinationRange(float v, float expected)
        {
            Assert.That(Utilities.Remap(v, 0f, 10f, 0f, 1f, need_clamp: true), Is.EqualTo(expected).Within(Tolerance));
        }

        [Test]
        public void Remap_EmptySourceRange_ReturnsDestinationMin()
        {
            Assert.That(Utilities.Remap(3f, 4f, 4f, 7f, 9f), Is.EqualTo(7f));
        }

        [Test]
        public void RgbToHsv_PrimaryAndGrayColors()
        {
            AssertHsv(Utilities.RgbToHsv(new Vector3(1, 0, 0)), 0f, 1f, 1f);
            AssertHsv(Utilities.RgbToHsv(new Vector3(0, 1, 0)), 1f / 3f, 1f, 1f);
            AssertHsv(Utilities.RgbToHsv(new Vector3(0, 0, 1)), 2f / 3f, 1f, 1f);
            AssertHsv(Utilities.RgbToHsv(new Vector3(0.5f, 0.5f, 0.5f)), 0f, 0f, 0.5f);
            AssertHsv(Utilities.RgbToHsv(Vector3.zero), 0f, 0f, 0f);
        }

        [Test]
        public void RgbToHsv_MatchesColorRGBToHSV_OnAGrid()
        {
            const int steps = 8;
            for (int r = 0; r <= steps; r++)
            for (int g = 0; g <= steps; g++)
            for (int b = 0; b <= steps; b++)
            {
                var rgb = new Vector3(r, g, b) / steps;
                Color.RGBToHSV(new Color(rgb.x, rgb.y, rgb.z), out float h, out float s, out float v);
                Vector3 hsv = Utilities.RgbToHsv(rgb);

                string where = $"rgb={rgb}";
                // Hue is circular: 0 and 1 are the same red.
                float hueError = Mathf.Abs(hsv.x - h);
                Assert.That(Mathf.Min(hueError, 1f - hueError), Is.LessThan(1e-4f), where);
                Assert.That(hsv.y, Is.EqualTo(s).Within(1e-4f), where);
                Assert.That(hsv.z, Is.EqualTo(v).Within(1e-4f), where);
            }
        }

        static void AssertHsv(Vector3 hsv, float h, float s, float v)
        {
            Assert.That(hsv.x, Is.EqualTo(h).Within(Tolerance), "hue");
            Assert.That(hsv.y, Is.EqualTo(s).Within(Tolerance), "saturation");
            Assert.That(hsv.z, Is.EqualTo(v).Within(Tolerance), "value");
        }
    }
}
