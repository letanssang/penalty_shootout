using Eleven.Core.Diagnostics;
using NUnit.Framework;

namespace Eleven.Tests.EditMode
{
    /// <summary>Nghiệm thu T04: hợp đồng percentile và CSV. Không cần thiết bị thật.</summary>
    public class PerfHudTests
    {
        [Test]
        public void HistoryLength_Is600Frames()
        {
            Assert.AreEqual(600, PerfHud.HistoryLength, "p95 phải tính trên 600 frame gần nhất");
        }

        [Test]
        public void EndCapture_ReturnsCsvWithHeaderRow()
        {
            PerfHud.BeginCapture("unit-test");
            string csv = PerfHud.EndCapture();

            Assert.IsNotNull(csv);
            StringAssert.StartsWith(
                "frame,total_ms,gpu_ms,cpu_main_ms,cpu_render_ms,draw_calls,set_pass_calls," +
                "triangles,gc_alloc_bytes,texture_memory_bytes,battery_level,thermal_state",
                csv);
        }

        [Test]
        public void FrameStats_IsStruct()
        {
            Assert.IsTrue(typeof(FrameStats).IsValueType, "FrameStats phải là struct để nằm gọn trong ring buffer");
        }
    }
}
