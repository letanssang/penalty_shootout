using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using Eleven.Ball;
using Eleven.Keeper;
using Eleven.Match;
using Eleven.Presentation;
using Eleven.Presentation.Kicker;
using Eleven.Presentation.Audio;
using Eleven.Presentation.Net;
using Eleven.Shooter;
using Random = Unity.Mathematics.Random;

namespace Eleven.UI
{
    /// <summary>
    /// NHẠC TRƯỞNG của trận luân lưu. Lớp này KHÔNG tự cài đặt một luật, một mô hình vật lý
    /// hay một hành vi AI nào — mọi thứ đã có chủ ở bảy phase trước; việc ở đây là nối chúng
    /// lại đúng thứ tự và đúng đồng hồ:
    ///
    ///   Phase 0  DeviceTier / PerfHud            (bậc máy, HUD hiệu năng)
    ///   Phase 1  BallSolver qua BallDriver, TrajectoryPredictor, GoalGeometry
    ///   Phase 2  SwipeCollector → ShotMapper, TimingWindow, KnuckleForce
    ///   Phase 3  KickerBoneCueSource → BayesianKeeperBrain → SimpleKeeperController
    ///            → ReachEnvelope/KeeperReach → SaveResolver, ShotHistory
    ///   Phase 4  ShootoutRules, KickSequencer, MatchSave, DifficultySelector
    ///   Phase 5  CameraDirector/CameraRig, ReplayPlayer, GoalNetView, hậu kỳ va chạm
    ///   Phase 6  BenchmarkRunner/SoakTest (qua DebugHotkeys)
    ///
    /// NHỊP MỘT LƯỢT SÚT:
    ///   Chạm ngón tay  → chốt ngắm, người sút BẮT ĐẦU CHẠY ĐÀ (thủ môn bắt đầu đọc vị)
    ///   Vuốt           → thân người nghiêng theo, ĐÂY LÀ TÍN HIỆU thủ môn đọc được
    ///   Nhả ngón tay   → chấm thời điểm bằng TimingWindow rồi mới dựng ShotIntent
    ///
    /// Nhờ tách "nhả tay" khỏi "dựng cú sút", người chơi có thể lừa: nghiêng người một
    /// đằng suốt đà chạy rồi giật cổ tay sang hướng khác ở khoảnh khắc cuối. Thủ môn đã
    /// cam kết thì không được sửa hướng (T19) — đúng như sinh học của một quả 11m thật.
    /// </summary>
    public sealed class MatchGameLoop : MonoBehaviour
    {
        [Header("Tham chiếu Scene")]
        [SerializeField] private Transform ballTransform;
        [SerializeField] private TrailRenderer ballTrail;
        [SerializeField] private GoalNetView goalNet;
        [SerializeField] private GoalkeeperView goalkeeper;
        [Tooltip("Người sút greybox. Để trống nếu dùng model Humanoid ở ô dưới.")]
        [SerializeField] private KickerAvatar kickerGreybox;

        [Tooltip("Người sút dùng model Humanoid + Mecanim (T35). Ưu tiên hơn greybox.")]
        [SerializeField] private MecanimKickerAnimator kickerModel;

        // Một đường đi duy nhất cho cả hai. Xem ghi chú ở cuối KickerAvatar.cs.
        private IKickerAnimator kicker;
        [SerializeField] private KickerBoneCueSource cueSource;
        [SerializeField] private TouchSwipeReceiver swipeReceiver;
        [SerializeField] private ScoreboardUI scoreboard;
        [SerializeField] private CameraRig cameraRig;
        [SerializeField] private MatchSaveLifecycle saveLifecycle;
        [SerializeField] private AudioDirector audioDirector;

        [Header("Hồ sơ độ khó thủ môn (T25)")]
        [SerializeField] private KeeperProfile easyProfile;
        [SerializeField] private KeeperProfile mediumProfile;
        [SerializeField] private KeeperProfile hardProfile;

        [Header("Nhịp trận")]
        [Tooltip("Thời điểm chạm bóng, tính theo tỉ lệ của pha chạy đà. Đây là tâm cửa sổ thời điểm.")]
        [SerializeField] private float idealContactFraction = 0.80f;
        [SerializeField] private uint matchSeed = 20260827u;

        // ── Hằng hình học, lấy từ nguồn sự thật duy nhất ──────────────────────
        const float GoalPlaneZ = GoalFrame.PenaltyDistance;
        const float BallRadius = 0.11f;

        // ── Hệ thống lõi ─────────────────────────────────────────────────────
        private BallDriver _driver;
        private readonly KickSequencer _seq = new KickSequencer();
        private DifficultySelector _difficulty;
        private readonly ImpactPostProcessEffect _impact = new ImpactPostProcessEffect();
        private ReplayPlayer _replayPlayer;
        private TimingWindowConfig _timingCfg = TimingWindowConfig.Default;
        private KickPhaseDurations _durations;
        private ShootoutState _state;

        // ── Trạng thái lượt hiện tại ─────────────────────────────────────────
        private uint _kickSeed;
        private ShotIntent _intent;
        private float3 _launchVelocity;
        private bool _hasFired;
        private bool _shotLive;
        private bool _crossingResolved;
        private bool _outcomeReported;
        private SaveResult _saveResult = SaveResult.Missed;
        private ShotOutcome _geomOutcome = ShotOutcome.Short;
        private float3 _crossingPoint;
        private int _crossingCell = 4;
        private float _arrivalTime;
        private float _sinceCrossing;
        private float _strikeTimer = -1f;
        private KickResult _lastResult = KickResult.Pending;
        private ReplayKickData _lastKickData;
        private float3 _lastLaunchVelocity;
        private bool _hasReplay;
        private float3 _currentAimPoint = new float3(0f, 1.22f, GoalPlaneZ);
        private float _aimLateralShift;    // người sút dạt ngang bao nhiêu mét theo hướng ngắm
        private ShotIntent _pendingAiIntent;
        private bool _hasPendingAiIntent;
        private bool _matchOver;

        // ── Replay ───────────────────────────────────────────────────────────
        private bool _replayActive;
        private float _replayOrbitYaw;

        // ── Bộ đệm HUD (dựng một lần, tránh cấp phát mỗi khung) ──────────────
        private readonly List<KickResult> _homeCache = new List<KickResult>(16);
        private readonly List<KickResult> _awayCache = new List<KickResult>(16);
        private readonly System.Text.StringBuilder _debugText = new System.Text.StringBuilder(96);

