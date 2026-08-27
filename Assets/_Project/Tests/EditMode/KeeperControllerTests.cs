using NUnit.Framework;
using UnityEngine.TestTools.Constraints;
using Is = NUnit.Framework.Is;
using Unity.Collections;
using Unity.Mathematics;
using Eleven.Keeper;

namespace Eleven.Tests.EditMode
{
    [TestFixture]
    public sealed class KeeperControllerTests
    {
        // ── Helpers ────────────────────────────────────────────────

        private static KeeperRead MakeRead(int bestCell, float confidence)
        {
            var probs = new FixedList64Bytes<float>();
            for (int i = 0; i < 9; i++)
                probs.Add(i == bestCell ? confidence : (1f - confidence) / 8f);

            return new KeeperRead
            {
                cellProbabilities = probs,
                bestCell = bestCell,
                confidence = confidence
            };
        }

        private static KeeperProfile DefaultProfile()
        {
            return KeeperProfile.CreateDefault();
        }

        // ── 1. After Committed, targetCell does not change ─────────

        [Test]
        public void AfterCommitted_TargetCell_DoesNotChange()
        {
            var ctrl = new SimpleKeeperController();
            var profile = DefaultProfile();
            var read = MakeRead(bestCell: 2, confidence: 0.9f);

            bool committed = ctrl.TryCommit(in read, timeToContact: 0.1f, profile, out var decision);

            Assert.IsTrue(committed, "Should commit with high confidence");
            Assert.AreEqual(KeeperPhase.Committed, ctrl.Phase);

            int lockedCell = decision.targetCell;

            // Try calling TryCommit again with a different read — should return false, decision unchanged
            var read2 = MakeRead(bestCell: 6, confidence: 1.0f);
            bool secondCommit = ctrl.TryCommit(in read2, timeToContact: 0.05f, profile, out _);

            Assert.IsFalse(secondCommit, "Should not re-commit once Committed");
            Assert.AreEqual(lockedCell, ctrl.LockedDecision.targetCell, "Locked target cell must not change");
        }

        // ── 2. Low confidence → delays commitment, stays Reading ──

        [Test]
        public void LowConfidence_WithTimeRemaining_StaysReading()
        {
            var ctrl = new SimpleKeeperController();
            var profile = DefaultProfile();
            // Low confidence, plenty of time
            var read = MakeRead(bestCell: 0, confidence: 0.2f);
            float plentyOfTime = 2.0f; // seconds

            bool committed = ctrl.TryCommit(in read, plentyOfTime, profile, out _);

            Assert.IsFalse(committed, "Should delay commit when confidence is low and time remains");
            Assert.AreEqual(KeeperPhase.Reading, ctrl.Phase, "Should be in Reading phase");

            // Call again — still low confidence, still time
            committed = ctrl.TryCommit(in read, plentyOfTime - 0.1f, profile, out _);
            Assert.IsFalse(committed);
            Assert.AreEqual(KeeperPhase.Reading, ctrl.Phase, "Should remain in Reading");
        }

        // ── 3. Very low confidence + out of time → center cell (4) ─

        [Test]
        public void VeryLowConfidence_OutOfTime_ChoosesCenter()
        {
            var ctrl = new SimpleKeeperController();
            var profile = DefaultProfile();
            var read = MakeRead(bestCell: 8, confidence: 0.10f); // very low
            float almostNoTime = 0.01f; // essentially no time left

            bool committed = ctrl.TryCommit(in read, almostNoTime, profile, out var decision);

            Assert.IsTrue(committed, "Must commit when out of time");
            Assert.AreEqual(4, decision.targetCell, "Very low confidence + out of time → center cell 4");
            Assert.IsFalse(decision.isFullDive, "Center cell is not a full dive");
        }

        // ── 4. Deterministic: same input → same output from seed ───

