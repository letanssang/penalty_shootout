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
        private const float k_ConfidenceThreshold = 0.45f;
        private const float k_VeryLowConfidence   = 0.20f;
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
            float deadlineMargin = reactionTimeSec + bestCellReachTime;
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
