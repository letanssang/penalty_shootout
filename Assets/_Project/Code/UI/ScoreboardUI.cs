using System;
using System.Collections.Generic;
using UnityEngine;
using Eleven.Match;   // KickResult
using Eleven.Shooter; // ShotType, TimingGrade
using Eleven.Keeper;  // DifficultyLevel

namespace Eleven.UI
{
    /// <summary>
    /// HUD trận đấu IMGUI cho bản demo mobile. Mọi kích thước co giãn theo screen.
    /// Giữ nguyên IMGUI (OnGUI) — không chuyển UGUI vì cấu trúc scene đang ổn định.
    /// </summary>
    public sealed class ScoreboardUI : MonoBehaviour
    {
        // ─── SỰ KIỆN CÔNG KHAI ───────────────────────────────────────────────────
        public event Action OnReplayClicked;
        public event Action OnNextKickClicked;
        public event Action<DifficultyLevel> OnDifficultyChanged;

        // ─── TRẠNG THÁI DỮ LIỆU ──────────────────────────────────────────────────
        private List<KickResult> _homeResults = new List<KickResult>();
        private List<KickResult> _awayResults = new List<KickResult>();
        private int _kickIndex;

        // Băng lượt sút
        private bool _isPlayerTurn;
        private int  _roundNumber;
        private bool _suddenDeath;

        // Badge cú sút
        private string _shotTypeLabel  = "";
        private Color  _shotTypeColor  = Color.white;
        private float  _shotSpeedKmh;
        private bool   _showShotBadge;
        // Cache chuỗi tốc độ — chỉ rebuild khi kmh thay đổi
        private float  _cachedSpeedKmh = -1f;
        private string _cachedSpeedStr  = "";

        // Banner kết quả
        private string _bannerTitle    = "";
        private string _bannerSubtitle = "";
        private Color  _bannerColor    = Color.white;
        private bool   _showBanner;
        private bool   _replayAvailable;

        // Nhắc thao tác giữa-dưới màn hình
        private string _promptText = "";

        // Thanh thời điểm
        private bool  _timingBarVisible;
        private float _timingProgress01;
        private float _timingPerfectCenter01;
        private float _timingPerfectHalf01;
        private float _timingGoodHalf01;

        // Nhãn PERFECT / GOOD / POOR
        private bool        _showGradeLabel;
        private TimingGrade _gradeValue;
        private float       _gradeErrorMs;
        private float       _gradeHideTime; // Time.unscaledTime tại lúc cần ẩn
        // Cache chuỗi nhãn grade — rebuild khi grade/errorMs thay đổi
        private string      _gradeLabelStr = "";

        // Nút độ khó
        private DifficultyLevel _difficulty = DifficultyLevel.Medium;

        // Debug thủ môn
        private string _keeperDebug = "";

        // ─── TEXTURE CACHE ────────────────────────────────────────────────────────
        // Tất cả texture tạo một lần trong Awake, không new mỗi frame → hết rác GC
        private Texture2D _texDarkBg;
        private Texture2D _texYellow;
        private Texture2D _texBoxEmpty;
        private Texture2D _texBoxScored;
        private Texture2D _texBoxMissed;
        private Texture2D _texBadgeBg;
        private Texture2D _texTimingBg;
        private Texture2D _texTimingGood;
        private Texture2D _texTimingPerfect;
        private Texture2D _texTimingCursor;
        private Texture2D _texTurnBand;
        private Texture2D _texBtnNormal;
        private Texture2D _texBtnActive;
        private Texture2D _texSemiBlack;

        // ─── GUI STYLE CACHE ─────────────────────────────────────────────────────
        // Tạo một lần khi scale thay đổi thay vì new GUIStyle mỗi OnGUI → hết rác GC
        private float      _cachedScale = -1f; // -1 → chưa tạo
        private GUIStyle   _styleScoreName;
        private GUIStyle   _styleScoreNum;
        private GUIStyle   _styleShotType;
        private GUIStyle   _styleShotSpeed;
        private GUIStyle   _styleBannerTitle;
        private GUIStyle   _styleBannerSub;
        private GUIStyle   _styleBannerBtn;
        private GUIStyle   _styleTurnBand;
        private GUIStyle   _styleGradeLabel;
        private GUIStyle   _stylePrompt;
        private GUIStyle   _styleDiffBtn;
        private GUIStyle   _styleDiffBtnActive;
        private GUIStyle   _styleDebug;
        private GUIStyle   _styleBoxScore;