        [Test]
        public void Deterministic_SameInput_SameOutput()
        {
            var profile = DefaultProfile();

            for (int trial = 0; trial < 50; trial++)
            {
                var ctrlA = new SimpleKeeperController();
                var ctrlB = new SimpleKeeperController();

                int bestCell = trial % 9;
                float conf = 0.3f + (trial % 7) * 0.1f;
                float ttc = 0.05f + (trial % 5) * 0.1f;

                var read = MakeRead(bestCell, math.clamp(conf, 0f, 1f));

                // First call (Set → Reading or commit)
                bool rA = ctrlA.TryCommit(in read, ttc, profile, out var dA);
                bool rB = ctrlB.TryCommit(in read, ttc, profile, out var dB);

                Assert.AreEqual(rA, rB, $"Trial {trial}: commit result mismatch");
                Assert.AreEqual(ctrlA.Phase, ctrlB.Phase, $"Trial {trial}: phase mismatch");

                if (rA)
                {
                    Assert.AreEqual(dA.targetCell, dB.targetCell, $"Trial {trial}: targetCell mismatch");
                    Assert.AreEqual(dA.isFullDive, dB.isFullDive, $"Trial {trial}: isFullDive mismatch");
                    Assert.AreEqual(dA.commitTime, dB.commitTime, $"Trial {trial}: commitTime mismatch");
                }
            }
        }

        // ── 5. 500 trials: physically plausible decisions ───────────

        [Test]
        public void FiveHundredTrials_CommitLogicConsistent()
        {
            var rng = new Unity.Mathematics.Random(42);
            var profile = DefaultProfile();

            for (int i = 0; i < 500; i++)
            {
                var ctrl = new SimpleKeeperController();

                int bestCell = rng.NextInt(0, 9);
                float conf = rng.NextFloat(0.0f, 1.0f);
                float ttc = rng.NextFloat(0.01f, 1.5f);

                var read = MakeRead(bestCell, conf);

                // Pump TryCommit — may need two calls (Set→Reading, then Reading→Committed)
                ctrl.TryCommit(in read, ttc + 0.5f, profile, out _);

                bool committed;
                DiveDecision decision;

                if (ctrl.Phase == KeeperPhase.Committed)
                {
                    committed = true;
                    decision = ctrl.LockedDecision;
                }
                else
                {
                    committed = ctrl.TryCommit(in read, 0.001f, profile, out decision);
                }

                if (committed)
                {
                    Assert.GreaterOrEqual(decision.targetCell, 0);
                    Assert.LessOrEqual(decision.targetCell, 8);
                }
            }
        }

        // ── 6. No Coroutine — state machine transitions are pure ───

        [Test]
        public void NoCoroutine_PureStateMachineTransitions()
        {
            var ctrl = new SimpleKeeperController();
            var profile = DefaultProfile();

            Assert.AreEqual(KeeperPhase.Set, ctrl.Phase);

            var read = MakeRead(bestCell: 5, confidence: 0.8f);
            bool committed = ctrl.TryCommit(in read, 0.05f, profile, out var decision);

            if (!committed)
            {
                Assert.AreEqual(KeeperPhase.Reading, ctrl.Phase);
                committed = ctrl.TryCommit(in read, 0.01f, profile, out decision);
            }

            Assert.IsTrue(committed);
            Assert.AreEqual(KeeperPhase.Committed, ctrl.Phase);

            ctrl.StartDive();
            Assert.AreEqual(KeeperPhase.Diving, ctrl.Phase);

            ctrl.Recover();
            Assert.AreEqual(KeeperPhase.Recovering, ctrl.Phase);

            ctrl.Reset();
            Assert.AreEqual(KeeperPhase.Set, ctrl.Phase);
        }

        // ── Additional: Full dive flag correctness ─────────────────

        [Test]
        public void FullDiveFlag_CorrectForAllCells()
        {
            // Cells 0,2,3,5,6,8 → isFullDive = true
            // Cells 1,4,7 → isFullDive = false
            bool[] expectedFullDive = { true, false, true, true, false, true, true, false, true };
            var profile = DefaultProfile();

            for (int cell = 0; cell < 9; cell++)
            {
                var ctrl = new SimpleKeeperController();
                var read = MakeRead(bestCell: cell, confidence: 0.95f);

                bool committed = ctrl.TryCommit(in read, 0.01f, profile, out var decision);
                if (!committed)
                    committed = ctrl.TryCommit(in read, 0.001f, profile, out decision);

                Assert.IsTrue(committed, $"Cell {cell}: should commit");
                Assert.AreEqual(cell, decision.targetCell, $"Cell {cell}: targetCell mismatch");
                Assert.AreEqual(expectedFullDive[cell], decision.isFullDive,
                    $"Cell {cell}: isFullDive should be {expectedFullDive[cell]}");
            }
        }