        private UnityEngine.Camera _cachedCamera;
        private UnityEngine.Camera ActiveCamera
        {
            get
            {
                if (_cachedCamera == null)
                {
                    _cachedCamera = cameraRig != null ? cameraRig.GetComponent<UnityEngine.Camera>() : null;
                    if (_cachedCamera == null) _cachedCamera = UnityEngine.Camera.main;
                }
                return _cachedCamera;
            }
        }

        private bool IsPlayerTurn => _state.IsHomeTurn;
        private float RunUpDuration => _durations.runUp;
        private float IdealContactTime => _durations.runUp * math.clamp(idealContactFraction, 0.2f, 0.98f);

        // ═══════════════════════════════════════════════════════════════════════
        //  Khởi tạo
        // ═══════════════════════════════════════════════════════════════════════

        private void Start()
        {
            _kickSeed = matchSeed != 0u ? matchSeed : 1u;

            if (ballTransform != null)
            {
                _driver = ballTransform.GetComponent<BallDriver>();
                if (_driver == null) _driver = ballTransform.gameObject.AddComponent<BallDriver>();
                _driver.OnSimStep += OnBallSimStep;
            }

            // Nhịp một lượt. aiming để rộng vì người chơi cầm điện thoại lần đầu cần thời gian;
            // runUp mới là pha chịu áp lực thời gian.
            _durations = new KickPhaseDurations
            {
                placing = 0.55f,
                aiming = 12.0f,
                runUp = 1.30f,
                contact = 0.06f,
                flight = 4.0f,
                resolution = 1.30f,
                reaction = 2.60f
            };
            _seq.Durations = _durations;
            _seq.OnPhaseChanged += HandlePhaseChanged;

            _difficulty = (easyProfile != null && mediumProfile != null && hardProfile != null)
                ? new DifficultySelector(easyProfile, mediumProfile, hardProfile, DifficultyLevel.Medium)
                : new DifficultySelector(DifficultyLevel.Medium);

            goalkeeper?.SetProfile(_difficulty.ActiveProfile);

            if (goalNet != null) goalNet.Initialize();

            // Model thật thắng greybox khi cả hai cùng được gán — greybox chỉ là thứ để
            // trận đấu vẫn chạy được khi chưa có tài sản nghệ thuật nào.
            kicker = kickerModel != null ? (IKickerAnimator)kickerModel : kickerGreybox;
            kicker?.SetRunUpDuration(RunUpDuration);

            if (cueSource != null && kicker != null)
            {
                cueSource.SetBones(kicker.Root, kicker.PlantFoot, kicker.Hips);
                cueSource.runUpDuration = RunUpDuration;

                // Mốc 0 của tín hiệu chân trụ được đặt lại TỪNG KHUNG HÌNH trong TickRunUp,
                // không phải một lần ở đây — xem ghi chú ở đó.
                cueSource.ballPosition = new float3(0f, BallRadius, 0f);
            }

            if (swipeReceiver != null)
            {
                swipeReceiver.OnAimBegin += HandleAimBegin;
                swipeReceiver.OnAimMove += HandleAimMove;
                swipeReceiver.OnSwipeReleased += HandleSwipeReleased;
                swipeReceiver.OnSwipeCancelled += HandleSwipeCancelled;
                swipeReceiver.IsInputEnabled = false;
            }

            if (scoreboard != null)
            {
                scoreboard.OnReplayClicked += PlayReplay;
                scoreboard.OnNextKickClicked += HandleNextKickPressed;
                scoreboard.OnDifficultyChanged += HandleDifficultyChanged;
                scoreboard.SetDifficulty(_difficulty.Current);
            }

            if (cameraRig != null)
            {
                cameraRig.BindBall(ballTransform);
                cameraRig.SetShot(CameraShot.BehindShooter, 0f);
            }

            // Bản lưu: có trận dở thì đá tiếp, không thì trận mới.
            _state = default;
            _state.homeKicksFirst = true;   // người chơi mở màn — mặc định struct là máy đá trước
            if (saveLifecycle != null && saveLifecycle.State.TotalKicksTaken > 0)
            {
                _state = saveLifecycle.State;
            }

            RefreshScoreboard();
            audioDirector?.SetCrowdTension(0.25f);
            audioDirector?.PlayWhistle();

            BeginNextKick();
        }