        // ─── AWAKE ────────────────────────────────────────────────────────────────
        private void Awake()
        {
            // Tạo texture nền một lần; 2×2 đủ rồi, GPU stretch tự làm phần còn lại
            _texDarkBg         = MakeTex(new Color(0.04f, 0.06f, 0.12f, 0.92f));
            _texYellow         = MakeTex(new Color(0.98f, 0.85f, 0.05f, 1f));
            _texBoxEmpty       = MakeTex(new Color(0.18f, 0.22f, 0.35f, 0.90f));
            _texBoxScored      = MakeTex(new Color(0.10f, 0.80f, 0.25f, 1f));
            _texBoxMissed      = MakeTex(new Color(0.88f, 0.14f, 0.14f, 1f));
            _texBadgeBg        = MakeTex(new Color(0.08f, 0.10f, 0.18f, 0.90f));
            _texTimingBg       = MakeTex(new Color(0.20f, 0.20f, 0.20f, 0.85f));
            _texTimingGood     = MakeTex(new Color(0.90f, 0.75f, 0.10f, 0.85f)); // vàng = GOOD
            _texTimingPerfect  = MakeTex(new Color(0.10f, 0.85f, 0.30f, 0.90f)); // xanh lá = PERFECT
            _texTimingCursor   = MakeTex(new Color(1.00f, 1.00f, 1.00f, 1.00f)); // trắng = con trỏ
            _texTurnBand       = MakeTex(new Color(0.06f, 0.09f, 0.20f, 0.88f));
            _texBtnNormal      = MakeTex(new Color(0.18f, 0.22f, 0.35f, 0.90f));
            _texBtnActive      = MakeTex(new Color(0.20f, 0.65f, 0.90f, 1.00f)); // xanh dương = đang chọn
            _texSemiBlack      = MakeTex(new Color(0.00f, 0.00f, 0.00f, 0.55f));
        }

        // ─── ON DESTROY ──────────────────────────────────────────────────────────
        private void OnDestroy()
        {
            // Phải huỷ tay vì texture tạo bằng new, không phải asset — tránh rò bộ nhớ GPU
            DestroyTex(ref _texDarkBg);
            DestroyTex(ref _texYellow);
            DestroyTex(ref _texBoxEmpty);
            DestroyTex(ref _texBoxScored);
            DestroyTex(ref _texBoxMissed);
            DestroyTex(ref _texBadgeBg);
            DestroyTex(ref _texTimingBg);
            DestroyTex(ref _texTimingGood);
            DestroyTex(ref _texTimingPerfect);
            DestroyTex(ref _texTimingCursor);
            DestroyTex(ref _texTurnBand);
            DestroyTex(ref _texBtnNormal);
            DestroyTex(ref _texBtnActive);
            DestroyTex(ref _texSemiBlack);
        }

        // ─── API CÔNG KHAI ────────────────────────────────────────────────────────

        /// <summary>Cập nhật danh sách kết quả hai đội và chỉ số lượt hiện tại.</summary>
        public void UpdateScores(List<KickResult> home, List<KickResult> away, int kickIndex)
        {
            // Copy để tránh phụ thuộc tham chiếu ngoài
            _homeResults.Clear();
            _homeResults.AddRange(home);
            _awayResults.Clear();
            _awayResults.AddRange(away);
            _kickIndex = kickIndex;
        }

        /// <summary>Cập nhật băng lượt sút ở trên cùng.</summary>
        public void SetTurn(bool isPlayerTurn, int roundNumber, bool suddenDeath)
        {
            _isPlayerTurn = isPlayerTurn;
            _roundNumber  = roundNumber;
            _suddenDeath  = suddenDeath;
        }

