using System;
using System.Collections.Generic;
using UnityEngine;
using Eleven.Match;
using Eleven.Shooter;

namespace Eleven.UI
{
    /// <summary>
    /// Giao diện HUD tỷ số và Thông số cú sút (Shot HUD):
    /// - Bảng điểm 5 lượt Penalty (POR vs FRA)
    /// - Badge hiển thị tức thì loại cú sút (Knuckleball, Cứa lòng má trong, Panenka, Mu bàn chân)
    /// - Tốc độ bóng thời gian thực (km/h)
    /// - Banner kết quả và Replay Slow-motion
    /// </summary>
    public sealed class ScoreboardUI : MonoBehaviour
    {
        public event Action OnReplayClicked;
        public event Action OnNextKickClicked;

        private List<KickResult> homeResults = new List<KickResult>();
        private List<KickResult> awayResults = new List<KickResult>();
        private string bannerMessage = "";
        private string subtitleMessage = "";
        private Color bannerColor = Color.white;
        private bool showBanner = false;
        private bool isReplayAvailable = false;
        private int currentKickIndex = 0;

        // Thông tin cú sút hiện tại
        private string currentShotTypeTitle = "";
        private Color currentShotTypeColor = Color.white;
        private float currentShotSpeedKmh = 0f;
        private bool showShotBadge = false;

        private Texture2D darkBgTex;
        private Texture2D yellowTex;
        private Texture2D boxEmptyTex;
        private Texture2D boxScoredTex;
        private Texture2D boxMissedTex;
        private Texture2D badgeBgTex;

        private void Awake()
        {
            darkBgTex = MakeTex(2, 2, new Color(0.04f, 0.06f, 0.12f, 0.92f));
            yellowTex = MakeTex(2, 2, new Color(0.98f, 0.85f, 0.05f, 1f));
            boxEmptyTex = MakeTex(2, 2, new Color(0.18f, 0.22f, 0.35f, 0.9f));
            boxScoredTex = MakeTex(2, 2, new Color(0.10f, 0.85f, 0.25f, 1f));
            boxMissedTex = MakeTex(2, 2, new Color(0.92f, 0.15f, 0.15f, 1f));
            badgeBgTex = MakeTex(2, 2, new Color(0.08f, 0.10f, 0.18f, 0.90f));
        }

        private void OnDestroy()
        {
            if (darkBgTex != null) Destroy(darkBgTex);
            if (yellowTex != null) Destroy(yellowTex);
            if (boxEmptyTex != null) Destroy(boxEmptyTex);
            if (boxScoredTex != null) Destroy(boxScoredTex);
            if (boxMissedTex != null) Destroy(boxMissedTex);
            if (badgeBgTex != null) Destroy(badgeBgTex);
        }

        public void SetCurrentShotInfo(ShotType type, float speedMps)
        {
            currentShotSpeedKmh = speedMps * 3.6f;
            showShotBadge = true;

            switch (type)
            {
                case ShotType.InsideFoot:
                    currentShotTypeTitle = "🌀 CỨA LÒNG MÁ TRONG (CURVE)";
                    currentShotTypeColor = new Color(0.2f, 0.85f, 1f); // Cyan
                    break;
                case ShotType.Knuckle:
                    currentShotTypeTitle = "⚡ LÁ BÀNG KNUCKLEBALL (UNSTABLE)";
                    currentShotTypeColor = new Color(1f, 0.85f, 0.1f); // Yellow/Gold
                    break;
                case ShotType.Chip:
                    currentShotTypeTitle = "🪶 PANENKA LỐP BÓNG (CHIP)";
                    currentShotTypeColor = new Color(0.4f, 1f, 0.5f); // Green
                    break;
                default:
                    currentShotTypeTitle = "🎯 MU BÀN CHÂN (POWER DRIVE)";
                    currentShotTypeColor = new Color(1f, 0.35f, 0.2f); // Red/Orange
                    break;
            }
        }

        public void HideShotBadge()
        {
            showShotBadge = false;
        }

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
            float scale = Screen.height / 1080.0f;
            if (scale < 0.65f) scale = 0.65f;

            int homeScore = 0;
            for (int i = 0; i < homeResults.Count; i++) if (homeResults[i] == KickResult.Scored) homeScore++;

            int awayScore = 0;
            for (int i = 0; i < awayResults.Count; i++) if (awayResults[i] == KickResult.Scored) awayScore++;

            // 1. BẢNG ĐIỂM GÓC TRÊN BÊN TRÁI (POR vs FRA)
            float boardX = 40 * scale;
            float boardY = 30 * scale;
            float rowW = 340 * scale;
            float rowH = 42 * scale;

