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

    /// <summary>
    /// Chữ HUD, vẽ bằng IMGUI ở toạ độ màn hình.
    ///
    /// Vì sao IMGUI chứ không TextMesh: TextMesh là mesh trong không gian thế giới — trên build
    /// thiết bị nó nằm ở gốc toạ độ, bị hình học che hoặc lọt ngoài khung camera, nên HUD gần như
    /// chắc chắn không nhìn thấy. IMGUI luôn vẽ đè lên trên, độc lập camera và scene.
    ///
    /// Cấp phát: GUIStyle/GUIContent tạo một lần và dùng lại; GUI.Label không-layout không sinh rác.
    /// Chuỗi hiển thị chỉ dựng lại 4 lần/giây (~200 B mỗi lần) — các khung còn lại cấp phát bằng 0.
    /// Muốn 0 tuyệt đối phải dùng TextMeshPro.SetCharArray(char[]), cần thêm gói UGUI.
    /// </summary>
    public sealed class PerfHudRenderer : MonoBehaviour
    {
        readonly StringBuilder sb = new StringBuilder(256);
        readonly GUIContent content = new GUIContent(string.Empty);
        GUIStyle style;          // chỉ dựng được bên trong OnGUI
        float nextRefresh;
        const float RefreshInterval = 0.25f;

        public bool IsShown { get; private set; }

        public void SetVisible(bool value)
        {
            IsShown = value;
            if (value) nextRefresh = 0f; // vẽ ngay lập tức, đỡ chờ 1/4 giây
        }

        void Update()
        {
            if (!IsShown || Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + RefreshInterval;
            content.text = BuildText();
        }

        string BuildText()
        {
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
              .Append("  tex ").Append(cur.textureMemoryBytes / (1024 * 1024)).Append("MB\n");
            sb.Append("battery ").Append((cur.batteryLevel * 100f).ToString("F0")).Append('%')
              .Append("  thermal ").Append(cur.thermalState);
            return sb.ToString();
        }

        void OnGUI()
        {
            if (!IsShown) return;
            if (Event.current.type != EventType.Repaint) return; // bỏ qua Layout: khỏi vẽ hai lần

            if (style == null)
            {
                // Cỡ chữ theo DPI: 14pt trên màn 160 dpi, để trên điện thoại vẫn đọc được.
                float dpi = Screen.dpi > 1f ? Screen.dpi : 160f;
                style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = Mathf.Clamp(Mathf.RoundToInt(14f * dpi / 160f), 14, 48),
                    alignment = TextAnchor.UpperLeft,
                    richText = false,
                };
                style.normal.textColor = Color.white;
            }

            float pad = style.fontSize * 0.5f;
            // safeArea đo từ đáy màn hình, GUI đo từ đỉnh — phải lật lại, nếu không HUD chui
            // xuống dưới notch thay vì tránh nó.
            var safe = Screen.safeArea;
            float topInset = Screen.height - (safe.y + safe.height);
            var rect = new Rect(safe.x + pad, topInset + pad, safe.width - pad * 2f, safe.height * 0.4f);

            // Nền tối mờ để chữ trắng đọc được trên cỏ sáng.
            var size = style.CalcSize(content);
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(rect.x - pad * 0.5f, rect.y - pad * 0.5f,
                                     size.x + pad, size.y + pad), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(rect, content, style);
        }
    }
}
