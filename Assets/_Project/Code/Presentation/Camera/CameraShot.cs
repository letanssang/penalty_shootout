namespace Eleven.Presentation
{
    /// <summary>
    /// Các góc quay máy ảnh được tác giả dựng và hỗ trợ trong trải nghiệm sút luân lưu.
    /// </summary>
    public enum CameraShot
    {
        /// <summary>Góc truyền hình truyền thống từ trên cao một bên khán đài.</summary>
        Broadcast,

        /// <summary>Góc nhìn phía sau người sút — góc quay mặc định cho pha ngắm và sút.</summary>
        BehindShooter,

        /// <summary>Góc nhìn từ tầm mắt thủ môn nhìn ra hướng chấm phạt đền.</summary>
        KeeperPOV,

        /// <summary>Góc máy thấp sát mặt cỏ tạo kịch tính trong pha bóng bay.</summary>
        LowAngle,

        /// <summary>Góc máy gắn trong lưới khung thành nhìn ra.</summary>
        NetCam,

        /// <summary>Góc máy xoay quỹ đạo cho pha phát lại (Replay), có giới hạn góc cứng.</summary>
        ReplayOrbit,

        /// <summary>
        /// Cận mặt người sút ở pha phản ứng — máy đứng trước mặt, hơi chếch, nhìn ngược lại.
        /// Khác mọi góc trên ở chỗ nó KHÔNG cố định trong không gian: vị trí tính từ đầu
        /// người sút tại đúng khoảnh khắc cắt cảnh, xem <c>CameraRig.SetKickerFace</c>.
        /// </summary>
        KickerFace
    }
}
