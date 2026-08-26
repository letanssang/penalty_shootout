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
        float cachedCpuMain, cachedCpuRender, cachedGpu;
        float cachedBatteryLevel = -1f;

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
            // FrameTimingManager.CaptureFrameTimings() và SystemInfo.batteryLevel cấp phát rác
            // trên Android (đo thật: 240/240 khung, ~600B/khung dù renderer đã đổi sang UGUI) —
            // dồn chung vào nhịp đo chậm với nhiệt độ, vì cả ba đều là số liệu đổi chậm.
            if (Time.unscaledTime >= nextSlowSampleTime)
            {
                FrameTimingManager.CaptureFrameTimings();
                // GetLatestTimings trả uint — gán thẳng vào int là lỗi biên dịch.
                uint got = FrameTimingManager.GetLatestTimings(1, frameTimings);
                if (got > 0)
                {
                    var t = frameTimings[0];
                    cachedCpuMain = (float)t.cpuMainThreadFrameTime;
                    cachedCpuRender = (float)t.cpuRenderThreadFrameTime;
                    cachedGpu = (float)t.gpuFrameTime;
                }

                cachedThermalState = ThermalStatusReader.Read();
                cachedBatteryLevel = SystemInfo.batteryLevel;
                nextSlowSampleTime = Time.unscaledTime + 0.5f;
            }

            float total = Mathf.Max(cachedGpu, Time.unscaledDeltaTime * 1000f);

            PerfHud.Record(new FrameStats
            {
                cpuMainMs = cachedCpuMain,
                cpuRenderMs = cachedCpuRender,
                gpuMs = cachedGpu,
                totalMs = total,
                drawCalls = (int)drawCalls.LastValue,
                triangles = (int)triangles.LastValue,
                setPassCalls = (int)setPassCalls.LastValue,
                gcAllocBytes = gcAlloc.LastValue,
                textureMemoryBytes = textureMem.Valid ? textureMem.LastValue : 0,
                batteryLevel = cachedBatteryLevel >= 0f ? cachedBatteryLevel : -1f,
                thermalState = cachedThermalState,
            });
        }
    }
}
