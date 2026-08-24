using System.Text;
using UnityEngine;

namespace Eleven.Core.Diagnostics
{
    /// <summary>Đọc trạng thái nhiệt. iOS qua native plugin, Android qua PowerManager (JNI).</summary>
    static class ThermalStatusReader
    {
#if UNITY_IOS && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        static extern int ElevenNative_ThermalState();

        public static int Read()
        {
            try { return ElevenNative_ThermalState(); }
            catch (System.EntryPointNotFoundException) { return 0; }
        }
#elif UNITY_ANDROID && !UNITY_EDITOR
        static AndroidJavaObject powerManager; // giữ sẵn tham chiếu JNI, tránh cấp phát lặp lại

        public static int Read()
        {
            if (powerManager == null)
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                    powerManager = activity.Call<AndroidJavaObject>("getSystemService", "power");
                // AndroidJavaObject giữ global ref của riêng nó — không cần NewGlobalRef thủ công.
            }
            if (powerManager == null) return 0;
            // API 29+: getCurrentThermalStatus trả 0..5 → nén về thang 0..3 của contract.
            int status = powerManager.Call<int>("getCurrentThermalStatus");
            return status <= 3 ? status : 3;
        }
#else
        public static int Read() => 0; // Editor/desktop: không có cảm biến nhiệt đáng tin cậy
#endif
    }

    /// <summary>Chữ HUD. Làm mới chuỗi 4 lần/giây — đây là nơi cấp phát DUY NHẤT của HUD.</summary>
    public sealed class PerfHudRenderer : MonoBehaviour
    {
        TextMesh textMesh;
        Renderer meshRenderer;
        readonly StringBuilder sb = new StringBuilder(256);
        float nextRefresh;
        const float RefreshInterval = 0.25f;

        public bool IsShown { get; private set; }

        void Awake()
        {
            textMesh = gameObject.AddComponent<TextMesh>();
            textMesh.fontSize = 32;
            textMesh.characterSize = 0.35f;
            textMesh.anchor = TextAnchor.UpperLeft;
            textMesh.color = Color.white;
            meshRenderer = GetComponent<Renderer>();
            IsShown = false;
            meshRenderer.enabled = false; // đo ngầm: sampler vẫn chạy khi HUD ẩn
        }

        public void SetVisible(bool value)
        {
            IsShown = value;
            if (meshRenderer != null) meshRenderer.enabled = value;
            if (value) nextRefresh = 0f; // vẽ ngay lập tức, đỡ chờ 1/4 giây
        }

        void Update()
        {
            if (!IsShown || Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + RefreshInterval;

            var cur = PerfHud.Current;
            var p95 = PerfHud.Percentile(0.95f);

            sb.Length = 0;
            sb.Append("Eleven Metres · ").Append(Application.version).Append('\n');
            sb.Append("avg ").Append(cur.totalMs.ToString("F1")).Append("ms")
              .Append("  p95 ").Append(p95.totalMs.ToString("F1")).Append("ms")
              .Append("  gpu ").Append(cur.gpuMs.ToString("F1")).Append("ms\n");
            sb.Append("draws ").Append(cur.drawCalls)
              .Append("  passes ").Append(cur.setPassCalls)
              .Append("  tris ").Append(cur.triangles).Append('\n');
            sb.Append("gc/frame ").Append(cur.gcAllocBytes).Append("B")
              .Append("  tex ").Append((cur.textureMemoryBytes / (1024 * 1024))).Append("MB\n");
            sb.Append("battery ").Append((cur.batteryLevel * 100f).ToString("F0")).Append('%')
              .Append("  thermal ").Append(cur.thermalState);

            textMesh.text = sb.ToString();
        }
    }
}
