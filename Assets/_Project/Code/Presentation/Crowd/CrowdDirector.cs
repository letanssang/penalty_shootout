using Unity.Mathematics;
using Eleven.Core;
using Eleven.Match;

namespace Eleven.Presentation.Crowd
{
    /// <summary>
    /// Điều phối khán đài: sinh ghế một lần lúc khởi tạo, đổi cảm xúc theo pha lượt sút và
    /// kết quả cú sút, rồi mỗi khung hình trả về ĐÚNG MỘT batch để vẽ.
    ///
    /// Ràng buộc đã cố tình đưa vào kiểu dữ liệu chứ không để trong ghi chú:
    ///   - một atlas  → <see cref="CrowdAtlas"/> là static, không có đường nào truyền texture khác vào;
    ///   - một draw call → <see cref="BuildBatch"/> trả về một struct, không phải danh sách;
    ///   - không cấp phát mỗi khung hình → mọi mảng cấp phát trong hàm dựng, <see cref="Tick"/>
    ///     chỉ cộng dồn một số thực.
    /// </summary>
    public sealed class CrowdDirector
    {
        private readonly CrowdInstance[] _instances;
        private readonly CrowdInstanceGpu[] _gpuInstances;
        private readonly int _instanceCount;

        private CrowdTierSettings _settings;
        private CrowdMood _mood;
        private float _time;

        /// <summary>Kết quả cú sút gần nhất, chỉ có nghĩa khi <see cref="_hasOutcome"/> = true.</summary>
        private ShotOutcome _lastOutcome;
        private bool _hasOutcome;

        public int InstanceCount => _instanceCount;
        public CrowdMood Mood => _mood;
        public float AnimationTime => _time;
        public bool IsAnimated => _settings.animated;
        public QualityTier Tier => _settings.tier;
        public float MaxGpuBudgetMs => _settings.maxGpuBudgetMs;

        /// <summary>Mảng ghế, chỉ đọc theo quy ước — trả thẳng để test và code vẽ không phải sao chép.</summary>
        public CrowdInstance[] Instances => _instances;

        /// <summary>Bản 48 byte/instance đã đóng gói, sẵn sàng nạp vào <c>GraphicsBuffer</c>.</summary>
        public CrowdInstanceGpu[] GpuInstances => _gpuInstances;

        public CrowdDirector(QualityTier tier, uint seed = 0x5EA75EA7u,
                             int seatCapacity = CrowdStandLayout.TotalSeats)
        {
            _settings = CrowdTierSettings.ForTier(tier);

            if (seatCapacity < 1) seatCapacity = 1;
            _instances = new CrowdInstance[seatCapacity];
            _instanceCount = CrowdStandLayout.Generate(_instances, seed);

            _gpuInstances = new CrowdInstanceGpu[_instanceCount];
            for (int i = 0; i < _instanceCount; i++)
            {
                _gpuInstances[i] = CrowdInstanceExtensions.ToGpu(in _instances[i]);
            }

            _mood = CrowdMood.Hushed;
            _time = 0f;
        }

        /// <summary>
        /// Đổi bậc chất lượng lúc đang chạy (người chơi hạ cấu hình, hoặc máy nóng lên).
        /// Không sinh lại ghế: cùng seed, cùng đám đông — chỉ animation bị đóng băng hay bật lại.
        /// </summary>
        public void ApplyTier(QualityTier tier)
        {
            _settings = CrowdTierSettings.ForTier(tier);
        }

        /// <summary>
        /// Cảm xúc ứng với một pha lượt sút. Hàm thuần, không trạng thái — test kiểm được
        /// toàn bộ bảng ánh xạ mà không cần dựng director.
        /// </summary>
        public static CrowdMood MoodFor(KickPhase phase, ShotOutcome outcome, bool hasOutcome)
        {
            switch (phase)
            {
                case KickPhase.Placing:
                case KickPhase.Complete:
                    return CrowdMood.Hushed;

                case KickPhase.Aiming:
                case KickPhase.RunUp:
                case KickPhase.Contact:
                case KickPhase.Flight:
                    return CrowdMood.Anticipation;

                case KickPhase.Resolution:
                case KickPhase.Reaction:
                    if (!hasOutcome)
                    {
                        // Chưa biết kết quả thì vẫn nín thở, tuyệt đối không đoán trước —
                        // khán đài ăn mừng sớm nửa giây là lỗi ai cũng thấy.
                        return CrowdMood.Anticipation;
                    }
                    return IsCelebration(outcome) ? CrowdMood.Celebrate : CrowdMood.Despair;

                default:
                    return CrowdMood.Hushed;
            }
        }

