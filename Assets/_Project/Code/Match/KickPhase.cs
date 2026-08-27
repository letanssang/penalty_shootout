namespace Eleven.Match
{
    /// <summary>
    /// Tám pha của MỘT lượt sút (T23). Thứ tự là tuyến tính tuyệt đối và không bao giờ
    /// được nhảy cóc:
    ///   Placing → Aiming → RunUp → Contact → Flight → Resolution → Reaction → Complete
    ///
    /// <see cref="Complete"/> vừa là điểm kết thúc vừa là trạng thái NGHỈ: một sequencer
    /// vừa khởi tạo cũng đang ở Complete. Chọn như vậy để chỉ có đúng một điều kiện hợp lệ
    /// cho <c>BeginKick</c> ("chỉ bắt đầu được khi đang ở Complete"), thay vì phải thêm một
    /// giá trị Idle riêng rồi ở đâu cũng phải xét hai trường hợp.
    ///
    /// Kiểu nền là <c>byte</c> vì giá trị này được ghi vào bản lưu
    /// (<see cref="KickSequencerSnapshot.phase"/>) — cố định 1 byte, không phụ thuộc nền tảng.
    /// </summary>
    public enum KickPhase : byte
    {
        /// <summary>Đặt bóng lên chấm 11m.</summary>
        Placing = 0,
        /// <summary>Người chơi đang ngắm. Kết thúc bằng <c>ConfirmAim()</c> hoặc hết giờ.</summary>
        Aiming = 1,
        /// <summary>Chạy đà — đây là lúc thủ môn đọc tín hiệu (T17).</summary>
        RunUp = 2,
        /// <summary>Khoảnh khắc chân chạm bóng. Rất ngắn, cố ý tách riêng để gắn hiệu ứng.</summary>
        Contact = 3,
        /// <summary>Bóng đang bay. Vật lý chạy ở đây.</summary>
        Flight = 4,
        /// <summary>Phân giải kết quả: vào lưới, ra ngoài, hay bị cản.</summary>
        Resolution = 5,
        /// <summary>Ăn mừng / tiếc nuối. Thuần trình diễn.</summary>
        Reaction = 6,
        /// <summary>Xong lượt — cũng là trạng thái nghỉ khi chưa có lượt nào.</summary>
        Complete = 7
    }

    /// <summary>
    /// Thời lượng (giây) của từng pha. Là <c>struct</c> và truyền theo giá trị để phần
    /// logic không bao giờ cấp phát; muốn đổi nhịp trận đấu thì gán cả cụm chứ không
    /// sửa từng trường trên một đối tượng dùng chung.
    ///
    /// <see cref="KickPhase.Complete"/> không có thời lượng: nó là điểm dừng, không tự trôi
    /// sang đâu cả — nên <see cref="For"/> trả 0 cho nó.
    /// </summary>
    public struct KickPhaseDurations
    {
        public float placing;
        public float aiming;
        public float runUp;
        public float contact;
        public float flight;
        public float resolution;
        public float reaction;

        /// <summary>
        /// Nhịp mặc định. <c>aiming = 3.00</c> là hạn giờ ngắm — hết giờ mà người chơi
        /// chưa quyết thì lượt vẫn phải chạy tiếp, không được treo.
        /// </summary>
        public static KickPhaseDurations Default
        {
            get
            {
                KickPhaseDurations durations;
                durations.placing = 0.80f;
                durations.aiming = 3.00f;
                durations.runUp = 0.90f;
                durations.contact = 0.05f;
                durations.flight = 1.20f;
                durations.resolution = 0.60f;
                durations.reaction = 1.50f;
                return durations;
            }
        }

        /// <summary>Thời lượng của <paramref name="phase"/>; 0 nghĩa là "đi tiếp ngay".</summary>
        public float For(KickPhase phase)
        {
            switch (phase)
            {
                case KickPhase.Placing:
                    return placing;
                case KickPhase.Aiming:
                    return aiming;
                case KickPhase.RunUp:
                    return runUp;
                case KickPhase.Contact:
                    return contact;
                case KickPhase.Flight:
                    return flight;
                case KickPhase.Resolution:
                    return resolution;
                case KickPhase.Reaction:
                    return reaction;
                default:
                    return 0f;
            }
        }
    }
}
