namespace Eleven.Match
{
    /// <summary>
    /// Máy trạng thái của MỘT lượt sút (T23). Thuần C#: không MonoBehaviour, không đọc
    /// <c>Time.deltaTime</c>, không <c>UnityEngine.Random</c> — người gọi bơm thời gian vào
    /// bằng <see cref="Tick"/>. Nhờ vậy 200 lượt chạy trong một EditMode test cũng cho ra
    /// đúng chuỗi pha như khi chạy trên máy thật.
    ///
    /// Hai điểm dễ sai đã được xử lý dứt điểm, xem chú thích tại chỗ:
    ///   1. <see cref="Tick"/> GIỮ phần dư thời gian khi vượt qua ranh giới pha, và bắn
    ///      sự kiện cho TỪNG bước — một <c>Tick(100f)</c> không được nhảy thẳng tới Complete.
    ///   2. <see cref="Restore"/> không "bay tiếp" quả bóng mà TUA LẠI về mốc gần nhất còn
    ///      tái hiện được bằng seed + intent.
    ///
    /// Không cấp phát GC sau khi khởi tạo: mọi trạng thái là trường giá trị, sự kiện được
    /// gọi qua biến cục bộ (không lambda, không LINQ, không <c>List</c>).
    /// </summary>
    public sealed class KickSequencer : IKickSequencer
    {
        private KickPhase _phase;
        private uint _currentSeed;
        private float _phaseElapsed;
        private KickResult _outcome;
        private Eleven.Shooter.ShotIntent _intent;
        private bool _hasIntent;
        private KickPhaseDurations _durations;

        // Chặn Tick lồng nhau: một handler OnPhaseChanged gọi lại Tick sẽ làm hai vòng lặp
        // cùng trừ _phaseElapsed và bắn sự kiện chồng chéo.
        private bool _ticking;

        public KickPhase Phase
        {
            get { return _phase; }
        }

        public event System.Action<KickPhase, KickPhase> OnPhaseChanged;

        /// <summary>Seed của lượt hiện tại. <see cref="Abort"/> KHÔNG xoá nó, để còn ghi log lượt huỷ.</summary>
        public uint CurrentSeed
        {
            get { return _currentSeed; }
        }

        /// <summary>Thời gian đã trôi trong pha hiện tại (giây).</summary>
        public float PhaseElapsed
        {
            get { return _phaseElapsed; }
        }

        /// <summary>
        /// Kết quả lượt. Chỉ đổi khi có người gọi <see cref="ReportOutcome"/>; sequencer
        /// KHÔNG bao giờ tự suy ra Scored/Missed — nó không biết gì về quả bóng.
        /// </summary>
        public KickResult Outcome
        {
            get { return _outcome; }
        }

        public Eleven.Shooter.ShotIntent Intent
        {
            get { return _intent; }
        }

        public bool HasIntent
        {
            get { return _hasIntent; }
        }

        public KickPhaseDurations Durations
        {
            get { return _durations; }
            set { _durations = value; }
        }

        public KickSequencer()
        {
            // Trạng thái nghỉ chính là Complete — xem chú thích ở KickPhase.
            _phase = KickPhase.Complete;
            _currentSeed = 0;
            _phaseElapsed = 0f;
            _outcome = KickResult.Pending;
            _hasIntent = false;
            _durations = KickPhaseDurations.Default;
        }

        /// <summary>
        /// Mở một lượt mới với <paramref name="seed"/>. Đang giữa lượt thì bỏ qua hoàn toàn:
        /// không đổi trạng thái, không bắn sự kiện, không ném exception — gọi nhầm hai lần
        /// không được phép làm hỏng lượt đang chạy.
        /// </summary>
        public void BeginKick(uint seed)
        {
            if (_phase != KickPhase.Complete)
            {
                return;
            }

            _currentSeed = seed;
            _phaseElapsed = 0f;
            _outcome = KickResult.Pending;
            _hasIntent = false;
            ChangePhase(KickPhase.Placing);
        }

        /// <summary>
        /// Huỷ lượt: về Complete sạch sẽ từ BẤT KỲ pha nào. Ở Complete sẵn rồi thì no-op
        /// và KHÔNG bắn sự kiện.
        /// </summary>
        public void Abort()
        {
            if (_phase == KickPhase.Complete)
            {
                return;
            }

            KickPhase previous = _phase;

            // Dọn sạch TRƯỚC rồi mới bắn sự kiện: handler có quyền gọi BeginKick ngay trong
            // OnPhaseChanged để nối lượt kế tiếp, nên khi nó chạy thì mọi trường phải đã ở
            // trạng thái cuối. Nếu bắn trước rồi mới gán, cú BeginKick đó sẽ bị ghi đè.
            // CurrentSeed cố ý giữ nguyên.
            _phase = KickPhase.Complete;
            _phaseElapsed = 0f;
            _outcome = KickResult.Pending;
            _hasIntent = false;

            InvokePhaseChanged(previous, KickPhase.Complete);
        }