        private void OnDestroy()
        {
            if (_driver != null) _driver.OnSimStep -= OnBallSimStep;

            if (swipeReceiver != null)
            {
                swipeReceiver.OnAimBegin -= HandleAimBegin;
                swipeReceiver.OnAimMove -= HandleAimMove;
                swipeReceiver.OnSwipeReleased -= HandleSwipeReleased;
                swipeReceiver.OnSwipeCancelled -= HandleSwipeCancelled;
            }

            if (scoreboard != null)
            {
                scoreboard.OnReplayClicked -= PlayReplay;
                scoreboard.OnNextKickClicked -= HandleNextKickPressed;
                scoreboard.OnDifficultyChanged -= HandleDifficultyChanged;
            }

            _seq.OnPhaseChanged -= HandlePhaseChanged;
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  Vòng lặp khung hình
        // ═══════════════════════════════════════════════════════════════════════

        private void Update()
        {
            float dt = Time.deltaTime;

            if (_replayActive)
            {
                TickReplay(dt);
                cameraRig?.Tick(dt);
                return;
            }

            _seq.Tick(dt);

            switch (_seq.Phase)
            {
                case KickPhase.Placing:
                    TickKicker(dt);
                    break;

                case KickPhase.Aiming:
                    TickKicker(dt);
                    // Máy không có ngón tay: nó "chốt ngắm" sau một nhịp thở cho người xem kịp nhìn.
                    if (!IsPlayerTurn && _seq.PhaseElapsed > 0.65f) _seq.ConfirmAim();
                    break;

                case KickPhase.RunUp:
                    TickRunUp(dt);
                    break;

                case KickPhase.Contact:
                    if (!_hasFired) FireScuffedShot();
                    TickStrikeAnimation(dt);
                    break;

                case KickPhase.Flight:
                    TickFlight(dt);
                    break;

                case KickPhase.Resolution:
                case KickPhase.Reaction:
                    TickAfterShot(dt);
                    break;
            }

            _impact.Tick(dt);
            cameraRig?.Tick(dt);
        }

        private void TickRunUp(float dt)
        {
            float t01 = math.saturate(_seq.PhaseElapsed / math.max(0.01f, RunUpDuration));
            if (kicker != null)
            {
                kicker.Tick(dt, t01);

                // Dạt ngang dần theo hướng ngắm — tín hiệu nặng ký nhất trong bảng của T18
                // (trọng số 1.0). Dùng sqrt(t01) chứ không phải t01: thủ môn cam kết ở khoảng
                // 36% đà chạy, mà t01 tuyến tính lúc đó mới lộ ra 0.10m — quá nhỏ so với dải
                // ±0.20m mà T18 mong đợi. sqrt cho 0.17m ở đúng thời điểm đó, vẫn giữ nguyên
                // tính chất "lộ dần".
                float reveal = _aimLateralShift * math.sqrt(t01);
                Vector3 kp = kicker.Root.position;
                kp.x += reveal;
                kicker.Root.position = kp;

                // MỐC 0 CỦA TÍN HIỆU CHÂN TRỤ, ĐẶT LẠI MỖI KHUNG HÌNH.
                // T18 kỳ vọng plantFootLateralOffset nằm trong ±0.20m quanh 0. Chân trụ thì
                // vừa lệch hẳn sang một bên bóng (~0.4m) vừa ĐANG DI CHUYỂN suốt đà chạy, nên
                // mọi mốc cố định đều sai: đo ở cuối đà chạy rồi đọc ở 36% đà chạy cho ra lệch
                // -0.48m, ngoài hẳn dải, phân phối phẳng, tin cậy 0.06, và thủ môn đứng giữa
                // mọi lượt (đo trên Pixel 7 ngày 2026-08-27).
                // Mốc đúng là vị trí chân trụ SẼ ở đâu tại chính khoảnh khắc này nếu người sút
                // ngắm thẳng. Lấy vị trí thật trừ đi phần dạt, ta được đúng mốc đó, và tín hiệu
                // còn lại đúng bằng độ lệch do ý đồ sút — thứ mà thủ môn thật sự đọc.
                if (cueSource != null && kicker.PlantFoot != null)
                {
                    cueSource.ballPosition = new float3(
                        kicker.PlantFoot.position.x - reveal, BallRadius, 0f);
                }
            }

            float timeToContact = math.max(0f, IdealContactTime - _seq.PhaseElapsed);

            // Thủ môn đọc tín hiệu từ XƯƠNG THẬT của người sút (T17) — không phải từ ý đồ
            // cú sút. Đó là lý do lừa được thủ môn.
            if (goalkeeper != null && cueSource != null)
            {
                KeeperCues cues = cueSource.Sample(timeToContact);
                if (goalkeeper.TickRead(cues, timeToContact, _kickSeed)) UpdateKeeperDebug();
            }

            if (IsPlayerTurn)
            {
                scoreboard?.SetTimingBar(true, t01,
                    idealContactFraction,
                    _timingCfg.perfectHalfWidthSeconds / RunUpDuration,
                    _timingCfg.goodHalfWidthSeconds / RunUpDuration);
            }
            else if (!_hasFired && _seq.PhaseElapsed >= IdealContactTime)
            {
                // Ý đồ lẽ ra đã chốt ở đầu đà chạy; dựng bù ở đây chỉ để không bao giờ sút
                // bằng một ShotIntent rỗng (tốc độ 0, ngắm vào gốc toạ độ).
                if (!_hasPendingAiIntent) _pendingAiIntent = BuildAiIntent(NextKickSeed());
                FireShot(in _pendingAiIntent);
            }
        }

        private void TickStrikeAnimation(float dt)
        {
            if (_strikeTimer < 0f) return;
            _strikeTimer += dt;
            TickKicker(dt);
        }

        /// <summary>
        /// Một điểm gọi duy nhất cho lớp hoạt ảnh. Tiến độ pha lấy từ sequencer chứ không đếm
        /// bằng bộ đếm riêng: bộ đếm riêng là thứ trôi lệch khỏi pha thật sau mỗi lần ai đó
        /// chỉnh KickPhaseDurations, mà không có gì báo.
        /// </summary>
        private void TickKicker(float dt)
        {
            if (kicker == null) return;
            float duration = math.max(0.01f, _durations.For(_seq.Phase));
            kicker.Tick(dt, math.saturate(_seq.PhaseElapsed / duration));
        }

        private void TickFlight(float dt)
        {
            TickStrikeAnimation(dt);
            goalkeeper?.TickDive(dt);

            if (_driver == null) return;

            // Lưới chỉ rung khi bóng còn sống; nhưng ĐỒNG HỒ PHÁN KẾT QUẢ thì phải chạy tiếp
            // kể cả khi bóng đã chết. Thủ môn bắt dính là đóng băng bóng ngay tại chỗ — nếu
            // đồng hồ này nằm trong nhánh "bóng còn sống" thì mọi pha bắt dính sẽ không bao giờ
            // được phán, sequencer tự trôi sang Resolution với kết quả Pending, và bảng tỷ số
            // ăn một lượt ma.
            if (_driver.IsLive)
            {
                BallState s = _driver.State;
                goalNet?.UpdateSimulation(dt, s.position, s.velocity, BallRadius);
            }

            if (_crossingResolved)
            {
                _sinceCrossing += dt;
                // Trễ một nhịp trước khi phán: để bóng kịp găm lưới / bật cột trên màn hình.
                if (_sinceCrossing >= 1.05f && !_outcomeReported) ReportOutcome();
            }
            else if (_seq.PhaseElapsed > _durations.flight - 0.35f)
            {
                // Bóng không bao giờ tới mặt phẳng khung thành (sút ngược, sút hụt, lăn chết).
                _geomOutcome = ShotOutcome.Short;
                _crossingResolved = true;
                _sinceCrossing = 0f;
            }
        }

        private void TickAfterShot(float dt)
        {
            goalkeeper?.TickDive(dt);
            TickKicker(dt);

            if (_driver != null && _driver.IsLive)
            {
                BallState s = _driver.State;
                goalNet?.UpdateSimulation(dt, s.position, s.velocity, BallRadius);
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  Vật lý bóng — chạy ở ĐỒNG HỒ SOLVER 120Hz, không phải tần số khung hình
        // ═══════════════════════════════════════════════════════════════════════

        private void OnBallSimStep(BallState s)
        {
            if (!_shotLive || _driver == null) return;

            BallState prev = _driver.PreviousState;
            BallParams p = _driver.Parameters;

            // 1. Bất ổn định knuckle (T15) — gia tốc gameplay, KHÔNG trộn vào solver.
            _driver.ExternalAcceleration = _intent.unstable
                ? KnuckleForce.Evaluate(in s, KnuckleConfig.Default, _driver.FlightTime, _kickSeed)
                : float3.zero;

            // 2. Khoảnh khắc bóng qua mặt phẳng khung thành: chốt kết quả hình học và pha cản phá.
            if (!_crossingResolved && prev.position.z < GoalPlaneZ && s.position.z >= GoalPlaneZ)
            {
                float denom = s.position.z - prev.position.z;
                float frac = denom > 1e-6f ? (GoalPlaneZ - prev.position.z) / denom : 0f;
                frac = math.saturate(frac);

                _crossingPoint = math.lerp(prev.position, s.position, frac);
                float3 crossVel = math.lerp(prev.velocity, s.velocity, frac);
                _arrivalTime = math.max(0.05f, _driver.FlightTime - BallDriver.SimDt * (1f - frac));
                _crossingCell = GoalFrame.CellOf(_crossingPoint);
                _crossingResolved = true;

                // Kết quả hình học do T10 phán, chạy lại đúng một bước solver từ trạng thái
                // ngay trước mặt phẳng — không tự viết lại luật vào/trượt ở đây.
                _geomOutcome = GoalGeometry.Classify(in prev, in p, out _, out _);

                var atCrossing = new BallState(_crossingPoint, crossVel, s.spin);
                if (goalkeeper != null)
                {
                    _saveResult = goalkeeper.ResolveSave(in atCrossing, _arrivalTime, _kickSeed, out float3 deflect);
                    ApplySaveToBall(in atCrossing, _saveResult, deflect);
                }

                UpdateKeeperDebug();
                _sinceCrossing = 0f;
                return;
            }

            // 3. Bật khung gỗ — chỉ khi T10 đã phán là chạm khung, để hình ảnh khớp kết quả.
            if (_crossingResolved && !_frameBounced && _saveResult == SaveResult.Missed)
            {
                if (_geomOutcome == ShotOutcome.PostOut || _geomOutcome == ShotOutcome.PostIn)
                {
                    BounceOffPost(s);
                    return;
                }
                if (_geomOutcome == ShotOutcome.Crossbar)
                {
                    BounceOffCrossbar(s);
                    return;
                }
            }

            // 4. Lưới hãm bóng lại và bóng nảy trên mặt cỏ.
            ApplyNetDragAndGround(s);
        }

        private bool _frameBounced;

        private void ApplySaveToBall(in BallState atCrossing, SaveResult result, float3 deflectVelocity)
        {
            if (result == SaveResult.Missed) return;

            audioDirector?.PlayGloveSave();
            _impact.TriggerImpact(0.5f, 0.10f);
            cameraRig?.Shake(0.05f, 0.18f);

            if (result == SaveResult.Caught)
            {
                // Bắt dính: bóng chết trong tay, không nảy đi đâu nữa.
                _driver.Override(new BallState(atCrossing.position, float3.zero, float3.zero));
                _driver.Freeze();
                _shotLive = false;
                if (ballTrail != null) ballTrail.emitting = false;
                return;
            }

            float3 v = math.lengthsq(deflectVelocity) > 0.01f ? deflectVelocity : -atCrossing.velocity * 0.4f;
            _driver.Override(new BallState(atCrossing.position, v, atCrossing.spin * 0.5f));
        }

        private void BounceOffPost(in BallState s)
        {
            _frameBounced = true;
            audioDirector?.PlayPost();
            _impact.TriggerImpact(0.7f, 0.12f);
            cameraRig?.Shake(0.09f, 0.25f);

            float side = s.position.x >= 0f ? 1f : -1f;
            float3 v = s.velocity;
            v.x = math.abs(v.x) * side * 0.72f;
            v.z = -math.abs(v.z) * 0.55f;

            float3 pos = s.position;
            pos.x = side * (GoalFrame.PostCenterX - GoalFrame.PostRadius - BallRadius - 0.005f);
            _driver.Override(new BallState(pos, v, s.spin * 0.6f));
        }

        private void BounceOffCrossbar(in BallState s)
        {
            _frameBounced = true;
            audioDirector?.PlayPost();
            _impact.TriggerImpact(0.7f, 0.12f);
            cameraRig?.Shake(0.09f, 0.25f);

            float3 v = s.velocity;
            v.y = -math.abs(v.y) * 0.65f - 1.5f;
            v.z = -math.abs(v.z) * 0.5f;

            float3 pos = s.position;
            pos.y = GoalFrame.Height - BallRadius - 0.01f;
            _driver.Override(new BallState(pos, v, s.spin * 0.6f));
        }

        private void ApplyNetDragAndGround(in BallState s)
        {
            float3 pos = s.position;
            float3 vel = s.velocity;
            bool changed = false;

            // Trong lòng lưới: vải hãm bóng rất nhanh, đây là thứ làm cú sút "găm" chứ không xuyên qua.
            bool insideNet = pos.z >= GoalPlaneZ && pos.z <= GoalPlaneZ + 1.8f &&
                             math.abs(pos.x) <= GoalFrame.Width * 0.5f && pos.y <= GoalFrame.Height;
            if (insideNet)
            {
                vel *= 1f - 7.5f * BallDriver.SimDt;
                changed = true;
            }

            // Mặt cỏ.
            if (pos.y < BallRadius)
            {
                pos.y = BallRadius;
                if (vel.y < 0f) vel.y = -vel.y * 0.45f;
                vel.x *= 0.80f;
                vel.z *= 0.80f;
                changed = true;
            }

            if (changed) _driver.Override(new BallState(pos, vel, s.spin));
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  Nhập liệu người chơi
        // ═══════════════════════════════════════════════════════════════════════

        private void HandleAimBegin(Vector2 screenPos)
        {
            if (!IsPlayerTurn) return;

            if (_seq.Phase == KickPhase.Aiming)
            {
                UpdateAimLean(screenPos);
                _seq.ConfirmAim();          // chạm ngón = chốt ngắm = bắt đầu chạy đà
            }
        }

        private void HandleAimMove(Vector2 screenPos)
        {
            if (!IsPlayerTurn) return;
            if (_seq.Phase == KickPhase.Aiming || _seq.Phase == KickPhase.RunUp) UpdateAimLean(screenPos);
        }

        private void HandleSwipeCancelled()
        {
            // Vuốt hụt trong lúc chạy đà: vẫn phải sút, đà đã chạy rồi. Cú sút hỏng chân.
            if (_seq.Phase == KickPhase.RunUp && IsPlayerTurn && !_hasFired) FireScuffedShot();
        }

        private void HandleSwipeReleased(SwipeFeatures features, Vector2 screenPos)
        {
            if (!IsPlayerTurn || _hasFired) return;
            if (_seq.Phase != KickPhase.RunUp) return;

            // Chấm thời điểm nhả ngón so với khoảnh khắc chân chạm bóng (T15).
            TimingResult timing = TimingWindow.Evaluate(_seq.PhaseElapsed, IdealContactTime, _timingCfg);

            ShotIntent intent = swipeReceiver.BuildIntent(in features, screenPos, ActiveCamera,
                                                          timing.mappedErrorSeconds, NextKickSeed());

            scoreboard?.ShowTimingGrade(timing.grade, timing.ErrorMilliseconds);
            audioDirector?.PlayKick(math.saturate((intent.speed - 18f) / 18f));

            FireShot(intent);
        }

        /// <summary>
        /// Nghiêng thân người sút theo hướng ngón tay đang kéo. ĐÂY LÀ TÍN HIỆU thủ môn đọc.
        ///
        /// Dấu ÂM là cố ý: quy ước tín hiệu của T18 (đã bị test khoá) là "hông xoay dương →
        /// bóng đi sang cột 0 (x âm)" — đúng với cú má trong cứa ngang người, thân mở ngược
        /// hướng bóng. Đảo dấu ở đây thì thủ môn sẽ đổ đúng chiều; không đảo thì nó luôn đổ
        /// ngược, tệ hơn cả đoán mò.
        /// </summary>
        private void UpdateAimLean(Vector2 screenPos)
        {
            _currentAimPoint = TouchSwipeReceiver.AimPointFromScreen(screenPos, ActiveCamera);
            ApplyAimLean(_currentAimPoint);
        }

        private void ApplyAimLean(float3 aimPoint)
        {
            float lateral = math.clamp(aimPoint.x / (GoalFrame.Width * 0.5f), -1f, 1f);

            // Cả hai tín hiệu đều mang dấu NGƯỢC hướng bóng, đúng quy ước T18: sút chéo bằng
            // lòng bàn chân thì thân mở ra phía đối diện. Đó là cái tell thật của một quả 11m,
            // và cũng chính là thứ người chơi có thể cố tình làm giả.
            kicker?.SetAimYawDegrees(-lateral * 18f);
            _aimLateralShift = -lateral * 0.28f;
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  Bắn cú sút
        // ═══════════════════════════════════════════════════════════════════════

        private uint NextKickSeed()
        {
            _kickSeed = _kickSeed * 1664525u + 1013904223u;
            if (_kickSeed == 0u) _kickSeed = 1u;
            return _kickSeed;
        }

        private void FireShot(in ShotIntent intent)
        {
            _intent = intent;
            _hasFired = true;
            _shotLive = true;
            _crossingResolved = false;
            _outcomeReported = false;
            _frameBounced = false;
            _saveResult = SaveResult.Missed;
            _sinceCrossing = 0f;
            _strikeTimer = 0f;

            _launchVelocity = TouchSwipeReceiver.SolveLaunchVelocity(in intent, BallParams.Default);
            _lastLaunchVelocity = _launchVelocity;

            var launchState = new BallState(new float3(0f, BallRadius, 0f), _launchVelocity, intent.spin);
            _driver.Launch(in launchState);

            _seq.SetIntent(in intent);

            // Lớp hoạt ảnh NHẬN kiểu sút, không bao giờ tự quyết (định luật Phase 7).
            kicker?.PrepareFor(intent.type);
            if (swipeReceiver != null) swipeReceiver.IsInputEnabled = false;
            if (!IsPlayerTurn) audioDirector?.PlayKick(math.saturate((intent.speed - 18f) / 18f));
            _hasPendingAiIntent = false;

            goalkeeper?.OnContact();
            goalkeeper?.RememberShot(GoalFrame.CellOf(intent.aimPoint));

            ApplyTrailStyle(intent.type);
            scoreboard?.SetCurrentShotInfo(intent.type, intent.speed);
            scoreboard?.SetTimingBar(false, 0f, 0f, 0f, 0f);
            scoreboard?.SetPrompt(string.Empty);
            audioDirector?.SetCrowdTension(0.85f);

            // Dữ liệu phát lại: seed + intent là đủ để dựng lại nguyên cú sút (T27).
            _lastKickData = new ReplayKickData
            {
                seed = _kickSeed,
                intent = intent,
                expectedOutcome = ShotOutcome.Goal,
                expectedCrossing = intent.aimPoint,
                expectedCell = GoalGeometry.CellOf(intent.aimPoint)
            };
            _hasReplay = true;

            if (cameraRig != null) cameraRig.SetBallTracking(true);
        }

        /// <summary>Chạy hết đà mà không vuốt: chân chạm hụt, bóng đi yếu và lệch.</summary>
        private void FireScuffedShot()
        {
            uint seed = NextKickSeed();
            var rng = new Random(seed);

            var intent = new ShotIntent
            {
                aimPoint = new float3(rng.NextFloat(-1.2f, 1.2f), rng.NextFloat(0.3f, 0.9f), GoalPlaneZ),
                spin = float3.zero,
                speed = rng.NextFloat(11f, 15f),
                type = ShotType.Instep,
                quality = 0f,
                unstable = false,
                scatterRadius = 1.0f
            };

            audioDirector?.PlayKick(0.15f);
            scoreboard?.ShowTimingGrade(TimingGrade.Poor, _timingCfg.maxErrorSeconds * 1000f);
            FireShot(intent);
        }

        /// <summary>
        /// Ý đồ cú sút của máy. Sinh hoàn toàn từ seed để lượt của máy cũng phát lại được.
        /// </summary>
        private ShotIntent BuildAiIntent(uint seed)
        {
            var rng = new Random(seed);

            // Phân bố ngắm của máy. KHÔNG dồn hết vào hai góc dưới dù đó là chỗ khó cản nhất:
            // ngân sách tầm với (đo 2026-08-27) cho thấy ô 6 và ô 8 gần như không thể cản, nên
            // một đối thủ chỉ sút góc chết sẽ ghi bàn 100% và trận đấu mất sạch kịch tính.
            // Trọng số này để máy sút vào vùng cản được đủ thường xuyên để có pha cứu thua thật.
            int[] weights = { 5, 4, 5, 11, 9, 11, 12, 11, 12 };
            int total = 0;
            for (int i = 0; i < 9; i++) total += weights[i];
            int roll = rng.NextInt(0, total);
            int cell = 8;
            for (int i = 0; i < 9; i++)
            {
                roll -= weights[i];
                if (roll < 0) { cell = i; break; }
            }

            float3 c = GoalFrame.CellCenter(cell);
            float3 aim = new float3(
                c.x + rng.NextFloat(-0.62f, 0.62f),
                math.max(0.25f, c.y + rng.NextFloat(-0.42f, 0.42f)),
                GoalPlaneZ);

            bool knuckle = rng.NextFloat() < 0.18f;
            bool chip = rng.NextFloat() < 0.08f;
            float spinY = knuckle ? 0f : rng.NextFloat(-38f, 38f);

            var intent = new ShotIntent
            {
                aimPoint = aim,
                spin = new float3(0f, spinY, 0f),
                speed = chip ? 17f : rng.NextFloat(23f, 31f),
                type = chip ? ShotType.Chip : (knuckle ? ShotType.Knuckle : (math.abs(spinY) > 22f ? ShotType.InsideFoot : ShotType.Instep)),
                quality = rng.NextFloat(0.4f, 1f),
                unstable = knuckle,
                scatterRadius = 0.3f
            };

            return intent;
        }

        private void ApplyTrailStyle(ShotType type)
        {
            if (ballTrail == null) return;

            ballTrail.Clear();
            ballTrail.emitting = true;

            switch (type)
            {
                case ShotType.InsideFoot:
                    ballTrail.startColor = new Color(0.10f, 0.85f, 1.00f, 0.95f);
                    ballTrail.endColor = new Color(0.00f, 0.40f, 1.00f, 0f);
                    break;
                case ShotType.Knuckle:
                    ballTrail.startColor = new Color(1.00f, 0.90f, 0.10f, 0.95f);
                    ballTrail.endColor = new Color(1.00f, 0.45f, 0.00f, 0f);
                    break;
                case ShotType.Chip:
                    ballTrail.startColor = new Color(0.40f, 1.00f, 0.60f, 0.95f);
                    ballTrail.endColor = new Color(0.10f, 0.80f, 0.40f, 0f);
                    break;
                default:
                    ballTrail.startColor = new Color(1.00f, 0.35f, 0.15f, 0.95f);
                    ballTrail.endColor = new Color(1.00f, 0.10f, 0.00f, 0f);
                    break;
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  Phán kết quả
        // ═══════════════════════════════════════════════════════════════════════

        private void ReportOutcome()
        {
            _outcomeReported = true;

            bool keeperTouched = _saveResult != SaveResult.Missed;
            bool geometricGoal = _geomOutcome == ShotOutcome.Goal || _geomOutcome == ShotOutcome.PostIn;
            bool scored = geometricGoal && !keeperTouched;

            _lastResult = scored ? KickResult.Scored : KickResult.Missed;
            _seq.ReportOutcome(_lastResult);

            // Người sút chỉ được biết kết quả ở đây — không tự suy từ ý đồ sút của chính mình.
            kicker?.SetOutcome(_lastResult);
        }

        private void HandlePhaseChanged(KickPhase oldPhase, KickPhase newPhase)
        {
            // Báo cho lớp hoạt ảnh TRƯỚC các handler bên dưới: OnEnterRunUp chốt ý đồ sút,
            // mà clip sút phải được chọn xong trước khi pha RunUp chạy khung đầu tiên.
            kicker?.OnPhaseChanged(oldPhase, newPhase);

            switch (newPhase)
            {
                case KickPhase.Placing: OnEnterPlacing(); break;
                case KickPhase.Aiming: OnEnterAiming(); break;
                case KickPhase.RunUp: OnEnterRunUp(); break;
                case KickPhase.Contact: OnEnterContact(); break;
                case KickPhase.Flight: cameraRig?.SetShot(CameraShot.BehindShooter, 0.2f); break;
                case KickPhase.Resolution: OnEnterResolution(); break;
                case KickPhase.Reaction: OnEnterReaction(); break;
                case KickPhase.Complete: OnEnterComplete(); break;
            }
        }

        private void OnEnterPlacing()
        {
            _hasFired = false;
            _shotLive = false;
            _crossingResolved = false;
            _outcomeReported = false;
            _frameBounced = false;
            _strikeTimer = -1f;
            _saveResult = SaveResult.Missed;
            _geomOutcome = ShotOutcome.Short;
            _lastResult = KickResult.Pending;

            _aimLateralShift = 0f;
            _driver?.ResetTo(new float3(0f, BallRadius, 0f));
            if (ballTrail != null) { ballTrail.Clear(); ballTrail.emitting = false; }

            goalkeeper?.ResetToHome();
            kicker?.ResetToStart(new float3(0f, BallRadius, 0f));

            scoreboard?.HideBanner();
            scoreboard?.HideShotBadge();
            scoreboard?.SetTimingBar(false, 0f, 0f, 0f, 0f);
            scoreboard?.SetKeeperDebug(string.Empty);

            cameraRig?.SetBallTracking(false);
            cameraRig?.SetShot(CameraShot.BehindShooter, 0f);

            audioDirector?.SetCrowdTension(0.30f);
            RefreshScoreboard();
        }

        private void OnEnterAiming()
        {
            bool player = IsPlayerTurn;
            if (swipeReceiver != null) swipeReceiver.IsInputEnabled = player;
            scoreboard?.SetPrompt(player
                ? "CHẠM ĐỂ CHẠY ĐÀ — VUỐT VỀ PHÍA GÓC MUỐN SÚT — NHẢ ĐÚNG VÙNG XANH"
                : "ĐỐI THỦ ĐANG CHUẨN BỊ SÚT…");
            audioDirector?.SetCrowdTension(0.45f);
        }

        private void OnEnterRunUp()
        {
            cueSource?.StartRunUp();

            // Máy phải chốt ý đồ NGAY LÚC NÀY, không phải lúc chạm bóng. Chốt muộn thì suốt
            // đà chạy thân người không nghiêng đi đâu cả, thủ môn đọc ra một tín hiệu trung
            // tính, độ tin cậy tụt xuống ~0.08 và lượt nào nó cũng đứng giữa. Đo trên máy thật
            // 2026-08-27: đúng như vậy.
            if (!IsPlayerTurn)
            {
                _pendingAiIntent = BuildAiIntent(NextKickSeed());
                _hasPendingAiIntent = true;
                ApplyAimLean(_pendingAiIntent.aimPoint);

                // Máy biết kiểu sút từ đây, nên nó được đúng clip. Người chơi thì không —
                // cử chỉ chỉ ngã ngũ lúc nhả tay. Xem KickerClipSelector.PrepareFor.
                kicker?.PrepareFor(_pendingAiIntent.type);
            }

            audioDirector?.SetCrowdTension(0.70f);
            scoreboard?.SetPrompt(IsPlayerTurn ? "NHẢ NGÓN TAY ĐÚNG LÚC CHÂN CHẠM BÓNG!" : string.Empty);
        }

        private void OnEnterContact()
        {
            scoreboard?.SetTimingBar(false, 0f, 0f, 0f, 0f);
        }

        private void OnEnterResolution()
        {
            _shotLive = false;
            if (swipeReceiver != null) swipeReceiver.IsInputEnabled = false;
            scoreboard?.SetPrompt(string.Empty);

            // Chốt chặn cuối: ShootoutRules.ApplyKick nhận đúng Scored hoặc Missed. Để lọt một
            // giá trị Pending vào bảng luân lưu là hỏng cả luật đếm lượt.
            if (_lastResult == KickResult.Pending)
            {
                _lastResult = KickResult.Missed;
                _outcomeReported = true;
            }

            bool scored = _lastResult == KickResult.Scored;
            bool keeperTouched = _saveResult != SaveResult.Missed;

            // Tắt bám bóng TRƯỚC khi đổi góc. Bóng lúc này có thể đã bật ngược ra sau lưng
            // camera; để nguyên trọng số bám thì điểm nhìn bị kéo giật ra sau và khung hình
            // thành một mảng cỏ vô nghĩa — đo được trên máy thật 2026-08-27.
            cameraRig?.SetBallTracking(false);

            if (scored)
            {
                audioDirector?.PlayNet();
                audioDirector?.PlayCrowdRoar();
                _impact.TriggerImpact(0.85f, 0.15f);
                cameraRig?.Shake(0.12f, 0.35f);
                cameraRig?.SetShot(CameraShot.NetCam, 0.35f);
            }
            else
            {
                audioDirector?.PlayCrowdGroan();
                // Pha cứu thua xem từ góc thấp trên sân, KHÔNG xem từ mắt thủ môn: camera đứng
                // ngay chỗ thủ môn thì chính thủ môn che hết khung hình.
                cameraRig?.SetShot(keeperTouched ? CameraShot.LowAngle : CameraShot.Broadcast, 0.35f);
            }

            // Luật luân lưu là của T22 — ở đây chỉ nộp kết quả một lượt.
            _state = ShootoutRules.ApplyKick(in _state, _lastResult);
            saveLifecycle?.SetState(in _state);
            saveLifecycle?.SaveNow();
            RefreshScoreboard();
        }

        private void OnEnterReaction()
        {
            cameraRig?.SetShot(CameraShot.Broadcast, 0.5f);
            cameraRig?.SetBallTracking(false);
            ShowResultBanner();
        }

        private void OnEnterComplete()
        {
            if (_matchOver) return;
            BeginNextKick();
        }

        private void ShowResultBanner()
        {
            string typeName = _intent.type switch
            {
                ShotType.InsideFoot => "cú cứa lòng má trong",
                ShotType.Knuckle => "cú sút không xoáy",
                ShotType.Chip => "cú lốp bóng Panenka",
                _ => "cú nã mu bàn chân"
            };

            string who = _lastKickWasPlayer ? "BẠN" : "ĐỐI THỦ";

            if (_lastResult == KickResult.Scored)
            {
                scoreboard?.ShowBanner($"⚽ VÀO! — {who}",
                    $"{char.ToUpper(typeName[0]) + typeName.Substring(1)} găm thẳng vào lưới.",
                    new Color(0.20f, 0.95f, 0.35f), _hasReplay);
            }
            else if (_saveResult == SaveResult.Caught)
            {
                scoreboard?.ShowBanner($"🧤 BẮT DÍNH! — {who}", "Thủ môn đọc đúng hướng và ôm gọn bóng.",
                    new Color(1f, 0.55f, 0.15f), _hasReplay);
            }
            else if (_saveResult != SaveResult.Missed)
            {
                scoreboard?.ShowBanner($"🧤 CẢN PHÁ! — {who}", "Thủ môn chạm tay đẩy bóng ra.",
                    new Color(1f, 0.55f, 0.15f), _hasReplay);
            }
            else
            {
                string reason = _geomOutcome switch
                {
                    ShotOutcome.PostOut => "Bóng đập cột dọc bật ra ngoài.",
                    ShotOutcome.Crossbar => "Bóng dội xà ngang.",
                    ShotOutcome.WideLeft => "Bóng đi chệch cột trái.",
                    ShotOutcome.WideRight => "Bóng đi chệch cột phải.",
                    ShotOutcome.Over => "Bóng bay vọt xà.",
                    _ => "Cú sút quá nhẹ, bóng không tới khung thành."
                };
                scoreboard?.ShowBanner($"❌ HỎNG ĂN — {who}", reason, new Color(0.95f, 0.25f, 0.25f), _hasReplay);
            }

            CheckMatchOver();
        }

        private bool _lastKickWasPlayer;

        private void CheckMatchOver()
        {
            if (!ShootoutRules.IsDecided(in _state, out int winner)) return;

            _matchOver = true;
            bool playerWon = winner == 0;
            scoreboard?.ShowBanner(
                playerWon ? "🏆 BẠN THẮNG LOẠT LUÂN LƯU!" : "😢 BẠN THUA LOẠT LUÂN LƯU",
                $"Tỷ số chung cuộc {CountScored(true)} — {CountScored(false)}. Bấm LƯỢT TIẾP THEO để chơi trận mới.",
                playerWon ? new Color(0.2f, 0.95f, 0.35f) : new Color(0.95f, 0.25f, 0.25f),
                _hasReplay);

            if (playerWon) audioDirector?.PlayCrowdRoar();
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  Điều phối lượt
        // ═══════════════════════════════════════════════════════════════════════

        private void BeginNextKick()
        {
            if (_seq.Phase != KickPhase.Complete) return;

            _lastKickWasPlayer = _state.IsHomeTurn;
            _seq.BeginKick(NextKickSeed());
            UpdateTurnHud();
        }

        private void HandleNextKickPressed()
        {
            audioDirector?.PlayUiClick();

            if (_matchOver)
            {
                StartNewMatch();
                return;
            }

            if (_seq.Phase == KickPhase.Resolution || _seq.Phase == KickPhase.Reaction)
            {
                _seq.Abort();   // Abort đưa về Complete, handler Complete tự mở lượt kế tiếp
            }
        }

        private void StartNewMatch()
        {
            _matchOver = false;
            _state = default;
            _state.homeKicksFirst = true;
            goalkeeper?.ClearMemory();
            saveLifecycle?.SetState(in _state);
            saveLifecycle?.SaveNow();
            RefreshScoreboard();
            audioDirector?.PlayWhistle();

            if (_seq.Phase != KickPhase.Complete) _seq.Abort();
            else BeginNextKick();
        }

        private void HandleDifficultyChanged(DifficultyLevel level)
        {
            audioDirector?.PlayUiClick();
            _difficulty.Request(level);

            // Chỉ đổi giữa hai lượt: đổi giữa lúc bóng đang bay là đổi luật giữa chừng.
            if (_seq.Phase == KickPhase.Complete || _seq.Phase == KickPhase.Placing || _seq.Phase == KickPhase.Aiming)
            {
                if (_difficulty.CommitPending()) goalkeeper?.SetProfile(_difficulty.ActiveProfile);
            }
            scoreboard?.SetDifficulty(_difficulty.Pending);
        }

        private void UpdateTurnHud()
        {
            int taken = CountTaken(_state.IsHomeTurn);
            bool sudden = CountTaken(true) >= ShootoutRules.RegulationKicks &&
                          CountTaken(false) >= ShootoutRules.RegulationKicks;
            scoreboard?.SetTurn(_state.IsHomeTurn, taken + 1, sudden);
        }

        private void RefreshScoreboard()
        {
            _homeCache.Clear();
            _awayCache.Clear();
            for (int i = 0; i < _state.home.Length; i++) _homeCache.Add(_state.home[i]);
            for (int i = 0; i < _state.away.Length; i++) _awayCache.Add(_state.away[i]);

            scoreboard?.UpdateScores(_homeCache, _awayCache, _state.TotalKicksTaken);
            UpdateTurnHud();
        }

        private int CountScored(bool home)
        {
            int n = 0;
            if (home) { for (int i = 0; i < _state.home.Length; i++) if (_state.home[i] == KickResult.Scored) n++; }
            else { for (int i = 0; i < _state.away.Length; i++) if (_state.away[i] == KickResult.Scored) n++; }
            return n;
        }

        private int CountTaken(bool home) => home ? _state.home.Length : _state.away.Length;

        private void UpdateKeeperDebug()
        {
            if (scoreboard == null || goalkeeper == null) return;

            _debugText.Clear();
            _debugText.Append("keeper: ô ").Append(goalkeeper.Decision.targetCell)
                      .Append(" | tin cậy ").Append(goalkeeper.Confidence.ToString("F2"))
                      .Append(" | cam kết ").Append((-goalkeeper.Decision.commitTime * 1000f).ToString("F0")).Append("ms");
            if (_crossingResolved)
            {
                _debugText.Append(" | bóng vào ô ").Append(_crossingCell)
                          .Append(" | tay-bóng ")
                          .Append(goalkeeper.HandDistanceTo(_crossingPoint, _arrivalTime).ToString("F2")).Append('m')
                          .Append(" | ").Append(_saveResult.ToString());
            }
            scoreboard.SetKeeperDebug(_debugText.ToString());
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  Phát lại (T27)
        // ═══════════════════════════════════════════════════════════════════════

        private void PlayReplay()
        {
            if (!_hasReplay) return;

            audioDirector?.PlayUiClick();
            _replayPlayer ??= new ReplayPlayer();

            // Nạp kèm vận tốc phóng THẬT, nếu không quỹ đạo phát lại sẽ khác quỹ đạo vừa xem.
            _replayPlayer.LoadWithLaunch(in _lastKickData, _lastLaunchVelocity);
            _replayPlayer.SetPlaybackSpeed(0.35f);
            _replayPlayer.Play();

            _replayActive = true;
            _replayOrbitYaw = -35f;

            _driver?.Freeze();
            if (ballTrail != null) { ballTrail.Clear(); ballTrail.emitting = true; }

            cameraRig?.SetOrbit(_replayOrbitYaw, 16f, 4.4f, new float3(0f, 1.2f, GoalPlaneZ));
            cameraRig?.SetShot(CameraShot.ReplayOrbit, 0.4f);
            scoreboard?.ShowBanner("🎬 XEM LẠI — 0.35x", "Quỹ đạo được dựng lại từ seed, không phải video.",
                new Color(0.35f, 0.85f, 1f), false);
        }

        private void TickReplay(float dt)
        {
            _replayPlayer.Tick(dt);

            float3 p = _replayPlayer.CurrentBallState.position;
            if (ballTransform != null) ballTransform.position = (Vector3)p;

            // Máy quay lượn quanh trong giới hạn góc cứng của vùng đã dựng.
            _replayOrbitYaw = math.min(35f, _replayOrbitYaw + dt * 26f);
            cameraRig?.SetOrbit(_replayOrbitYaw, 16f, 4.4f, new float3(p.x * 0.5f, math.max(0.8f, p.y), math.min(GoalPlaneZ, p.z + 1.5f)));

            if (!_replayPlayer.IsPlaying || _replayPlayer.HasCompleted)
            {
                _replayActive = false;
                cameraRig?.SetShot(CameraShot.Broadcast, 0.4f);
                ShowResultBanner();
            }
        }
    }
}
