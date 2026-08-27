using System;
using System.Collections.Generic;
using UnityEngine;
using Eleven.Match;

namespace Eleven.UI
{
    /// <summary>
    /// Giao diện HUD chuẩn eFootball / EA Sports FC:
    /// - Bảng điểm 2 đội (POR vs FRA) góc trên bên trái với các ô vuông 5 lượt sút luân lưu.
    /// - Banner kết quả chỉ hiển thị SAU KHI SÚT để không che chắn tầm nhìn.
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

        private Texture2D darkBgTex;
        private Texture2D yellowTex;
        private Texture2D boxEmptyTex;
        private Texture2D boxScoredTex;
        private Texture2D boxMissedTex;
        private Texture2D aimingBarTex;

        private void Awake()
        {
            darkBgTex = MakeTex(2, 2, new Color(0.04f, 0.06f, 0.12f, 0.92f));
            yellowTex = MakeTex(2, 2, new Color(0.98f, 0.85f, 0.05f, 1f));
            boxEmptyTex = MakeTex(2, 2, new Color(0.18f, 0.22f, 0.35f, 0.9f));
            boxScoredTex = MakeTex(2, 2, new Color(0.10f, 0.85f, 0.25f, 1f));
            boxMissedTex = MakeTex(2, 2, new Color(0.92f, 0.15f, 0.15f, 1f));
            aimingBarTex = MakeTex(2, 2, new Color(0.10f, 0.10f, 0.10f, 0.90f));
        }

        private void OnDestroy()
        {
            if (darkBgTex != null) Destroy(darkBgTex);
            if (yellowTex != null) Destroy(yellowTex);
            if (boxEmptyTex != null) Destroy(boxEmptyTex);
            if (boxScoredTex != null) Destroy(boxScoredTex);
            if (boxMissedTex != null) Destroy(boxMissedTex);
            if (aimingBarTex != null) Destroy(aimingBarTex);
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

            // 1. BẢNG ĐIỂM GÓC TRÊN BÊN TRÁI (POR vs FRA eFootball Style)
            float boardX = 50 * scale;
            float boardY = 40 * scale;
            float rowW = 360 * scale;
            float rowH = 46 * scale;

            DrawTeamRow(boardX, boardY, rowW, rowH, scale, "POR", homeScore, homeResults);
            DrawTeamRow(boardX, boardY + rowH + 4 * scale, rowW, rowH, scale, "FRA", awayScore, awayResults);

            // Thanh hướng dẫn ngắm góc trên bên phải
            float guideW = 380 * scale;
            float guideH = 38 * scale;
            float guideX = Screen.width - guideW - 50 * scale;
            float guideY = 40 * scale;
            GUI.DrawTexture(new Rect(guideX, guideY, guideW, guideH), darkBgTex);

            GUIStyle guideStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(15 * scale),
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.9f, 0.92f, 0.95f) }
            };
            GUI.Label(new Rect(guideX, guideY, guideW, guideH), "Display Aiming Guides          RB", guideStyle);

            // 2. THANH THƯỚC NGẮM DƯỚI QUẢ BÓNG (Aiming Indicator Bar như ảnh mẫu)
            float barW = 240 * scale;
            float barH = 10 * scale;
            float barX = (Screen.width - barW) * 0.5f;
            float barY = Screen.height * 0.775f;
            GUI.DrawTexture(new Rect(barX, barY, barW, barH), aimingBarTex);

            // 3. BANNER KẾT QUẢ SÚT (Chỉ hiện khi bóng đã bay vào lưới hoặc bị cản phá)
            if (showBanner)
            {
                float bannerW = 680 * scale;
                float bannerH = 160 * scale;
                float bannerX = (Screen.width - bannerW) * 0.5f;
                float bannerY = Screen.height * 0.38f;

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

                GUILayout.Space(12 * scale);
                GUILayout.Label(bannerMessage, mainBannerStyle);
                GUILayout.Label(subtitleMessage, subStyle);
                GUILayout.Space(8 * scale);

                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();

                GUIStyle btnStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = Mathf.RoundToInt(16 * scale),
                    fontStyle = FontStyle.Bold,
                    fixedHeight = 40 * scale,
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

            // Tên đội
            GUIStyle nameStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(18 * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.yellow }
            };
            GUI.Label(new Rect(x + 10 * scale, y, 65 * scale, h), teamName, nameStyle);

            // 5 ô vuông luân lưu
            float boxSize = 22 * scale;
            float boxSpacing = 6 * scale;
            float boxStartX = x + 90 * scale;
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

            // Điểm số
            float scoreBoxW = 45 * scale;
            float scoreBoxX = x + w - scoreBoxW;
            GUI.DrawTexture(new Rect(scoreBoxX, y, scoreBoxW, h), yellowTex);

            GUIStyle scoreStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(22 * scale),
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
