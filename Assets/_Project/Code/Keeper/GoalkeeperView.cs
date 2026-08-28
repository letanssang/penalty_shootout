using Unity.Mathematics;
using UnityEngine;
using Eleven.Ball;

namespace Eleven.Keeper
{
    /// <summary>
    /// Thân xác của thủ môn trong scene. Lớp này KHÔNG tự nghĩ ra hành vi nào — nó chỉ
    /// hiển thị kết quả của bộ não (T18), máy trạng thái cam kết (T19), vùng với tới (T16)
    /// và bộ phân giải cản phá (T21).
    ///
    /// Bất biến quan trọng: đường bay người nhìn thấy trên màn hình dùng ĐÚNG công thức
    /// <see cref="KeeperReach.ReachProgress"/> mà <see cref="SaveResolver"/> dùng để phán
    /// kết quả. Nếu vẽ bằng một công thức khác, sẽ có những pha mắt thấy tay chạm bóng mà
    /// máy báo thủng lưới — thứ khiến người chơi gọi game là "ăn gian".
    /// </summary>
    public sealed class GoalkeeperView : MonoBehaviour
    {
        [Header("Vị trí")]
        [SerializeField] private Vector3 homePosition = new Vector3(0f, 0.95f, 11.0f);

        [Header("Găng tay (tuỳ chọn — chỉ để hiển thị)")]
        [SerializeField] private Transform leftGlove;
        [SerializeField] private Transform rightGlove;

        private readonly SimpleKeeperController _controller = new SimpleKeeperController();
        private readonly BayesianKeeperBrain _brain = new BayesianKeeperBrain();
        private ShotHistory _history;

        private KeeperProfile _profile;    // hồ sơ độ khó đang chọn (asset, không được sửa)
        private KeeperProfile _effective;  // bản sao runtime, có commitOffset THẬT của lượt này

        private DiveDecision _decision;
        private bool _committed;
        private float _confidence;
        private int _bestCell = 4;

        private bool _diving;
        private float _timeSinceContact;
        private Vector3 _homeGloveL, _homeGloveR;

        public KeeperPhase Phase => _controller.Phase;
        public DiveDecision Decision => _decision;
        public bool HasCommitted => _committed;
        public float Confidence => _confidence;
        public int PredictedCell => _bestCell;

        /// <summary>Hồ sơ dùng để phân giải — đã gắn thời điểm cam kết thật của lượt hiện tại.</summary>
        public KeeperProfile EffectiveProfile => _effective != null ? _effective : _profile;

        private void Awake()
        {
            _history = default;
            if (_profile == null) _profile = KeeperProfile.CreateMedium();

            _effective = ScriptableObject.CreateInstance<KeeperProfile>();
            CopyProfile(_profile, _effective);

            if (leftGlove != null) _homeGloveL = leftGlove.localPosition;
            if (rightGlove != null) _homeGloveR = rightGlove.localPosition;

            ResetToHome();
        }

        private void OnDestroy()
        {
            if (_effective != null) Destroy(_effective);
        }

        public void SetProfile(KeeperProfile p)
        {
            if (p == null) return;
            _profile = p;
            if (_effective != null) CopyProfile(_profile, _effective);
        }

        /// <summary>Xoá trí nhớ thói quen — gọi khi bắt đầu một trận mới, KHÔNG gọi giữa trận.</summary>
        public void ClearMemory() => _history.Clear();

        /// <summary>Nạp một cú sút vào trí nhớ (T20). Ô nào bị sút nhiều thì lần sau bị đọc vị dễ hơn.</summary>
        public void RememberShot(int cell) => _history.Record(cell);

        public void ResetToHome()
        {
            _controller.Reset();
            _committed = false;
            _diving = false;
            _confidence = 0f;
            _bestCell = 4;
            _decision = default;
            _timeSinceContact = 0f;

            transform.position = homePosition;
            transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            if (leftGlove != null) leftGlove.localPosition = _homeGloveL;
            if (rightGlove != null) rightGlove.localPosition = _homeGloveR;

            if (_effective != null) CopyProfile(_profile, _effective);
        }