        /// <summary>
        /// Bơm <paramref name="deltaTime"/> giây vào lượt. deltaTime &lt;= 0 hoặc NaN: không làm gì.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f || float.IsNaN(deltaTime) || _phase == KickPhase.Complete || _ticking)
            {
                return;
            }

            _ticking = true;
            try
            {
                _phaseElapsed += deltaTime;

                while (_phase != KickPhase.Complete)
                {
                    float duration = _durations.For(_phase);

                    if (duration > 0f && _phaseElapsed < duration)
                    {
                        break;
                    }

                    // TRỪ đúng thời lượng pha thay vì gán 0: một Tick lớn (khung hình rớt,
                    // hoặc test tua nhanh) phải đẩy được phần dư sang pha sau, nếu không
                    // nhịp trận sẽ trôi chậm dần theo số lần rớt khung.
                    // Thời lượng <= 0 thì vẫn tiến đúng MỘT bước mỗi vòng — chống lặp vô hạn.
                    if (duration > 0f)
                    {
                        _phaseElapsed -= duration;
                    }

                    KickPhase expected = NextPhase(_phase);
                    ChangePhase(expected);

                    // Handler vừa can thiệp (BeginKick/Abort/Restore) — nhường quyền cho nó,
                    // đừng lấy phần dư của lượt cũ tua tiếp lượt mới.
                    if (_phase != expected)
                    {
                        break;
                    }

                    if (_phase == KickPhase.Complete)
                    {
                        _phaseElapsed = 0f;
                        break;
                    }
                }
            }
            finally
            {
                _ticking = false;
            }
        }

        /// <summary>Người chơi chốt hướng ngắm. Chỉ có tác dụng ở <see cref="KickPhase.Aiming"/>.</summary>
        public void ConfirmAim()
        {
            if (_phase != KickPhase.Aiming)
            {
                return;
            }

            _phaseElapsed = 0f;
            ChangePhase(KickPhase.RunUp);
        }

        /// <summary>
        /// Ghi lại thông số cú sút. Chỉ nhận ở RunUp/Contact/Flight — trước RunUp thì chưa
        /// có gì để ghi, sau Flight thì bóng đã bay xong, sửa nữa là gian lận.
        /// </summary>
        public void SetIntent(in Eleven.Shooter.ShotIntent intent)
        {
            if (_phase != KickPhase.RunUp &&
                _phase != KickPhase.Contact &&
                _phase != KickPhase.Flight)
            {
                return;
            }

            _intent = intent;
            _hasIntent = true;
        }

        /// <summary>
        /// Báo kết quả từ tầng vật lý. Ở Flight thì đồng thời tiến sang Resolution;
        /// ở Resolution thì chỉ ghi đè kết quả (ví dụ bóng bật cột rồi mới vào).
        /// Pha khác: no-op.
        /// </summary>
        public void ReportOutcome(KickResult result)
        {
            if (_phase != KickPhase.Flight && _phase != KickPhase.Resolution)
            {
                return;
            }

            _outcome = result;

            if (_phase == KickPhase.Flight)
            {
                _phaseElapsed = 0f;
                ChangePhase(KickPhase.Resolution);
            }
        }

        /// <summary>Chụp nguyên trạng để ghi vào bản lưu.</summary>
        public KickSequencerSnapshot Capture()
        {
            KickSequencerSnapshot snapshot;
            snapshot.phase = (byte)_phase;
            snapshot.seed = _currentSeed;
            snapshot.phaseElapsed = _phaseElapsed;
            snapshot.outcome = (byte)_outcome;
            snapshot.hasIntent = _hasIntent;
            snapshot.intent = _intent;
            return snapshot;
        }