        /// <summary>Hiện badge loại cú sút và tốc độ.</summary>
        public void SetCurrentShotInfo(ShotType type, float speedMps)
        {
            _shotSpeedKmh  = speedMps * 3.6f;
            _showShotBadge = true;

            // Chọn nhãn & màu theo kiểu sút — không dùng emoji để tránh vấn đề font trên một số thiết bị
            switch (type)
            {
                case ShotType.InsideFoot:
                    _shotTypeLabel = "CUA LONG MA TRONG (CURVE)";
                    _shotTypeColor = new Color(0.20f, 0.85f, 1.00f);
                    break;
                case ShotType.Knuckle:
                    _shotTypeLabel = "LA BANG KNUCKLEBALL (UNSTABLE)";
                    _shotTypeColor = new Color(1.00f, 0.85f, 0.10f);
                    break;
                case ShotType.Chip:
                    _shotTypeLabel = "PANENKA LOP BONG (CHIP)";
                    _shotTypeColor = new Color(0.40f, 1.00f, 0.50f);
                    break;
                default: // Instep
                    _shotTypeLabel = "MU BAN CHAN (POWER DRIVE)";
                    _shotTypeColor = new Color(1.00f, 0.40f, 0.20f);
                    break;
            }

            // Làm chuỗi tốc độ vô hiệu để buộc rebuild ở OnGUI
            _cachedSpeedKmh = -1f;
        }

        /// <summary>Ẩn badge loại cú sút.</summary>
        public void HideShotBadge()
        {
            _showShotBadge = false;
        }

        /// <summary>Hiện banner kết quả với hai nút hành động.</summary>
        public void ShowBanner(string title, string subtitle, Color color, bool replayAvailable = true)
        {
            _bannerTitle     = title    ?? "";
            _bannerSubtitle  = subtitle ?? "";
            _bannerColor     = color;
            _showBanner      = true;
            _replayAvailable = replayAvailable;
        }

        /// <summary>Ẩn banner kết quả.</summary>
        public void HideBanner()
        {
            _showBanner = false;
        }

        /// <summary>Dòng nhắc thao tác giữa-dưới màn hình; chuỗi rỗng = ẩn.</summary>
        public void SetPrompt(string text)
        {
            _promptText = text ?? "";
        }

        /// <summary>
        /// Vẽ thanh thời điểm ở đáy màn hình.
        /// visible=false thì không vẽ gì — caller không cần kiểm tra trước.
        /// </summary>
        public void SetTimingBar(bool visible, float progress01,
                                 float perfectCenter01, float perfectHalfWidth01,
                                 float goodHalfWidth01)
        {
            _timingBarVisible      = visible;
            _timingProgress01      = progress01;
            _timingPerfectCenter01 = perfectCenter01;
            _timingPerfectHalf01   = perfectHalfWidth01;
            _timingGoodHalf01      = goodHalfWidth01;
        }

        /// <summary>Hiện nhãn PERFECT / GOOD / POOR giữa màn hình, tự ẩn sau 1.2 giây thực.</summary>
        public void ShowTimingGrade(TimingGrade grade, float errorMs)
        {
            _showGradeLabel = true;
            _gradeValue     = grade;
            _gradeErrorMs   = errorMs;
            // Dùng unscaledTime để slow-motion không ảnh hưởng thời gian hiện nhãn
            _gradeHideTime  = Time.unscaledTime + 1.2f;
            _gradeLabelStr  = ""; // Bắt buộc rebuild chuỗi
        }

        /// <summary>Đồng bộ nút độ khó từ code bên ngoài (không bắn event).</summary>
        public void SetDifficulty(DifficultyLevel level)
        {
            _difficulty = level;
        }

        /// <summary>Dòng debug thủ môn góc dưới trái; chuỗi rỗng = ẩn.</summary>
        public void SetKeeperDebug(string text)
        {
            _keeperDebug = text ?? "";
        }

