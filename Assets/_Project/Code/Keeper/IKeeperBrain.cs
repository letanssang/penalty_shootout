namespace Eleven.Keeper
{
    /// <summary>
    /// Giao diện suy luận đọc vị của thủ môn (T18).
    /// Nhận tín hiệu thị giác (KeeperCues), lịch sử (ShotHistory),
    /// hồ sơ năng lực (KeeperProfile) và seed ngẫu nhiên tất định,
    /// trả về phân phối xác suất trên 9 ô (KeeperRead).
    ///
    /// Mọi cài đặt phải tất định: cùng input cho cùng output từng bit.
    /// </summary>
    public interface IKeeperBrain
    {
        KeeperRead Infer(in KeeperCues cues, in ShotHistory history,
                         KeeperProfile profile, uint seed);
    }
}