        // ── State transition guards ────────────────────────────────

        [Test]
        public void StartDive_OnlyWorksFromCommitted()
        {
            var ctrl = new SimpleKeeperController();

            // From Set — should not transition
            ctrl.StartDive();
            Assert.AreEqual(KeeperPhase.Set, ctrl.Phase);

            // Get to Committed
            var profile = DefaultProfile();
            var read = MakeRead(3, 0.9f);
            ctrl.TryCommit(in read, 0.01f, profile, out _);
            if (ctrl.Phase == KeeperPhase.Reading)
                ctrl.TryCommit(in read, 0.001f, profile, out _);

            Assert.AreEqual(KeeperPhase.Committed, ctrl.Phase);

            ctrl.StartDive();
            Assert.AreEqual(KeeperPhase.Diving, ctrl.Phase);

            // StartDive again from Diving — should not change
            ctrl.StartDive();
            Assert.AreEqual(KeeperPhase.Diving, ctrl.Phase);
        }

        [Test]
        public void Recover_OnlyWorksFromDiving()
        {
            var ctrl = new SimpleKeeperController();
            var profile = DefaultProfile();
            var read = MakeRead(0, 0.95f);

            ctrl.TryCommit(in read, 0.01f, profile, out _);
            if (ctrl.Phase == KeeperPhase.Reading)
                ctrl.TryCommit(in read, 0.001f, profile, out _);

            // Recover from Committed — should not work
            ctrl.Recover();
            Assert.AreEqual(KeeperPhase.Committed, ctrl.Phase);

            ctrl.StartDive();
            ctrl.Recover();
            Assert.AreEqual(KeeperPhase.Recovering, ctrl.Phase);

            // Recover again — should not change
            ctrl.Recover();
            Assert.AreEqual(KeeperPhase.Recovering, ctrl.Phase);
        }

        [Test]
        public void Reset_WorksFromAnyPhase()
        {
            var profile = DefaultProfile();
            var read = MakeRead(7, 0.9f);

            KeeperPhase[] phasesToTest = {
                KeeperPhase.Set, KeeperPhase.Reading, KeeperPhase.Committed,
                KeeperPhase.Diving, KeeperPhase.Recovering
            };

            foreach (var targetPhase in phasesToTest)
            {
                var ctrl = new SimpleKeeperController();

                switch (targetPhase)
                {
                    case KeeperPhase.Set:
                        break;
                    case KeeperPhase.Reading:
                        ctrl.TryCommit(MakeRead(1, 0.1f), 5.0f, profile, out _);
                        break;
                    case KeeperPhase.Committed:
                        ctrl.TryCommit(in read, 0.01f, profile, out _);
                        if (ctrl.Phase == KeeperPhase.Reading)
                            ctrl.TryCommit(in read, 0.001f, profile, out _);
                        break;
                    case KeeperPhase.Diving:
                        ctrl.TryCommit(in read, 0.01f, profile, out _);
                        if (ctrl.Phase == KeeperPhase.Reading)
                            ctrl.TryCommit(in read, 0.001f, profile, out _);
                        ctrl.StartDive();
                        break;
                    case KeeperPhase.Recovering:
                        ctrl.TryCommit(in read, 0.01f, profile, out _);
                        if (ctrl.Phase == KeeperPhase.Reading)
                            ctrl.TryCommit(in read, 0.001f, profile, out _);
                        ctrl.StartDive();
                        ctrl.Recover();
                        break;
                }

                Assert.AreEqual(targetPhase, ctrl.Phase, $"Failed to reach phase {targetPhase}");

                ctrl.Reset();
                Assert.AreEqual(KeeperPhase.Set, ctrl.Phase, $"Reset from {targetPhase} should return to Set");
            }
        }
    }
}