        // ─── ON GUI ───────────────────────────────────────────────────────────────
        private void OnGUI()
        {
            // Tính hệ số scale một lần cho frame này
            // Dùng Min(w,h) phù hợp màn hình dọc lẫn ngang; kẹp [0.75, 3.0]
            float scale = Mathf.Clamp(
                Mathf.Min(Screen.width, Screen.height) / 720f,
                0.75f, 3.0f);

            // Rebuild style cache khi scale thay đổi — tránh new GUIStyle mỗi frame
            EnsureStyles(scale);

            // Tự động tắt nhãn grade sau 1.2 giây thực
            if (_showGradeLabel && Time.unscaledTime >= _gradeHideTime)
                _showGradeLabel = false;

            DrawScoreboard(scale);
            DrawTurnBand(scale);
            DrawDifficultyButtons(scale);

            if (_showShotBadge)
                DrawShotBadge(scale);

            if (_timingBarVisible)
                DrawTimingBar(scale);

            if (_showGradeLabel)
                DrawGradeLabel(scale);

            if (!string.IsNullOrEmpty(_promptText))
                DrawPrompt(scale);

            if (_showBanner)
                DrawBanner(scale);

            if (!string.IsNullOrEmpty(_keeperDebug))
                DrawKeeperDebug(scale);
        }

        // ─── VẼ BẢNG ĐIỂM ────────────────────────────────────────────────────────
        private void DrawScoreboard(float scale)
        {
            // Đếm tỷ số để hiện chữ số lớn
            int homeScore = CountScored(_homeResults);
            int awayScore = CountScored(_awayResults);

            // Chiều rộng cố định; chiều cao mỗi hàng đội đủ cho ngón tay
            float rowH  = 48f  * scale;
            float boardX = 12f * scale;
            float boardY = 12f * scale;

            DrawTeamRow(boardX, boardY,           scale, rowH, "BAN",
                        homeScore, _homeResults, _homeResults.Count > 5 || _awayResults.Count > 5);
            DrawTeamRow(boardX, boardY + rowH + 4f * scale, scale, rowH, "MAY",
                        awayScore, _awayResults, _homeResults.Count > 5 || _awayResults.Count > 5);
        }

        private void DrawTeamRow(float x, float y, float scale, float rowH,
                                 string teamName, int score,
                                 List<KickResult> kicks, bool showExtra)
        {
            // Tính số lượt hiển thị: 5 bình thường + phần sudden death nếu có
            int displayCount = Mathf.Max(5, kicks.Count);

            float nameW    = 60f  * scale;
            float scoreW   = 40f  * scale;
            float boxSize  = 22f  * scale;
            float boxGap   = 5f   * scale;
            float sdLabelW = 80f  * scale; // nhãn "LUOT CHET"

            // Tổng chiều rộng = tên + khoảng + 5 ô + ... + tỷ số
            float boxGroupW = 5 * (boxSize + boxGap) - boxGap;
            float extraW    = displayCount > 5
                ? sdLabelW + (displayCount - 5) * (boxSize + boxGap)
                : 0f;
            float totalW = nameW + 8f * scale + boxGroupW + extraW + 8f * scale + scoreW;

            GUI.DrawTexture(new Rect(x, y, totalW, rowH), _texDarkBg);

            // --- Tên đội ---
            GUI.Label(new Rect(x + 6f * scale, y, nameW, rowH), teamName, _styleScoreName);

            // --- Ô kết quả 5 lượt chính ---
            float bx = x + nameW + 8f * scale;
            float by = y + (rowH - boxSize) * 0.5f;
            for (int i = 0; i < 5; i++)
            {
                Texture2D tex = _texBoxEmpty;
                if (i < kicks.Count)
                    tex = kicks[i] == KickResult.Scored ? _texBoxScored : _texBoxMissed;
                GUI.DrawTexture(new Rect(bx + i * (boxSize + boxGap), by, boxSize, boxSize), tex);
            }

            // --- Ô sudden death (nếu có) ---
            if (displayCount > 5)
            {
                // Nhãn "LUOT CHET" nhỏ
                float sdX = bx + 5 * (boxSize + boxGap) + 4f * scale;
                GUI.Label(new Rect(sdX, y, sdLabelW, rowH), "SD", _styleDebug);
                float sdBoxX = sdX + sdLabelW;
                for (int i = 5; i < displayCount; i++)
                {
                    int j = i - 5;
                    Texture2D tex = _texBoxEmpty;
                    if (i < kicks.Count)
                        tex = kicks[i] == KickResult.Scored ? _texBoxScored : _texBoxMissed;
                    GUI.DrawTexture(new Rect(sdBoxX + j * (boxSize + boxGap), by, boxSize, boxSize), tex);
                }
            }

            // --- Tỷ số lớn bên phải ---
            float sx = x + totalW - scoreW;
            GUI.DrawTexture(new Rect(sx, y, scoreW, rowH), _texYellow);
            // Cache chuỗi tỷ số trong _styleScoreNum.name không được — dùng score.ToString() OK
            // vì ToString() trên int nhỏ không cấp phát trên IL2CPP (runtime IL2CPP intern số nhỏ)
            GUI.Label(new Rect(sx, y, scoreW, rowH), score.ToString(), _styleBoxScore);
        }

