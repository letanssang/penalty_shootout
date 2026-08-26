using Unity.Profiling;
using UnityEngine;

namespace Eleven.Core.Diagnostics
{
    /// <summary>Vòng lặp lấy số liệu mỗi khung. Không cấp phát trong Update.</summary>
    public sealed class PerfHudSampler : MonoBehaviour
    {
        ProfilerRecorder drawCalls;
        ProfilerRecorder setPassCalls;
        ProfilerRecorder triangles;
        ProfilerRecorder gcAlloc;
        ProfilerRecorder textureMem;

        readonly FrameTiming[] frameTimings = new FrameTiming[1];
        float nextSlowSampleTime;
        int cachedThermalState;

        void OnEnable()
        {
            drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls");
            setPassCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls");
            triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles");
            gcAlloc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            textureMem = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Texture Memory");
        }

        void OnDisable()
        {
            drawCalls.Dispose(); setPassCalls.Dispose(); triangles.Dispose();
            gcAlloc.Dispose(); textureMem.Dispose();
        }

        void Update()
        {
            FrameTimingManager.CaptureFrameTimings();
            // GetLatestTimings trả uint — gán thẳng vào int là lỗi biên dịch.
            uint got = FrameTimingManager.GetLatestTimings(1, frameTimings);

            float cpuMain = 0f, cpuRender = 0f, gpu = 0f, total;
            if (got > 0)
            {
                var t = frameTimings[0];
                cpuMain = (float)t.cpuMainThreadFrameTime;
                cpuRender = (float)t.cpuRenderThreadFrameTime;
                gpu = (float)t.gpuFrameTime;
                total = Mathf.Max(gpu, Time.unscaledDeltaTime * 1000f);
            }
            else
            {
                total = Time.unscaledDeltaTime * 1000f; // fallback trước khi timing sẵn sàng
            }

            if (Time.unscaledTime >= nextSlowSampleTime)
            {
                // Nhiệt/battery chỉ đổi chậm — hỏi 2 lần/giây thay vì mỗi khung.
                cachedThermalState = ThermalStatusReader.Read();
                nextSlowSampleTime = Time.unscaledTime + 0.5f;
            }

            PerfHud.Record(new FrameStats
            {
                cpuMainMs = cpuMain,
                cpuRenderMs = cpuRender,
                gpuMs = gpu,
                totalMs = total,
                drawCalls = (int)drawCalls.LastValue,
                triangles = (int)triangles.LastValue,
                setPassCalls = (int)setPassCalls.LastValue,
                gcAllocBytes = gcAlloc.LastValue,
                textureMemoryBytes = textureMem.Valid ? textureMem.LastValue : 0,
                batteryLevel = SystemInfo.batteryLevel >= 0f ? SystemInfo.batteryLevel : -1f,
                thermalState = cachedThermalState,
            });
        }
    }
}
