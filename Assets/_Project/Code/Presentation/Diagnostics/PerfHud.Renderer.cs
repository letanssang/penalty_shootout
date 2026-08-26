using System.Text;
using UnityEngine;
using UnityEngine.UI;

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
    /// Chữ HUD, vẽ bằng UGUI (Canvas + Text) ở toạ độ màn hình.
    ///
    /// Vì sao không IMGUI: OnGUI ép Unity xử lý một lượt sự kiện Repaint mỗi khung hình, và
    /// chi phí xử lý sự kiện đó cấp phát rác bất kể code bên trong OnGUI có vẽ gì hay không —
    /// đo thật trên Pixel 7 ra 240/240 khung cấp phát, bất kể GUIStyle/GUIContent đã cache.
    /// UGUI chỉ dựng lại mesh khi Graphic bị đánh dấu dirty (đổi text/color/rect); khung nào
    /// không đổi gì thì Canvas.willRenderCanvases không có việc để làm — cấp phát 0 thật.
    ///
    /// Cấp phát còn lại: `Text.text` chỉ được gán trong RefreshText(), tối đa 4 lần/giây theo
    /// RefreshInterval — đây là cấp phát duy nhất, và chỉ xảy ra ở khung làm mới chữ.
    /// </summary>
    public sealed class PerfHudRenderer : MonoBehaviour
    {
        readonly StringBuilder sb = new StringBuilder(256);
        float nextRefresh;
        const float RefreshInterval = 0.25f;
        const int SortingOrder = 32760; // vẽ đè lên mọi thứ khác trong game

        Canvas canvas;
        Image background;
        Text label;

        public bool IsShown { get; private set; }

        public void SetVisible(bool value)
        {
            IsShown = value;
            EnsureUi();
            canvas.enabled = value;
            if (value)
            {
                nextRefresh = 0f; // vẽ ngay lập tức, đỡ chờ 1/4 giây
                RefreshText();
            }
        }

        void Update()
        {
            if (!IsShown || Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + RefreshInterval;
            RefreshText();
        }

        void RefreshText()
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

            label.text = sb.ToString();
            ResizeToFit();
        }

        void ResizeToFit()
        {
            float pad = label.fontSize * 0.5f;
            // safeArea đo từ đáy màn hình, UGUI top-left anchor đo từ đỉnh — phải lật lại,
            // nếu không HUD chui xuống dưới notch thay vì tránh nó.
            var safe = Screen.safeArea;
            float topInset = Screen.height - (safe.y + safe.height);
            var pos = new Vector2(safe.x + pad, -(topInset + pad));

            float w = label.preferredWidth;
            float h = label.preferredHeight;

            label.rectTransform.anchoredPosition = pos;
            label.rectTransform.sizeDelta = new Vector2(w, h);

            // Nền tối mờ để chữ trắng đọc được trên cỏ sáng.
            background.rectTransform.anchoredPosition = pos + new Vector2(-pad * 0.5f, pad * 0.5f);
            background.rectTransform.sizeDelta = new Vector2(w + pad, h + pad);
        }

        void EnsureUi()
        {
            if (canvas != null) return;

            var canvasGo = new GameObject("Canvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;

            var bgGo = new GameObject("Background", typeof(RectTransform));
            bgGo.transform.SetParent(canvasGo.transform, false);
            background = bgGo.AddComponent<Image>();
            background.color = new Color(0f, 0f, 0f, 0.55f);
            background.raycastTarget = false;
            SetTopLeftAnchor(background.rectTransform);

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(canvasGo.transform, false);
            label = labelGo.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.color = Color.white;
            label.alignment = TextAnchor.UpperLeft;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            label.supportRichText = false;
            SetTopLeftAnchor(label.rectTransform);

            // Cỡ chữ theo DPI: 14pt trên màn 160 dpi, để trên điện thoại vẫn đọc được.
            float dpi = Screen.dpi > 1f ? Screen.dpi : 160f;
            label.fontSize = Mathf.Clamp(Mathf.RoundToInt(14f * dpi / 160f), 14, 48);
        }

        static void SetTopLeftAnchor(RectTransform rt)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
        }
    }
}