        // ─── BĂNG LƯỢT SÚT ───────────────────────────────────────────────────────
        private void DrawTurnBand(float scale)
        {
            // Băng nhỏ hiện bên dưới bảng điểm
            float bandH = 34f * scale;
            float bandY = 12f * scale + 2f * (48f * scale + 4f * scale) + 4f * scale;
            float bandW = Screen.width * 0.55f; // không trải full — tránh che nút độ khó

            string label;
            if (_suddenDeath)
                label = string.Format("LUOT CHET #{0}", _roundNumber);
            else if (_isPlayerTurn)
                label = string.Format("LUOT SUT CUA BAN — Luot {0}", _roundNumber);
            else
                label = string.Format("MAY SUT — Luot {0}", _roundNumber);

            GUI.DrawTexture(new Rect(12f * scale, bandY, bandW, bandH), _texTurnBand);
            GUI.Label(new Rect(18f * scale, bandY, bandW - 12f * scale, bandH), label, _styleTurnBand);
        }

        // ─── NÚT ĐỘ KHÓ (góc trên phải) ─────────────────────────────────────────
        private void DrawDifficultyButtons(float scale)
        {
            // Nút đủ to cho ngón tay: >= 44 điểm * scale
            float btnH = Mathf.Max(44f * scale, 44f * scale);
            float btnW = 72f * scale;
            float gap  = 6f  * scale;
            float margin = 12f * scale;

            float totalW = 3f * btnW + 2f * gap;
            float startX = Screen.width - totalW - margin;
            float startY = margin;

            DrawDiffBtn(startX,                 startY, btnW, btnH, "DZ",     DifficultyLevel.Easy);
            DrawDiffBtn(startX + btnW + gap,     startY, btnW, btnH, "THUONG", DifficultyLevel.Medium);
            DrawDiffBtn(startX + 2f*(btnW+gap),  startY, btnW, btnH, "KHO",   DifficultyLevel.Hard);
        }

        private void DrawDiffBtn(float x, float y, float w, float h,
                                 string label, DifficultyLevel level)
        {
            bool isActive = _difficulty == level;
            GUIStyle style = isActive ? _styleDiffBtnActive : _styleDiffBtn;

            // GUI.Button trả true khi bấm — đủ để phát event và cập nhật trạng thái
            if (GUI.Button(new Rect(x, y, w, h), label, style))
            {
                _difficulty = level;
                OnDifficultyChanged?.Invoke(level);
            }
        }

