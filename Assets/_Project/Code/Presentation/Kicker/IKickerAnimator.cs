using Eleven.Match;
using Eleven.Shooter;
using UnityEngine;

namespace Eleven.Presentation.Kicker
{
    /// <summary>
    /// Lớp hoạt ảnh của người sút. Định luật Phase 7: hoạt ảnh KHÔNG BAO GIỜ lái vật lý.
    /// Vận tốc rời chân do gameplay tính tại thời điểm chạm; interface này chỉ NHẬN
    /// <see cref="ShotType"/> qua <see cref="PrepareFor"/> chứ không bao giờ tự quyết.
    ///
    /// Không có phương thức nào ở đây được phép ghi vào BallDriver/BallState/ShotIntent —
    /// <c>KickerAnimatorHygieneTests</c> canh điều đó bằng cách soi IL.
    /// </summary>
    public interface IKickerAnimator
    {
        /// <summary>Clip đang phát ngay lúc này.</summary>
        KickerClip CurrentClip { get; }

        /// <summary>Vị trí trong clip hiện tại, 0..1. Quá 1 nghĩa là clip không lặp đã chạy hết.</summary>
        float NormalizedTime { get; }

        /// <summary>
        /// Thời điểm bàn chân chạm bóng trong clip sút hiện hành, tính theo tỉ lệ 0..1 của clip đó.
        /// Đo bằng <c>Eleven/Art/Probe Strike Contact</c>, số nằm ở docs/data/strike-contact.tsv.
        /// Trả 0 khi clip hiện hành không phải clip sút.
        /// </summary>
        float ContactNormalizedTime { get; }

        /// <summary>Báo trước kiểu sút để chọn clip. Gọi TRƯỚC khi vào pha RunUp.</summary>
        void PrepareFor(ShotType type);

        /// <summary>
        /// Báo kết quả để chọn giữa <see cref="KickerClip.Celebrate"/> và
        /// <see cref="KickerClip.Dejected"/> ở pha Reaction.
        ///
        /// BỔ SUNG so với đặc tả T35 gốc: đặc tả liệt kê cả hai clip cảm xúc nhưng không cho
        /// lớp này đường nào biết bàn thắng hay không. Không có nó thì hai giá trị enum kia
        /// vĩnh viễn không với tới được.
        /// </summary>
        void SetOutcome(KickResult result);

        /// <summary>Đổi pha. Gọi từ <c>KickSequencer.OnPhaseChanged</c>.</summary>
        void OnPhaseChanged(KickPhase oldPhase, KickPhase newPhase);

        /// <summary>Cập nhật mỗi khung. <paramref name="phaseProgress01"/> là tiến độ pha hiện tại.</summary>
        void Tick(float dt, float phaseProgress01);

        /// <summary>Xoay người theo hướng ngắm (độ, quanh trục Y).</summary>
        void SetAimYawDegrees(float yawDegrees);

        /// <summary>Về vạch xuất phát đầu lượt.</summary>
        void ResetToStart(Unity.Mathematics.float3 ballPosition);

        /// <summary>Đồng bộ với nhịp trận đang chạy — quyết định lúc nào bật clip sút.</summary>
        void SetRunUpDuration(float seconds);

        Transform Root { get; }
        Transform Hips { get; }

        /// <summary>Chân trụ — chân KHÔNG sút. <c>KickerBoneCueSource</c> đọc nó mỗi khung.</summary>
        Transform PlantFoot { get; }

        /// <summary>Chân sút.</summary>
        Transform KickFoot { get; }
    }
}
