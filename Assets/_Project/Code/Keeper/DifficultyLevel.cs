namespace Eleven.Keeper
{
    /// <summary>
    /// Ba bậc độ khó của thủ môn (T25).
    ///
    /// Giá trị số được ghi CỨNG chứ không để trình biên dịch tự đánh số, vì hai lý do:
    /// bản lưu trận đấu (T24) lưu bậc độ khó dưới dạng byte, và tên asset được sinh ra
    /// từ chính tên hằng ("KeeperProfile-Easy.asset"). Chèn thêm một bậc vào giữa danh
    /// sách sẽ làm mọi bản lưu cũ đọc ra sai độ khó mà không có lỗi nào được ném ra.
    /// </summary>
    public enum DifficultyLevel : byte
    {
        /// <summary>Dễ — đọc vị kém, cam kết muộn, tầm với hẹp nhất trong ba bậc.</summary>
        Easy = 0,

        /// <summary>Thường — bậc mặc định, cũng là <see cref="KeeperProfile.CreateDefault"/>.</summary>
        Medium = 1,

        /// <summary>Khó — đọc vị tốt, cam kết sớm, nhớ thói quen người sút.</summary>
        Hard = 2
    }
}