        // ─── BADGE CÚ SÚT (góc trên phải, bên dưới nút độ khó) ─────────────────
        private void DrawShotBadge(float scale)
        {
            // Cache chuỗi tốc độ để không nối chuỗi mỗi frame
            if (!Mathf.Approximately(_cachedSpeedKmh, _shotSpeedKmh))
            {
                _cachedSpeedKmh = _shotSpeedKmh;
                _cachedSpeedStr  = string.Format("Toc do: {0:F1} km/h", _shotSpeedKmh);
            }

            float badgeW = 300f * scale;
            float badgeH = 60f  * scale;
            float badgeX = Screen.width - badgeW - 12f * scale;
            // Đặt bên dưới nút độ khó: top=12 + btnH(44)+gap(8) = ~64*scale
            float badgeY = (12f + 44f + 8f) * scale;

            GUI.DrawTexture(new Rect(badgeX, badgeY, badgeW, badgeH), _texBadgeBg);

            // Đổi màu nhãn kiểu sút theo loại — phải làm trực tiếp không cache style riêng
            // vì màu thay đổi tuỳ loại, cache toàn bộ style sẽ cần 4 style → không đáng
            _styleShotType.normal.textColor = _shotTypeColor;
            GUI.Label(new Rect(badgeX + 8f*scale, badgeY + 4f*scale,
                               badgeW - 16f*scale, 28f*scale), _shotTypeLabel, _styleShotType);
            GUI.Label(new Rect(badgeX + 8f*scale, badgeY + 32f*scale,
                               badgeW - 16f*scale, 24f*scale), _cachedSpeedStr, _styleShotSpeed);
        }

        // ─── THANH THỜI ĐIỂM ─────────────────────────────────────────────────────
        private void DrawTimingBar(float scale)
        {
            float barH  = 28f * scale;
            float barW  = Screen.width * 0.80f;
            float barX  = (Screen.width - barW) * 0.5f;
            // Đáy màn hình, chừa margin để không che vùng vuốt sút
            float barY  = Screen.height - barH - 18f * scale;

            // Nền xám
            GUI.DrawTexture(new Rect(barX, barY, barW, barH), _texTimingBg);

            // Vùng GOOD (vàng): ±goodHalfWidth quanh perfectCenter
            float gL = Mathf.Clamp01(_timingPerfectCenter01 - _timingGoodHalf01);
            float gR = Mathf.Clamp01(_timingPerfectCenter01 + _timingGoodHalf01);
            GUI.DrawTexture(new Rect(barX + gL * barW, barY,
                                     (gR - gL) * barW, barH), _texTimingGood);

            // Vùng PERFECT (xanh lá): ±perfectHalfWidth quanh perfectCenter — vẽ đè lên GOOD
            float pL = Mathf.Clamp01(_timingPerfectCenter01 - _timingPerfectHalf01);
            float pR = Mathf.Clamp01(_timingPerfectCenter01 + _timingPerfectHalf01);
            GUI.DrawTexture(new Rect(barX + pL * barW, barY,
                                     (pR - pL) * barW, barH), _texTimingPerfect);

            // Con trỏ trắng: 4px vật lý rộng
            float cursorW = Mathf.Max(4f, 4f * scale);
            float cursorX = barX + Mathf.Clamp01(_timingProgress01) * barW - cursorW * 0.5f;
            GUI.DrawTexture(new Rect(cursorX, barY - 2f * scale,
                                     cursorW, barH + 4f * scale), _texTimingCursor);
        }

        // ─── NHÃN PERFECT / GOOD / POOR ──────────────────────────────────────────
        private void DrawGradeLabel(float scale)
        {
            // Rebuild chuỗi chỉ khi chưa có (bị xóa trong ShowTimingGrade)
            if (string.IsNullOrEmpty(_gradeLabelStr))
            {
                string gradeName;
                switch (_gradeValue)
                {
                    case TimingGrade.Perfect: gradeName = "PERFECT"; break;
                    case TimingGrade.Good:    gradeName = "GOOD";    break;
                    default:                  gradeName = "POOR";    break;
                }
                // Dấu có ký hiệu, tránh bỏ dấu; format tới ms
                string sign   = _gradeErrorMs >= 0f ? "+" : "-";
                float  absMs  = Mathf.Abs(_gradeErrorMs);
                _gradeLabelStr = string.Format("{0}  {1}{2:F0}ms", gradeName, sign, absMs);
            }

            // Màu theo mức
            switch (_gradeValue)
            {
                case TimingGrade.Perfect: _styleGradeLabel.normal.textColor = new Color(0.10f, 0.90f, 0.35f); break;
                case TimingGrade.Good:    _styleGradeLabel.normal.textColor = new Color(0.95f, 0.80f, 0.10f); break;
                default:                  _styleGradeLabel.normal.textColor = new Color(1.00f, 0.50f, 0.10f); break;
            }

            float lblW = Screen.width * 0.7f;
            float lblH = 80f * scale;
            float lblX = (Screen.width  - lblW) * 0.5f;
            float lblY = (Screen.height - lblH) * 0.5f - 60f * scale; // hơi lên trên giữa màn hình

            GUI.Label(new Rect(lblX, lblY, lblW, lblH), _gradeLabelStr, _styleGradeLabel);
        }

