using System;
using System.Collections.Generic;
using UnityEngine;
using Eleven.Match;

namespace Eleven.UI
{
    /// <summary>
    /// Giao diện HUD hiển thị bảng điểm loạt sút luân lưu 5 quả chuẩn FIFA và banner thông báo.
    /// Tự động thích ứng tốt trên mọi kích thước màn hình điện thoại (DPI responsive).
    /// </summary>
    public sealed class ScoreboardUI : MonoBehaviour
    {
        public event Action OnReplayClicked;
        public event Action OnNextKickClicked;

        private List<KickResult> homeResults = new List<KickResult>();
        private List<KickResult> awayResults = new List<KickResult>();
        private string bannerMessage = "HÃY VUỐT ĐỂ SÚT PHẠT ĐỀN!";
        private string subtitleMessage = "Vuốt nhanh về phía khung thành để thực hiện cú sút";
        private Color bannerColor = Color.yellow;
        private bool showBanner = true;
        private bool isReplayAvailable = false;
        private int currentKickIndex = 0;

        public void UpdateScores(List<KickResult> home, List<KickResult> away, int kickIndex)
        {
            homeResults = new List<KickResult>(home);
            awayResults = new List<KickResult>(away);
            currentKickIndex = kickIndex;
        }

        public void ShowBanner(string title, string subtitle, Color color, bool replayAvailable = true)
        {
            bannerMessage = title;
            subtitleMessage = subtitle;
            bannerColor = color;
            showBanner = true;
            isReplayAvailable = replayAvailable;
        }

        public void HideBanner()
        {
            showBanner = false;
        }

        private void OnGUI()
        {
            // Thiết lập phong cách hiển thị cao cấp
            float scale = Screen.width / 1080.0f;
            if (scale < 0.6f) scale = 0.6f;

            // 1. BẢNG ĐIỂM Ở GÓC TRÊN MÀN HÌNH (Scoreboard)
            GUILayout.BeginArea(new Rect(20, 20, Screen.width - 40, 140 * scale));
            GUILayout.BeginVertical("box");

            GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(22 * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            GUILayout.Label($"🏆 LOẠT SÚT LUÂN LƯU 11 MÉT — LƯỢT {currentKickIndex + 1}/5", titleStyle);

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            // Hiển thị 5 chấm lượt sút của người chơi
            GUIStyle dotStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(28 * scale),
                alignment = TextAnchor.MiddleCenter
            };

            GUILayout.Label("BẠN:  ", titleStyle);
            for (int i = 0; i < 5; i++)
            {
                string dot = "⚪"; // Chưa sút
                if (i < homeResults.Count)
                {
                    dot = homeResults[i] == KickResult.Scored ? "🟢" : "🔴";
                }
                GUILayout.Label(dot, dotStyle);
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
            GUILayout.EndArea();

            // 2. BANNER THÔNG BÁO Ở GIỮA MÀN HÌNH KHI KẾT THÚC CÚ SÚT
            if (showBanner)
            {
                float bannerW = Screen.width * 0.85f;
                float bannerH = 200 * scale;
                float bannerX = (Screen.width - bannerW) * 0.5f;
                float bannerY = Screen.height * 0.35f;

                GUILayout.BeginArea(new Rect(bannerX, bannerY, bannerW, bannerH));
                GUILayout.BeginVertical("box");

                GUIStyle mainBannerStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = Mathf.RoundToInt(34 * scale),
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = bannerColor }
                };

                GUIStyle subStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = Mathf.RoundToInt(18 * scale),
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Color.white }
                };

                GUILayout.Space(10);
                GUILayout.Label(bannerMessage, mainBannerStyle);
                GUILayout.Label(subtitleMessage, subStyle);
                GUILayout.Space(10);

                // Nút bấm Replay và Lượt tiếp theo
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();

                GUIStyle btnStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = Mathf.RoundToInt(20 * scale),
                    fontStyle = FontStyle.Bold,
                    fixedHeight = 45 * scale,
                    fixedWidth = 160 * scale
                };

                if (isReplayAvailable && GUILayout.Button("🎬 REPLAY", btnStyle))
                {
                    OnReplayClicked?.Invoke();
                }

                GUILayout.Space(20);

                if (GUILayout.Button("➡️ TIẾP TỤC", btnStyle))
                {
                    OnNextKickClicked?.Invoke();
                }

                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                GUILayout.EndVertical();
                GUILayout.EndArea();
            }
        }
    }
}