        /// <summary>
        /// Một khung hình ĐỌC VỊ trong lúc người sút chạy đà. Trả về true đúng khung hình
        /// thủ môn chốt quyết định.
        /// </summary>
        public bool TickRead(in KeeperCues cues, float timeToContact, uint seed)
        {
            if (_committed) return false;

            KeeperRead read = _brain.Infer(cues, _history, _profile, seed);
            _confidence = read.confidence;
            _bestCell = read.bestCell;

            // Hạn cam kết đã tính sẵn quãng bóng bay bên trong SimpleKeeperController.
            if (_controller.TryCommit(read, timeToContact, _profile, out DiveDecision d))
            {
                _decision = d;
                _committed = true;

                // Thời điểm cam kết THẬT (âm = trước lúc chạm bóng) ghi đè hồ sơ, để pha do dự
                // phải trả giá bằng đúng số mili giây đã do dự — cả ở hình ảnh lẫn ở kết quả.
                if (_effective != null) _effective.commitOffsetMs = -d.commitTime * 1000f;
                return true;
            }

            return false;
        }

        /// <summary>Chân đã chạm bóng: bắt đầu đếm giờ bay người.</summary>
        public void OnContact()
        {
            _timeSinceContact = 0f;
            _diving = true;

            if (!_committed)
            {
                // Không kịp đọc vị: đứng giữa. Cam kết ngay lúc chạm bóng.
                _decision = new DiveDecision { targetCell = 4, commitTime = 0f, isFullDive = false };
                _committed = true;
                if (_effective != null) _effective.commitOffsetMs = 0f;
            }

            _controller.StartDive();
        }

        /// <summary>Bay người. Gọi mỗi khung hình trong pha bóng bay.</summary>
        public void TickDive(float dt)
        {
            if (!_diving) return;
            _timeSinceContact += dt;

            float prog = KeeperReach.ReachProgress(_decision.targetCell, _timeSinceContact, EffectiveProfile);

            float3 cell = GoalFrame.CellCenter(_decision.targetCell);
            // Thân người dừng thấp hơn và gần tâm hơn tay: tay mới là thứ chạm bóng.
            Vector3 target = new Vector3(
                Mathf.Clamp(cell.x * 0.80f, -2.95f, 2.95f),
                Mathf.Clamp(cell.y * 0.72f + 0.20f, 0.38f, 1.85f),
                homePosition.z);

            transform.position = Vector3.Lerp(homePosition, target, prog);

            // Nghiêng người: bay hết tầm thì gần như nằm ngang, đổ người tại chỗ thì chỉ nghiêng nhẹ.
            float maxTilt = _decision.isFullDive ? 74f : 22f;
            float side = cell.x >= 0f ? -1f : 1f;
            transform.rotation = Quaternion.Euler(0f, 180f, maxTilt * prog * side);

            // Găng tay phía đổ người vươn tới đúng tâm ô — đây chính là điểm SaveResolver đo.
            Transform reaching = cell.x >= 0f ? rightGlove : leftGlove;
            if (reaching != null)
            {
                Vector3 hand = (Vector3)(float3)KeeperReach.HandPositionAt(_decision.targetCell, _timeSinceContact, EffectiveProfile);
                reaching.position = hand;
            }
        }

        /// <summary>
        /// Phân giải pha cản phá tại đúng khoảnh khắc bóng qua mặt phẳng khung thành (T21).
        /// </summary>
        public SaveResult ResolveSave(in BallState atCrossing, float ballArrivalTime, uint seed, out float3 deflectVelocity)
        {
            float handDist = KeeperReach.HandDistanceToBall(_decision, atCrossing.position, ballArrivalTime, EffectiveProfile);
            SaveResult result = SaveResolver.Resolve(atCrossing, _decision, handDist, EffectiveProfile, seed, out deflectVelocity);
            _controller.Recover();
            return result;
        }

        /// <summary>Khoảng cách tay–bóng dự kiến, chỉ để hiển thị debug.</summary>
        public float HandDistanceTo(float3 crossingPoint, float ballArrivalTime)
            => KeeperReach.HandDistanceToBall(_decision, crossingPoint, ballArrivalTime, EffectiveProfile);

        private static void CopyProfile(KeeperProfile src, KeeperProfile dst)
        {
            if (src == null || dst == null) return;
            dst.readAccuracy = src.readAccuracy;
            dst.reactionMs = src.reactionMs;
            dst.commitOffsetMs = src.commitOffsetMs;
            dst.reachScale = src.reachScale;
            dst.parryChance = src.parryChance;
            dst.memoryWeight = src.memoryWeight;
        }
    }
}