        // ─── NHẮC THAO TÁC ───────────────────────────────────────────────────────
        private void DrawPrompt(float scale)
        {
            float w = Screen.width * 0.70f;
            float h = 44f * scale;
            float x = (Screen.width - w) * 0.5f;
            // Giữa-dưới: cách đáy 120px*scale (trên thanh timing nếu có)
            float y = Screen.height - h - (18f + 28f + 14f) * scale;

            GUI.DrawTexture(new Rect(x, y, w, h), _texSemiBlack);
            GUI.Label(new Rect(x, y, w, h), _promptText, _stylePrompt);
        }

        // ─── BANNER KẾT QUẢ ──────────────────────────────────────────────────────
        private void DrawBanner(float scale)
        {
            float bannerW = Mathf.Min(680f * scale, Screen.width - 24f * scale);
            float bannerH = 200f * scale;
            float bannerX = (Screen.width  - bannerW) * 0.5f;
            float bannerY = Screen.height * 0.30f;

            GUI.DrawTexture(new Rect(bannerX, bannerY, bannerW, bannerH), _texDarkBg);

            // Màu tiêu đề theo context
            _styleBannerTitle.normal.textColor = _bannerColor;

            float innerX = bannerX + 16f * scale;
            float innerW = bannerW - 32f * scale;
            GUI.Label(new Rect(innerX, bannerY + 16f * scale, innerW, 50f * scale),
                      _bannerTitle, _styleBannerTitle);
            GUI.Label(new Rect(innerX, bannerY + 68f * scale, innerW, 32f * scale),
                      _bannerSubtitle, _styleBannerSub);

            // Nút hành động — đủ lớn cho ngón tay
            float btnH = Mathf.Max(44f * scale, 50f * scale);
            float btnW = 160f * scale;
            float btnY = bannerY + bannerH - btnH - 16f * scale;

            if (_replayAvailable)
            {
                float replayX = bannerX + bannerW * 0.5f - btnW - 8f * scale;
                if (GUI.Button(new Rect(replayX, btnY, btnW, btnH), "XEM LAI", _styleBannerBtn))
                    OnReplayClicked?.Invoke();
            }

            float nextX = _replayAvailable
                ? bannerX + bannerW * 0.5f + 8f * scale
                : (bannerX + bannerW - btnW) * 0.5f + bannerX * 0f; // căn giữa khi chỉ có 1 nút

            // Khi chỉ có nút "TIEP THEO", căn giữa trong banner
            if (!_replayAvailable)
                nextX = bannerX + (bannerW - btnW) * 0.5f;

            if (GUI.Button(new Rect(nextX, btnY, btnW, btnH), "LUOT TIEP THEO", _styleBannerBtn))
                OnNextKickClicked?.Invoke();
        }

        // ─── DEBUG THỦ MÔN ───────────────────────────────────────────────────────
        private void DrawKeeperDebug(float scale)
        {
            float w = Screen.width * 0.5f;
            float h = 24f * scale;
            float x = 8f  * scale;
            float y = Screen.height - h - 8f * scale;
            GUI.Label(new Rect(x, y, w, h), _keeperDebug, _styleDebug);
        }