            DrawTeamRow(boardX, boardY, rowW, rowH, scale, "POR", homeScore, homeResults);
            DrawTeamRow(boardX, boardY + rowH + 4 * scale, rowW, rowH, scale, "FRA", awayScore, awayResults);

            // 2. BADGE THÔNG SỐ CÚ SÚT (Góc trên bên phải)
            if (showShotBadge)
            {
                float badgeW = 420 * scale;
                float badgeH = 68 * scale;
                float badgeX = Screen.width - badgeW - 40 * scale;
                float badgeY = 30 * scale;

                GUI.DrawTexture(new Rect(badgeX, badgeY, badgeW, badgeH), badgeBgTex);

                GUIStyle typeStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = Mathf.RoundToInt(16 * scale),
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft,
                    normal = { textColor = currentShotTypeColor }
                };

                GUIStyle speedStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = Mathf.RoundToInt(14 * scale),
                    alignment = TextAnchor.MiddleLeft,
                    normal = { textColor = new Color(0.85f, 0.90f, 0.95f) }
                };

                GUI.Label(new Rect(badgeX + 16 * scale, badgeY + 8 * scale, badgeW - 32 * scale, 26 * scale), currentShotTypeTitle, typeStyle);
                GUI.Label(new Rect(badgeX + 16 * scale, badgeY + 34 * scale, badgeW - 32 * scale, 24 * scale), $"🚀 Tốc độ sút: {currentShotSpeedKmh:F1} km/h", speedStyle);
            }

            // 3. BANNER KẾT QUẢ SÚT
            if (showBanner)
            {
                float bannerW = 680 * scale;
                float bannerH = 175 * scale;
                float bannerX = (Screen.width - bannerW) * 0.5f;
                float bannerY = Screen.height * 0.36f;

                GUI.DrawTexture(new Rect(bannerX, bannerY, bannerW, bannerH), darkBgTex);

                GUILayout.BeginArea(new Rect(bannerX, bannerY, bannerW, bannerH));
                GUILayout.BeginVertical();

                GUIStyle mainBannerStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = Mathf.RoundToInt(28 * scale),
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = bannerColor }
                };

                GUIStyle subStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = Mathf.RoundToInt(15 * scale),
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Color.white }
                };

                GUILayout.Space(14 * scale);
                GUILayout.Label(bannerMessage, mainBannerStyle);
                GUILayout.Label(subtitleMessage, subStyle);
                GUILayout.Space(12 * scale);

                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();

                GUIStyle btnStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = Mathf.RoundToInt(16 * scale),
                    fontStyle = FontStyle.Bold,
                    fixedHeight = 42 * scale,
                    fixedWidth = 150 * scale
                };

                if (isReplayAvailable && GUILayout.Button("🎬 REPLAY", btnStyle))
                {
                    OnReplayClicked?.Invoke();
                }

                GUILayout.Space(20 * scale);

                if (GUILayout.Button("➡ TIẾP TỤC", btnStyle))
                {
                    OnNextKickClicked?.Invoke();
                }

                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                GUILayout.EndVertical();
                GUILayout.EndArea();
            }
        }

        private void DrawTeamRow(float x, float y, float w, float h, float scale, string teamName, int score, List<KickResult> kicks)
        {
            GUI.DrawTexture(new Rect(x, y, w, h), darkBgTex);

            GUIStyle nameStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(16 * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.yellow }
            };
            GUI.Label(new Rect(x + 8 * scale, y, 60 * scale, h), teamName, nameStyle);

            float boxSize = 20 * scale;
            float boxSpacing = 6 * scale;
            float boxStartX = x + 80 * scale;
            float boxStartY = y + (h - boxSize) * 0.5f;

            for (int i = 0; i < 5; i++)
            {
                Rect boxRect = new Rect(boxStartX + i * (boxSize + boxSpacing), boxStartY, boxSize, boxSize);
                Texture2D boxTex = boxEmptyTex;

                if (i < kicks.Count)
                {
                    boxTex = (kicks[i] == KickResult.Scored) ? boxScoredTex : boxMissedTex;
                }

                GUI.DrawTexture(boxRect, boxTex);
            }

            float scoreBoxW = 40 * scale;
            float scoreBoxX = x + w - scoreBoxW;
            GUI.DrawTexture(new Rect(scoreBoxX, y, scoreBoxW, h), yellowTex);

            GUIStyle scoreStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(20 * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.black }
            };
            GUI.Label(new Rect(scoreBoxX, y, scoreBoxW, h), score.ToString(), scoreStyle);
        }

        private Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; ++i)
            {
                pix[i] = col;
            }
            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }
    }
}
