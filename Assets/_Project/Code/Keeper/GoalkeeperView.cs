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
    ///
    /// CÁCH GIỮ BẤT BIẾN ẤY ĐÃ ĐỔI (2026-08-28). Trước: tắt hẳn Animator lúc chạm bóng rồi
    /// lái toàn bộ thân người bằng code. Đúng tuyệt đối, nhưng thủ môn trượt ngang như tủ
    /// lạnh rồi đứng hình ở tư thế cuối cho tới hết lượt. Nay: chia đôi việc.
    ///
    ///   • VỊ TRÍ GỐC vẫn do code lái, vẫn bằng đúng <c>ReachProgress</c> như cũ.
    ///   • TƯ THẾ do clip Mixamo diễn (nhào, chạm đất, chống tay đứng dậy) — mỗi ô một clip.
    ///   • BÀN TAY bị <see cref="KeeperHandIK"/> ghim vào đúng
    ///     <see cref="KeeperReach.HandPositionAt"/>, nên điểm SaveResolver đem đi chấm không
    ///     đổi một milimét.
    ///
    /// Nói cách khác bất biến không hề được nới; chỉ có phần KHÔNG ảnh hưởng tới kết quả
    /// chấm — dáng người — mới được trả về cho hoạt ảnh.
    /// </summary>
    public sealed class GoalkeeperView : MonoBehaviour
    {
        [Header("Vị trí")]
        [SerializeField] private Vector3 homePosition = new Vector3(0f, 0.95f, 11.0f);

        [Header("Găng tay (chỉ dùng cho nhánh khối primitive)")]
        [SerializeField] private Transform leftGlove;
        [SerializeField] private Transform rightGlove;

        [Header("Model có xương (tuỳ chọn)")]

        /// <summary>
        /// Animator của model thủ môn, nếu scene dựng bằng model thay vì khối primitive.
        /// KHÔNG còn bị tắt lúc chạm bóng nữa — xem phần "cách giữ bất biến" ở đầu lớp.
        /// </summary>
        [SerializeField] private Animator animator;

        /// <summary>
        /// Bộ ghim bàn tay, ngồi cùng GameObject với <see cref="animator"/>. Thiếu nó thì bàn
        /// tay thả trôi theo clip và hình ảnh lệch khỏi kết quả chấm, nên nhánh có model coi
        /// nó là bắt buộc; nhánh khối primitive không dùng.
        /// </summary>
        [SerializeField] private KeeperHandIK handIK;

        /// <summary>
        /// Tên state đứng chờ trong Animator Controller của thủ môn.
        /// Chép tay từ <c>KeeperAnimatorControllerBuilder.IdleState</c>: bộ dựng nằm trong
        /// assembly Editor nên runtime không tham chiếu tới được. Test
        /// <c>KeeperAnimatorControllerTests</c> ràng hai bên phải bằng nhau.
        /// </summary>
        private const string IdleState = "Idle";

        /// <summary>Tiền tố state bay người: "Dive0".."Dive8" theo chỉ số ô. Xem <see cref="IdleState"/>.</summary>
        private const string DiveStatePrefix = "Dive";

        /// <summary>
        /// Gốc toạ độ nằm ở GÓT CHÂN (model có xương) hay ở GIỮA THÂN (khối primitive)?
        ///
        /// Mấy con số bay người ở <see cref="TickDive"/> được chỉnh cho một cái capsule có gốc
        /// giữa thân, cao 0.95m. Model Mixamo thì gốc nằm dưới đất. Cắm nguyên công thức cũ
        /// vào model là những pha bay thấp cho ra y âm — thủ môn chui xuống dưới mặt cỏ.
        /// </summary>
        [SerializeField] private bool rootAtFeet;

        /// <summary>Chiều cao gốc toạ độ mà mấy con số bay người ở dưới được chỉnh theo.</summary>
        private const float TunedPivotHeight = 0.95f;

        /// <summary>
        /// Bao lâu để gốc toạ độ trở về chỗ đứng chờ sau khi pha cản phá đã được chấm.
        /// Trùng cỡ độ dài pha đứng dậy trong clip Mixamo (~1s), nên thủ môn vừa chống tay
        /// đứng lên vừa lùi về vạch vôi thay vì đứng dậy tại chỗ rồi giật một phát về giữa.
        /// </summary>
        private const float RecoverSeconds = 1.10f;

        /// <summary>
        /// Chốt chặn: nếu quá lâu mà chưa ai gọi <see cref="ResolveSave"/> thì tự vào pha hồi
        /// phục. Có những cú bóng không bao giờ tới mặt phẳng khung thành (sút hụt, lăn chết)
        /// và pha cản phá không được phán — thiếu chốt này thủ môn nằm luôn dưới cỏ tới hết
        /// lượt, đúng cái lỗi đứng hình mà bản này sinh ra để sửa.
        /// </summary>
        private const float MaxDiveHold = 1.20f;

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
        private bool _diveRightHand = true;
        private float _timeSinceContact;
        private Vector3 _homeGloveL, _homeGloveR;

        // Pha hồi phục: đứng dậy và lùi về vạch vôi.
        private bool _recovering;
        private float _recoverElapsed;
        private Vector3 _recoverFromPos;
        private Quaternion _recoverFromRot;
        private Vector3 _recoverHand;
        private bool _recoverRightHand;

        /// <summary>
        /// Bàn tay đang vươn ra cản bóng — CHÍNH cái xương mà <see cref="KeeperHandIK"/> ghim
        /// vào điểm SaveResolver đem đi chấm. Bóng bắt dính bám vào đây, nên quả bóng nằm
        /// đúng chỗ bàn tay chạm nó chứ không phải một chỗ ước chừng.
        ///
        /// Trước lúc chạm bóng thì trả về tay phải: chưa đổ người thì chưa có "bên đang vươn",
        /// và không ai hỏi tới nó ở giai đoạn đó.
        /// </summary>
        public Transform CatchHand => _diveRightHand ? rightGlove : leftGlove;

        public KeeperPhase Phase => _controller.Phase;
        public DiveDecision Decision => _decision;
        public bool HasCommitted => _committed;
        public float Confidence => _confidence;
        public int PredictedCell => _bestCell;

        /// <summary>Hồ sơ dùng để phân giải — đã gắn thời điểm cam kết thật của lượt hiện tại.</summary>
        public KeeperProfile EffectiveProfile => _effective != null ? _effective : _profile;

        /// <summary>
        /// Có clip lo tư thế hay không. Sai thì đây là nhánh khối primitive, và code phải tự
        /// nghiêng người lấy — khối hộp không tự biết nhào.
        /// </summary>
        private bool ClipDriven => animator != null && animator.runtimeAnimatorController != null;

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
            _recovering = false;
            _recoverElapsed = 0f;
            _confidence = 0f;
            _bestCell = 4;
            _decision = default;
            _diveRightHand = true;
            _timeSinceContact = 0f;

            transform.position = homePosition;
            transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            if (leftGlove != null) leftGlove.localPosition = _homeGloveL;
            if (rightGlove != null) rightGlove.localPosition = _homeGloveR;

            if (handIK != null) handIK.Release();

            if (animator != null)
            {
                animator.enabled = true;
                animator.Play(IdleState, 0, 0f);
            }

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

        /// <summary>Chân đã chạm bóng: bắt đầu đếm giờ bay người và phát clip của ô đã chọn.</summary>
        public void OnContact()
        {
            _timeSinceContact = 0f;
            _diving = true;
            _recovering = false;
            _recoverElapsed = 0f;

            if (!_committed)
            {
                // Không kịp đọc vị: đứng giữa. Cam kết ngay lúc chạm bóng.
                _decision = new DiveDecision { targetCell = 4, commitTime = 0f, isFullDive = false };
                _committed = true;
                if (_effective != null) _effective.commitOffsetMs = 0f;
            }

            // Chốt bên tay NGAY ở đây, cùng công thức TickDive dùng để chọn tay ghim IK.
            // Tính lại ở chỗ khác là có ngày hai chỗ lệch nhau và bóng dính vào tay không
            // cản bóng.
            _diveRightHand = GoalFrame.CellCenter(_decision.targetCell).x >= 0f;

            PlayDiveClip(_decision.targetCell);

            _controller.StartDive();
        }

        /// <summary>
        /// Phát clip bay người của ô. Thiếu state thì cứ để clip đang chạy — thà thủ môn đứng
        /// yên diễn tiếp còn hơn Animator.Play ném cảnh báo mỗi cú sút rồi đứng ở tư thế rỗng.
        /// </summary>
        private void PlayDiveClip(int cell)
        {
            if (!ClipDriven) return;

            int hash = Animator.StringToHash(DiveStatePrefix + cell);
            if (!animator.HasState(0, hash))
            {
                Debug.LogWarning($"[GoalkeeperView] Animator thiếu state {DiveStatePrefix}{cell}. " +
                                 "Chạy Eleven > Art > Build Keeper Animator Controller.");
                return;
            }

            // CrossFade chứ không Play: thủ môn đang nhún người chờ, cắt phát sang khung đầu
            // clip bay là một cú giật thấy rõ ở 60fps. 0.06s đủ để mượt mà chưa kịp ăn vào
            // ngân sách thời gian của pha vươn tay.
            animator.CrossFadeInFixedTime(hash, 0.06f, 0, 0f);
        }

        /// <summary>Bay người. Gọi mỗi khung hình trong pha bóng bay.</summary>
        public void TickDive(float dt)
        {
            if (!_diving) return;
            _timeSinceContact += dt;

            if (!_recovering && _timeSinceContact >= MaxDiveHold) BeginRecover();

            if (_recovering)
            {
                TickRecover(dt);
                return;
            }

            float prog = KeeperReach.ReachProgress(_decision.targetCell, _timeSinceContact, EffectiveProfile);

            float3 cell = GoalFrame.CellCenter(_decision.targetCell);
            // Thân người dừng thấp hơn và gần tâm hơn tay: tay mới là thứ chạm bóng.
            float bodyY = Mathf.Clamp(cell.y * 0.72f + 0.20f, 0.38f, 1.85f);

            // Với model gốc ở gót chân, con số trên là chiều cao GIỮA THÂN chứ không phải
            // chiều cao gót. Quy nó về "gót rời mặt đất bao nhiêu": bay lên góc cao thì bật
            // khỏi đất thật, còn bay thấp thì chân vẫn quét sát cỏ chứ không lún xuống dưới.
            float targetY = rootAtFeet
                ? homePosition.y + Mathf.Max(0f, bodyY - TunedPivotHeight)
                : bodyY;

            Vector3 target = new Vector3(
                Mathf.Clamp(cell.x * 0.80f, -2.95f, 2.95f),
                targetY,
                homePosition.z);

            transform.position = Vector3.Lerp(homePosition, target, prog);

            // Nghiêng người CHỈ khi không có clip. Clip Mixamo đã tự nhào rồi; cộng thêm 74°
            // của code vào nữa là thủ môn xoay hai vòng và cắm đầu xuống cỏ.
            if (!ClipDriven)
            {
                float maxTilt = _decision.isFullDive ? 74f : 22f;
                float side = cell.x >= 0f ? -1f : 1f;
                transform.rotation = Quaternion.Euler(0f, 180f, maxTilt * prog * side);
            }

            // Bàn tay phía đổ người vươn tới đúng tâm ô — đây chính là điểm SaveResolver đo.
            Vector3 hand = (Vector3)(float3)KeeperReach.HandPositionAt(
                _decision.targetCell, _timeSinceContact, EffectiveProfile);
            bool right = _diveRightHand;

            if (handIK != null)
            {
                handIK.Pin(right, hand, 1f);
            }
            else
            {
                Transform reaching = right ? rightGlove : leftGlove;
                if (reaching != null) reaching.position = hand;
            }
        }

        /// <summary>
        /// Vào pha đứng dậy. Chốt lại chỗ đang nằm để nội suy về vạch vôi từ đó — clip lo phần
        /// chống tay đứng lên, code chỉ lo đưa gốc toạ độ về.
        /// </summary>
        private void BeginRecover()
        {
            if (_recovering || !_diving) return;

            _recovering = true;
            _recoverElapsed = 0f;
            _recoverFromPos = transform.position;
            _recoverFromRot = transform.rotation;

            _recoverRightHand = _diveRightHand;
            _recoverHand = (Vector3)(float3)KeeperReach.HandPositionAt(
                _decision.targetCell, _timeSinceContact, EffectiveProfile);
        }

        private void TickRecover(float dt)
        {
            _recoverElapsed += dt;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_recoverElapsed / RecoverSeconds));

            transform.position = Vector3.Lerp(_recoverFromPos, homePosition, t);
            transform.rotation = Quaternion.Slerp(_recoverFromRot, Quaternion.Euler(0f, 180f, 0f), t);

            // Nhả tay dần chứ không nhả phát một: nhả đột ngột là bàn tay nhảy từ điểm ghim về
            // chỗ clip đang để nó, thấy rõ thành một cú giật tay.
            if (handIK != null) handIK.Pin(_recoverRightHand, _recoverHand, 1f - t);

            if (t < 1f) return;

            _diving = false;
            _recovering = false;
            if (handIK != null) handIK.Release();
            if (ClipDriven) animator.CrossFadeInFixedTime(IdleState, 0.25f, 0, 0f);
        }

        /// <summary>
        /// Phân giải pha cản phá tại đúng khoảnh khắc bóng qua mặt phẳng khung thành (T21).
        /// </summary>
        public SaveResult ResolveSave(in BallState atCrossing, float ballArrivalTime, uint seed, out float3 deflectVelocity)
        {
            float handDist = KeeperReach.HandDistanceToBall(_decision, atCrossing.position, ballArrivalTime, EffectiveProfile);
            SaveResult result = SaveResolver.Resolve(atCrossing, _decision, handDist, EffectiveProfile, seed, out deflectVelocity);
            _controller.Recover();

            // Kết quả đã chấm xong: từ giây này trở đi hình ảnh không còn ràng buộc gì với
            // KeeperReach nữa, nên đây là chỗ sớm nhất được phép cho thủ môn đứng dậy.
            BeginRecover();

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
