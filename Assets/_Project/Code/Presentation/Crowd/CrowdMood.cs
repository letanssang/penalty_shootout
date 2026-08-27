namespace Eleven.Presentation.Crowd
{
    /// <summary>
    /// Bốn trạng thái cảm xúc của khán đài. Giá trị enum CHÍNH LÀ chỉ số hàng trong atlas
    /// (xem <see cref="CrowdAtlas"/>) — đổi thứ tự là đổi luôn cách đọc texture, đừng đổi.
    ///
    /// Ánh xạ sang lượt sút (T23):
    ///   Placing              → Hushed        (im khi đặt bóng)
    ///   Aiming/RunUp/Contact/Flight → Anticipation
    ///   Resolution/Reaction  → Celebrate nếu bóng vào lưới, Despair nếu hỏng
    ///   Complete             → Hushed
    /// </summary>
    public enum CrowdMood : byte
    {
        /// <summary>Im lặng, ngồi yên — lúc đặt bóng lên chấm 11m.</summary>
        Hushed = 0,
        /// <summary>Nhấp nhổm chờ đợi — từ lúc ngắm tới lúc bóng đang bay.</summary>
        Anticipation = 1,
        /// <summary>Nhảy lên ăn mừng — bóng vào lưới.</summary>
        Celebrate = 2,
        /// <summary>Gục xuống ôm đầu — sút hỏng hoặc bị cản.</summary>
        Despair = 3
    }
}