        /// <summary>Bóng có vào lưới không. <see cref="ShotOutcome.PostIn"/> cũng là bàn thắng.</summary>
        public static bool IsCelebration(ShotOutcome outcome)
        {
            return outcome == ShotOutcome.Goal || outcome == ShotOutcome.PostIn;
        }

        /// <summary>Gắn vào sự kiện đổi pha của <c>IKickSequencer</c> (T23).</summary>
        public void OnKickPhaseChanged(KickPhase phase)
        {
            if (phase == KickPhase.Placing)
            {
                // Lượt mới: quên kết quả lượt trước, nếu không khán đài sẽ gục sẵn từ lúc đặt bóng.
                _hasOutcome = false;
            }

            _mood = MoodFor(phase, _lastOutcome, _hasOutcome);
        }

        /// <summary>Gắn vào lúc T10/T21 chốt kết quả cú sút.</summary>
        public void OnOutcomeResolved(ShotOutcome outcome)
        {
            _lastOutcome = outcome;
            _hasOutcome = true;
            _mood = IsCelebration(outcome) ? CrowdMood.Celebrate : CrowdMood.Despair;
        }

        /// <summary>Đặt thẳng cảm xúc — dùng cho cut-scene và test.</summary>
        public void SetMood(CrowdMood mood)
        {
            _mood = mood;
        }

        /// <summary>
        /// Nhích đồng hồ animation. Đây là toàn bộ phần chạy mỗi khung hình của CPU: một phép
        /// cộng. Chỉ số khung hình của từng instance được tính trong shader từ pha riêng của nó,
        /// CPU không đụng vào mảng instance nữa sau lúc khởi tạo.
        /// </summary>
        public void Tick(float dt)
        {
            if (!_settings.animated || dt <= 0f)
            {
                return;
            }

            _time += dt;

            // Bọc vòng theo chu kỳ animation để _time không lớn dần vô hạn: sau vài giờ chơi,
            // float32 mất hết phần lẻ và animation giật từng nấc.
            float period = CycleSeconds;
            if (period > 0f && _time >= period)
            {
                _time -= math.floor(_time / period) * period;
            }
        }

        /// <summary>Độ dài một vòng animation (giây).</summary>
        public float CycleSeconds =>
            _settings.animationFps > 0f ? CrowdAtlas.FramesPerMood / _settings.animationFps : 0f;

        /// <summary>
        /// Khung hình mà instance thứ <paramref name="index"/> đang hiển thị.
        /// Cùng công thức với shader — test EditMode nhờ vậy kiểm được đúng thứ GPU sẽ tính.
        /// </summary>
        public int GetFrame(int index)
        {
            if (index < 0 || index >= _instanceCount)
            {
                return 0;
            }

            if (!_settings.animated)
            {
                return 0;   // bậc C: khán giả đứng yên ở khung 0
            }

            ref readonly CrowdInstance inst = ref _instances[index];

            float frames = _time * _settings.animationFps * inst.speedScale
                           + inst.phase01 * CrowdAtlas.FramesPerMood;

            int frame = (int)math.floor(frames) % CrowdAtlas.FramesPerMood;
            if (frame < 0) frame += CrowdAtlas.FramesPerMood;
            return frame;
        }

        /// <summary>Ô UV mà instance thứ <paramref name="index"/> đang lấy hình.</summary>
        public float4 GetCellUv(int index)
        {
            return CrowdAtlas.GetCellUv(_mood, GetFrame(index));
        }

        /// <summary>
        /// Dựng mô tả lần vẽ. Trả về struct theo giá trị — không cấp phát, gọi mỗi khung hình
        /// được.
        /// </summary>
        public CrowdRenderBatch BuildBatch()
        {
            return new CrowdRenderBatch
            {
                instanceCount = _instanceCount,
                atlasId = 0,
                drawCallCount = CrowdBudget.MaxDrawCalls,
                mood = _mood,
                animated = _settings.animated
            };
        }
    }
}
