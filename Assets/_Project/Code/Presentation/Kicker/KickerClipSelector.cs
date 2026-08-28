using Eleven.Match;
using Eleven.Shooter;

namespace Eleven.Presentation.Kicker
{
    /// <summary>
    /// Chọn clip. Thuần logic: không Animator, không Transform, không Time.deltaTime —
    /// nên kiểm thử được bằng EditMode mà không cần dựng scene, và chạy được ở nơi
    /// chưa có tài sản nghệ thuật nào.
    ///
    /// <see cref="Resolve"/> là hàm TOÀN PHẦN trên <see cref="KickPhase"/>: mọi pha, kể cả
    /// pha rơi vào giữa chừng sau <c>KickSequencer.Abort()</c>, đều cho ra đúng một clip.
    /// Đó là cách bảo đảm "huỷ giữa chừng không để lại tư thế kẹt" bằng CẤU TRÚC chứ không
    /// bằng một danh sách lệnh dọn dẹp mà người sau dễ quên cập nhật.
    /// </summary>
    public struct KickerClipSelector
    {
        ShotType _pending;
        KickResult _outcome;
        bool _strikeLocked;

        /// <summary>Kiểu sút đã báo trước. Mặc định Instep — trùng mặc định của <c>ShotMapper</c>.</summary>
        public ShotType PendingShot => _pending;

        public KickResult Outcome => _outcome;

        /// <summary>
        /// Báo trước kiểu sút. KHÔNG có tác dụng khi chân đã bắt đầu vung.
        ///
        /// Đây không phải sự cẩn thận thừa. Với lượt của NGƯỜI CHƠI, kiểu sút chỉ ngã ngũ lúc
        /// nhả ngón tay — tức ở pha Contact, sau khi clip sút đã chạy được vài phần mười giây.
        /// Không chốt lại thì cú vung đang giữa chừng bị cắt và phát lại từ đầu ngay lúc bóng
        /// rời chân. Một cú vung đã bắt đầu thì ngoài đời cũng không đổi được nữa.
        ///
        /// Hệ quả cần biết: lượt của người chơi hiện dùng clip của <see cref="ShotType.Instep"/>
        /// (mặc định) vì cử chỉ chưa được phân loại lúc bắt đầu chạy đà. Muốn đúng clip thì
        /// phải phân loại tạm cử chỉ đang vuốt — việc riêng, không thuộc T35.
        /// </summary>
        public void PrepareFor(ShotType type)
        {
            if (_strikeLocked) return;
            _pending = type;
        }

        /// <summary>Gọi khi clip sút thật sự bắt đầu phát.</summary>
        public void LockStrike() => _strikeLocked = true;

        public void SetOutcome(KickResult result) => _outcome = result;

        /// <summary>Về trạng thái đầu lượt. Gọi khi <c>BeginKick</c> hoặc <c>Abort</c>.</summary>
        public void Reset()
        {
            _pending = ShotType.Instep;
            _outcome = KickResult.Pending;
            _strikeLocked = false;
        }

        /// <summary>
        /// Bốn kiểu sút ra bốn clip khác nhau. Ánh xạ này là TOÀN ÁNH có chủ đích: nếu
        /// mai kia thêm ShotType mà quên bổ sung ở đây thì rơi vào Instep — sai thầm lặng.
        /// Vì vậy nhánh mặc định để riêng và có kiểm thử đếm đúng bốn giá trị phân biệt.
        /// </summary>
        public static KickerClip StrikeFor(ShotType type)
        {
            switch (type)
            {
                case ShotType.Instep:     return KickerClip.StrikeInstep;
                case ShotType.InsideFoot: return KickerClip.StrikeInsideFoot;
                case ShotType.Chip:       return KickerClip.StrikeChip;
                case ShotType.Knuckle:    return KickerClip.StrikeKnuckle;
                default:                  return KickerClip.StrikeInstep;
            }
        }

        /// <summary>
        /// Clip đúng cho pha hiện tại.
        ///
        /// <paramref name="runUpSecondsRemaining"/> và <paramref name="strikeLeadSeconds"/>
        /// giải quyết chuyện tế nhị nhất của T35: khung chạm bóng nằm GIỮA clip sút, không
        /// phải ở đầu. PenaltyKick dài 1.500 s và chạm ở tỉ lệ 0.4889, tức 0.733 s sau khi
        /// clip bắt đầu. Nếu đợi tới pha Contact mới bật clip thì bàn chân chạm bóng muộn
        /// 0.733 s so với lúc gameplay đã bắn bóng đi — người xem thấy bóng bay trước rồi
        /// chân mới vung. Nên clip sút phải khởi động TRONG pha RunUp, sớm đúng bằng
        /// <paramref name="strikeLeadSeconds"/> = ContactNormalizedTime × độ dài clip.
        ///
        /// Việc này KHÔNG vi phạm định luật "hoạt ảnh không lái vật lý": gameplay vẫn tự
        /// định đoạt thời điểm chạm, hoạt ảnh chỉ căn theo lịch đã biết trước của gameplay.
        /// </summary>
        public KickerClip Resolve(KickPhase phase, float runUpSecondsRemaining, float strikeLeadSeconds)
        {
            switch (phase)
            {
                case KickPhase.Placing:
                case KickPhase.Aiming:
                    return KickerClip.Idle;

                case KickPhase.RunUp:
                    return runUpSecondsRemaining <= strikeLeadSeconds
                        ? StrikeFor(_pending)
                        : KickerClip.RunUp;

                case KickPhase.Contact:
                case KickPhase.Flight:
                    // Clip sút tự nó đã chứa phần vung chân sau khi chạm; cắt sang
                    // FollowThrough ngay lúc này sẽ chặt cụt cú vung.
                    return StrikeFor(_pending);

                case KickPhase.Resolution:
                    return KickerClip.FollowThrough;

                case KickPhase.Reaction:
                    // Pending nghĩa là chưa ai báo kết quả. Đứng thở còn hơn ăn mừng nhầm.
                    if (_outcome == KickResult.Scored) return KickerClip.Celebrate;
                    if (_outcome == KickResult.Missed) return KickerClip.Dejected;
                    return KickerClip.FollowThrough;

                case KickPhase.Complete:
                default:
                    return KickerClip.Idle;
            }
        }
    }
}
