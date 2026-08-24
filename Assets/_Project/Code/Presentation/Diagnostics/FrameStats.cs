namespace Eleven.Core.Diagnostics
{
    /// <summary>Ảnh chụp một khung hình. Struct thuần — không tham chiếu, an toàn để nằm trong ring buffer.</summary>
    public struct FrameStats
    {
        public float cpuMainMs, cpuRenderMs, gpuMs, totalMs;
        public int   drawCalls, triangles, setPassCalls;
        public long  gcAllocBytes, textureMemoryBytes;
        public float batteryLevel;
        public int   thermalState;   // 0 bình thường .. 3 nghiêm trọng
    }
}