        // ─── KHỞI TẠO STYLE (chỉ khi scale thay đổi) ────────────────────────────
        private void EnsureStyles(float scale)
        {
            // So sánh float scale dùng epsilon nhỏ để tránh rebuild liên tục do noise FP
            if (Mathf.Abs(scale - _cachedScale) < 0.001f) return;
            _cachedScale = scale;

            // Hàm tiện ích nội bộ: tạo GUIStyle kế thừa từ skin.label
            _styleScoreName = NewLabel(14, FontStyle.Bold, TextAnchor.MiddleLeft,
                                       new Color(0.95f, 0.95f, 0.50f), scale);

            _styleBoxScore  = NewLabel(20, FontStyle.Bold, TextAnchor.MiddleCenter,
                                       Color.black, scale);

            _styleShotType  = NewLabel(13, FontStyle.Bold, TextAnchor.MiddleLeft,
                                       Color.white, scale); // màu ghi đè khi vẽ

            _styleShotSpeed = NewLabel(12, FontStyle.Normal, TextAnchor.MiddleLeft,
                                       new Color(0.80f, 0.88f, 0.95f), scale);

            _styleBannerTitle = NewLabel(26, FontStyle.Bold, TextAnchor.UpperCenter,
                                         Color.white, scale); // màu ghi đè khi vẽ

            _styleBannerSub  = NewLabel(14, FontStyle.Normal, TextAnchor.UpperCenter,
                                         Color.white, scale);

            _styleBannerBtn  = BuildBtnStyle(16, scale, _texBtnNormal, _texBtnActive);

            _styleTurnBand   = NewLabel(13, FontStyle.Bold, TextAnchor.MiddleLeft,
                                         new Color(0.85f, 0.92f, 1.00f), scale);

            _styleGradeLabel = NewLabel(36, FontStyle.Bold, TextAnchor.MiddleCenter,
                                         Color.white, scale); // màu ghi đè khi vẽ

            _stylePrompt     = NewLabel(14, FontStyle.Normal, TextAnchor.MiddleCenter,
                                         new Color(0.90f, 0.95f, 1.00f), scale);

            _styleDiffBtn    = BuildBtnStyle(11, scale, _texBtnNormal, _texBtnNormal);
            _styleDiffBtnActive = BuildBtnStyle(11, scale, _texBtnActive, _texBtnActive);
            // Nút đang chọn: chữ đậm hơn
            _styleDiffBtnActive.fontStyle = FontStyle.Bold;
            _styleDiffBtnActive.normal.textColor = Color.white;

            _styleDebug      = NewLabel(10, FontStyle.Normal, TextAnchor.MiddleLeft,
                                         new Color(0.60f, 0.65f, 0.70f, 0.80f), scale);

            _styleScoreNum   = NewLabel(20, FontStyle.Bold, TextAnchor.MiddleCenter,
                                         Color.black, scale);
        }

        // ─── HELPER ──────────────────────────────────────────────────────────────

        private GUIStyle NewLabel(int baseFontSize, FontStyle fontStyle,
                                  TextAnchor anchor, Color color, float scale)
        {
            var s = new GUIStyle(GUI.skin.label)
            {
                fontSize  = Mathf.RoundToInt(baseFontSize * scale),
                fontStyle = fontStyle,
                alignment = anchor,
            };
            s.normal.textColor = color;
            return s;
        }

        private GUIStyle BuildBtnStyle(int baseFontSize, float scale,
                                       Texture2D normalTex, Texture2D activeTex)
        {
            var s = new GUIStyle(GUI.skin.button)
            {
                fontSize  = Mathf.RoundToInt(baseFontSize * scale),
                alignment = TextAnchor.MiddleCenter,
            };
            s.normal.background   = normalTex;
            s.hover.background    = activeTex;
            s.active.background   = activeTex;
            s.normal.textColor    = new Color(0.88f, 0.92f, 1.00f);
            s.hover.textColor     = Color.white;
            s.active.textColor    = Color.white;
            return s;
        }

        private static int CountScored(List<KickResult> list)
        {
            int n = 0;
            for (int i = 0; i < list.Count; i++)
                if (list[i] == KickResult.Scored) n++;
            return n;
        }

        /// <summary>Tạo texture 2×2 một màu; 2×2 đủ để GPU stretch.</summary>
        private static Texture2D MakeTex(Color col)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.SetPixels(new[] { col, col, col, col });
            tex.Apply();
            return tex;
        }

        private static void DestroyTex(ref Texture2D tex)
        {
            if (tex != null) { Destroy(tex); tex = null; }
        }
    }
}