        /// <summary>
        /// Khôi phục sau khi app bị hệ điều hành giết giữa lượt.
        ///
        /// VÌ SAO KHÔNG KHÔI PHỤC ĐÚNG PHA CŨ: trạng thái quả bóng (vị trí, vận tốc, xoáy tại
        /// thời điểm bị giết) KHÔNG nằm trong sequencer, nên "bay tiếp từ giữa Flight" là bất
        /// khả thi. Thay vào đó tua về mốc gần nhất mà mô phỏng tất định còn tái hiện được:
        ///   - {Placing, Aiming, RunUp}: chưa cam kết cú sút nào → về Placing, cùng seed,
        ///     người chơi đá lại y hệt lượt đó.
        ///   - {Contact, Flight}: đã cam kết, thông số nằm trong snapshot.intent → về đầu
        ///     Flight và mô phỏng lại, cùng seed + cùng intent cho ra đúng đường bóng cũ.
        ///   - {Resolution, Reaction}: kết quả đã chốt → giữ kết quả, về Resolution.
        ///     Nhưng nếu outcome vẫn là Pending thì kết quả CHƯA từng được ghi, phải coi như
        ///     nhánh Flight mà đá lại, chứ không được bịa ra một kết quả.
        ///
        /// File lưu có thể bị sửa tay hoặc hỏng: byte ngoài dải được coi là Complete/Pending,
        /// tuyệt đối không ném exception (crash lúc mở lại app là hỏng cả bản lưu).
        /// </summary>
        public void Restore(in KickSequencerSnapshot snapshot)
        {
            KickPhase restoredPhase = IsValidPhase(snapshot.phase)
                ? (KickPhase)snapshot.phase
                : KickPhase.Complete;

            KickResult restoredOutcome = IsValidOutcome(snapshot.outcome)
                ? (KickResult)snapshot.outcome
                : KickResult.Pending;

            KickPhase targetPhase;
            bool restoreIntent;

            switch (restoredPhase)
            {
                case KickPhase.Placing:
                case KickPhase.Aiming:
                case KickPhase.RunUp:
                    targetPhase = KickPhase.Placing;
                    restoreIntent = false;
                    restoredOutcome = KickResult.Pending;
                    break;

                case KickPhase.Contact:
                case KickPhase.Flight:
                    targetPhase = KickPhase.Flight;
                    restoreIntent = true;
                    restoredOutcome = KickResult.Pending;
                    break;

                case KickPhase.Resolution:
                case KickPhase.Reaction:
                    if (restoredOutcome == KickResult.Pending)
                    {
                        targetPhase = KickPhase.Flight;
                        restoreIntent = true;
                    }
                    else
                    {
                        targetPhase = KickPhase.Resolution;
                        restoreIntent = true;
                    }
                    break;

                default:
                    targetPhase = KickPhase.Complete;
                    restoreIntent = false;
                    restoredOutcome = KickResult.Pending;
                    break;
            }

            KickPhase previous = _phase;

            _currentSeed = snapshot.seed;
            _phase = targetPhase;
            _phaseElapsed = 0f;
            _outcome = restoredOutcome;
            _hasIntent = restoreIntent && snapshot.hasIntent;

            if (restoreIntent)
            {
                _intent = snapshot.intent;
            }

            if (previous != targetPhase)
            {
                InvokePhaseChanged(previous, targetPhase);
            }
        }

        /// <summary>Bảng chuyển pha DUY NHẤT. Mọi đường đi đều qua đây nên không thể nhảy cóc.</summary>
        private static KickPhase NextPhase(KickPhase phase)
        {
            switch (phase)
            {
                case KickPhase.Placing:
                    return KickPhase.Aiming;
                case KickPhase.Aiming:
                    return KickPhase.RunUp;
                case KickPhase.RunUp:
                    return KickPhase.Contact;
                case KickPhase.Contact:
                    return KickPhase.Flight;
                case KickPhase.Flight:
                    return KickPhase.Resolution;
                case KickPhase.Resolution:
                    return KickPhase.Reaction;
                default:
                    return KickPhase.Complete;
            }
        }

        private static bool IsValidPhase(byte phase)
        {
            return phase <= (byte)KickPhase.Complete;
        }

        private static bool IsValidOutcome(byte outcome)
        {
            return outcome <= (byte)KickResult.Missed;
        }

        private void ChangePhase(KickPhase next)
        {
            KickPhase previous = _phase;
            _phase = next;
            InvokePhaseChanged(previous, next);
        }

        private void InvokePhaseChanged(KickPhase previous, KickPhase next)
        {
            // Đọc ra biến cục bộ trước khi gọi: handler có thể tự huỷ đăng ký ngay trong
            // lúc chạy. Không dùng ?.Invoke để giữ nguyên phong cách không cấp phát.
            System.Action<KickPhase, KickPhase> handler = OnPhaseChanged;
            if (handler != null)
            {
                handler(previous, next);
            }
        }
    }
}
