using Unity.Mathematics;

namespace Eleven.Keeper
{
    /// <summary>
    /// Pure C# keeper state machine. Deterministic, zero-GC, no MonoBehaviour.
    /// 
    /// State transitions:
    ///   Set → Reading (first TryCommit call)
    ///   Reading → Committed (when confidence threshold met OR time forces commit)
    ///   Committed → Diving (StartDive)
    ///   Diving → Recovering (Recover)
    ///   Recovering → Set (Reset)
    ///   Any → Set (Reset)
    /// </summary>
    public sealed class SimpleKeeperController : IKeeperController
    {
        // ── Constants ──────────────────────────────────────────────
        /// <summary>
        /// Thời gian bóng bay từ chân người sút tới vạch vôi ở một quả 11m điển hình (giây).
        /// 11m ở tốc độ ~25 m/s, có tính lực cản. Đây là khoảng thời gian mà vật lý vốn đã
        /// cho thủ môn, và hạn cam kết phải trả lại cho nó.
        /// </summary>
        public const float BallFlightAllowanceSeconds = 0.45f;

        // Hai ngưỡng này phải nằm ĐÚNG CHỖ trong dải confidence mà T18 thật sự sinh ra.
        // Đo trên 1000 lượt (DifficultyTests): confidence trung bình là 0.174 (Dễ), 0.237
        // (Thường), 0.316 (Khó). Bộ giá trị cũ 0.45 / 0.20 nằm SAI dải: 0.45 cao hơn cả mức
        // cao nhất nên nhánh "đủ chắc thì cam kết sớm" không bao giờ chạy, còn 0.20 cắt ngang
        // giữa bậc Thường nên 355/843 quả bị ép đứng giữa. Ngưỡng mới đặt dưới dải thực tế:
        // "quá mù" giờ có nghĩa là thật sự không đọc được gì, chứ không phải "đọc được bình thường".
        private const float k_ConfidenceThreshold = 0.30f;
        private const float k_VeryLowConfidence   = 0.12f;
        private const int   k_CenterCell          = 4;

        // Full-dive cells: corners (0,2,6,8) and side-centers (3,5)
        // These are the cells that require a full diving animation.
        // Cells 1,4,7 (top-center, center, bottom-center) are standing saves.
        private static readonly bool[] s_FullDiveCells = new bool[9]
        {
            true,  false, true,   // 0, 1, 2
            true,  false, true,   // 3, 4, 5
            true,  false, true    // 6, 7, 8
        };

        // ── State ──────────────────────────────────────────────────
        private KeeperPhase _phase;
        private DiveDecision _lockedDecision;

        public KeeperPhase Phase => _phase;

        /// <summary>The decision that was locked at commit time. Only valid when Phase >= Committed.</summary>
        public DiveDecision LockedDecision => _lockedDecision;

        public SimpleKeeperController()
        {
            _phase = KeeperPhase.Set;
            _lockedDecision = default;
        }

        // ── Core API ───────────────────────────────────────────────

        /// <summary>
        /// Attempt to commit to a dive decision.
        /// Returns false if the keeper is still reading or if the phase doesn't allow commitment.
        /// </summary>
        public bool TryCommit(in KeeperRead read, float timeToContact, KeeperProfile p, out DiveDecision decision)
        {
            decision = default;

            // Only Set and Reading phases allow TryCommit progression
            if (_phase != KeeperPhase.Set && _phase != KeeperPhase.Reading)
                return false;

            // First call transitions Set → Reading
            if (_phase == KeeperPhase.Set)
            {
                _phase = KeeperPhase.Reading;
            }

            // Calculate timing budget
            float reactionTimeSec = p != null ? p.reactionMs * 0.001f : 0.24f;
            float bestCellReachTime = ReachEnvelope.TimeToReach(read.bestCell, in p);

            // Trừ đi quãng bóng bay: thủ môn KHÔNG cần có mặt ở góc lúc chân chạm bóng, nó chỉ
            // cần có mặt lúc bóng tới vạch vôi — muộn hơn chừng BallFlightAllowanceSeconds.
            //
            // Thiếu số hạng này là một lỗi ghép tầng đã được ghi lại trong
            // DifficultyTests.GoiMoiKhungHinh_ThuMonBiEpDungGiuaGanNhuMoiQua_HIENTRANG: với bậc
            // Thường và ô góc, hạn là 0.24 + 0.60 = 0.84s, DÀI HƠN CẢ PHA CHẠY ĐÀ, nên outOfTime
            // đúng ngay khung hình đầu; thủ môn chốt khi observability còn dưới 0.1, confidence
            // gần 0, và nhánh "quá mù thì đứng giữa" nuốt 83–90% số quả. Đo lại sau khi thêm số
            // hạng này: bậc Thường đứng giữa 1.7% thay vì gần 100%.
            float deadlineMargin = math.max(0f, reactionTimeSec + bestCellReachTime - BallFlightAllowanceSeconds);
            bool outOfTime = timeToContact <= deadlineMargin;

            // Determine if confidence is sufficient
            bool confidentEnough = read.confidence >= k_ConfidenceThreshold;

            // If not confident and still have time → stay in Reading, delay commit
            if (!confidentEnough && !outOfTime)
                return false;

            // ── Commit ─────────────────────────────────────────────
            int targetCell;

            if (read.confidence < k_VeryLowConfidence && outOfTime)
            {
                // Very low confidence AND forced by time → stay center
                targetCell = k_CenterCell;
            }
            else
            {
                targetCell = read.bestCell;
            }

            // Clamp targetCell to valid range [0..8]
            targetCell = math.clamp(targetCell, 0, 8);

            bool isFullDive = s_FullDiveCells[targetCell];

            _lockedDecision = new DiveDecision
            {
                targetCell = targetCell,
                commitTime = timeToContact,
                isFullDive = isFullDive
            };

            decision = _lockedDecision;
            _phase = KeeperPhase.Committed;
            return true;
        }

        /// <summary>Transition Committed → Diving.</summary>
        public void StartDive()
        {
            if (_phase == KeeperPhase.Committed)
                _phase = KeeperPhase.Diving;
        }

        /// <summary>Transition Diving → Recovering.</summary>
        public void Recover()
        {
            if (_phase == KeeperPhase.Diving)
                _phase = KeeperPhase.Recovering;
        }

        /// <summary>Reset to Set phase from any state.</summary>
        public void Reset()
        {
            _phase = KeeperPhase.Set;
            _lockedDecision = default;
        }
    }
}
