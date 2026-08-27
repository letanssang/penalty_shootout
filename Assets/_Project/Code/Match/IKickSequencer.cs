namespace Eleven.Match
{
    /// <summary>
    /// Hợp đồng tối thiểu của máy trạng thái lượt sút (T23) — đúng bằng những gì
    /// tầng trình diễn cần biết. Các phần còn lại (Tick, Capture/Restore, SetIntent…)
    /// nằm trên lớp cài đặt <see cref="KickSequencer"/>, cố ý KHÔNG đưa vào đây để
    /// UI không có cách nào tự ý tua thời gian hay ghi đè kết quả.
    /// </summary>
    public interface IKickSequencer
    {
        KickPhase Phase { get; }

        /// <summary>
        /// Bắn mỗi lần đổi pha, tham số là (pha cũ, pha mới). Khi handler chạy thì
        /// trạng thái của sequencer ĐÃ được cập nhật xong — đọc <see cref="Phase"/>
        /// trong handler luôn thấy pha mới.
        /// </summary>
        event System.Action<KickPhase, KickPhase> OnPhaseChanged;

        /// <summary>Bắt đầu một lượt với <paramref name="seed"/> tất định. Bỏ qua nếu đang giữa lượt.</summary>
        void BeginKick(uint seed);

        /// <summary>Huỷ lượt đang chạy, về thẳng <see cref="KickPhase.Complete"/>.</summary>
        void Abort();
    }

    /// <summary>
    /// Ảnh chụp trạng thái lượt sút để ghi vào bản lưu (ghép với T24 MatchSave).
    /// Toàn bộ là kiểu blittable (byte/uint/float/struct) — không chuỗi, không tham chiếu —
    /// nên ghi nhị phân được và không tốn GC.
    ///
    /// <c>phase</c> và <c>outcome</c> để dạng <c>byte</c> chứ không phải enum: file lưu có thể
    /// bị sửa tay hoặc hỏng, và <see cref="KickSequencer.Restore"/> phải chịu được giá trị
    /// ngoài dải mà không ném exception. Ép sang enum ngay tại đây sẽ giấu mất chuyện đó.
    /// </summary>
    public struct KickSequencerSnapshot
    {
        public byte phase;
        public uint seed;
        public float phaseElapsed;
        public byte outcome;
        public bool hasIntent;
        public Eleven.Shooter.ShotIntent intent;
    }
}
