namespace Eleven.Keeper
{
    /// <summary>
    /// Chọn hồ sơ thủ môn theo bậc độ khó, và — quan trọng hơn — quyết định THỜI ĐIỂM
    /// bậc mới có hiệu lực (T25).
    ///
    /// Người chơi có thể mở menu đổi độ khó bất cứ lúc nào, kể cả khi bóng đang bay.
    /// Nếu áp ngay lập tức thì thủ môn đang bay người theo tính toán của hồ sơ cũ sẽ
    /// đột ngột dùng reachScale của hồ sơ mới ở giữa cú sút — quả đó không còn tất định
    /// (phát lại từ cùng seed ra kết quả khác) và bản lưu T24 ghi lại một lượt không thể
    /// tái dựng. Vì vậy yêu cầu được XẾP HÀNG ở <see cref="Pending"/>, và chỉ chuyển sang
    /// <see cref="Current"/> khi <see cref="CommitPending"/> được gọi lúc mở lượt sút mới
    /// (điểm gọi đúng: khi KickSequencer vào pha Placing).
    ///
    /// Thuần C#: không MonoBehaviour, không cấp phát sau khi dựng, tất định.
    /// </summary>
    public sealed class DifficultySelector
    {
        readonly KeeperProfile _easy;
        readonly KeeperProfile _medium;
        readonly KeeperProfile _hard;

        DifficultyLevel _current;
        DifficultyLevel _pending;

        /// <summary>
        /// Dựng từ asset đã gán trong scene. Slot nào null thì thay bằng hằng số trong code:
        /// một profile null không ném lỗi ở bất kỳ đâu trong chuỗi T18–T19 (cả brain lẫn
        /// controller đều có nhánh mặc định cho null), nên biểu hiện của việc quên gán asset
        /// sẽ là "thủ môn ở bậc Khó chơi y như bậc Dễ" — một lỗi im lặng, rất khó truy.
        /// </summary>
        public DifficultySelector(KeeperProfile easy, KeeperProfile medium, KeeperProfile hard,
                                  DifficultyLevel start = DifficultyLevel.Medium)
        {
            _easy = easy != null ? easy : KeeperProfile.CreateEasy();
            _medium = medium != null ? medium : KeeperProfile.CreateMedium();
            _hard = hard != null ? hard : KeeperProfile.CreateHard();
            _current = start;
            _pending = start;
        }

        /// <summary>Dựng từ hằng số trong code — dùng cho test và cho chế độ chạy không cần asset.</summary>
        public DifficultySelector(DifficultyLevel start = DifficultyLevel.Medium)
            : this(null, null, null, start)
        {
        }

        /// <summary>Bậc đang có hiệu lực cho lượt sút hiện tại.</summary>
        public DifficultyLevel Current
        {
            get { return _current; }
        }

        /// <summary>Bậc sẽ có hiệu lực từ lượt sút kế tiếp. Bằng <see cref="Current"/> khi không có yêu cầu nào đang chờ.</summary>
        public DifficultyLevel Pending
        {
            get { return _pending; }
        }

        /// <summary>Hồ sơ ứng với <see cref="Current"/>. Không bao giờ null.</summary>
        public KeeperProfile ActiveProfile
        {
            get { return ProfileFor(_current); }
        }

        /// <summary>
        /// Hồ sơ của một bậc bất kỳ, không đổi trạng thái — màn hình chọn độ khó cần đọc
        /// thông số của bậc đang trỏ tới trước khi người chơi xác nhận.
        /// Bậc ngoài dải trả về hồ sơ Thường thay vì ném lỗi (giá trị đọc từ file lưu hỏng).
        /// </summary>
        public KeeperProfile ProfileFor(DifficultyLevel level)
        {
            switch (level)
            {
                case DifficultyLevel.Easy:
                    return _easy;
                case DifficultyLevel.Hard:
                    return _hard;
                default:
                    return _medium;
            }
        }

        /// <summary>
        /// Xếp hàng đổi sang <paramref name="level"/>. Không đụng tới lượt đang chạy.
        /// Gọi nhiều lần trước khi commit thì chỉ lần cuối có tác dụng.
        /// </summary>
        public void Request(DifficultyLevel level)
        {
            _pending = level;
        }

        /// <summary>
        /// Áp bậc đang chờ. Gọi ở đầu mỗi lượt sút, TRƯỚC khi thủ môn đọc cue lần đầu.
        /// Trả về true nếu bậc thực sự đổi — nơi gọi dùng để ghi log/telemetry lượt nào
        /// đổi độ khó, vì tỉ lệ cản phá của loạt sút chỉ đọc được nếu biết bậc từng quả.
        /// </summary>
        public bool CommitPending()
        {
            if (_pending == _current)
                return false;

            _current = _pending;
            return true;
        }
    }
}
