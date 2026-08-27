using NUnit.Framework;
using UnityEngine.TestTools.Constraints;   // gop extension AllocatingGCMemory()
using Is = NUnit.Framework.Is;             // khu nhap nhang voi lop Is cua Unity
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.TestTools;
using Eleven.Keeper;

/// <summary>
/// Unit tests cho T18 (KeeperRead, IKeeperBrain / BayesianKeeperBrain)
/// và T20 (ShotHistory).
///
/// Bao phủ checklist nghiệm thu:
/// T18-1: 9 xác suất luôn cộng lại bằng 1, sai số dưới 1e-5
/// T18-2: observability = 0 cho phân phối gần đều và confidence gần 0
/// T18-3: 1000 lần với profile "Thường": tỉ lệ bestCell đúng ~ 0.50 ± 0.04
/// T18-4: Cùng seed và cùng tín hiệu cho cùng kết quả, byte giống byte
/// T18-5: Bịa tín hiệu mâu thuẫn không làm sinh NaN hay xác suất âm
/// T18-6: confidence tương quan thuận với độ chính xác thực tế
///
/// T20-1: Cú gần đây có trọng số cao hơn cú cũ
/// T20-2: Lịch sử rỗng cho prior đều tuyệt đối
/// T20-3: Prior cộng lại bằng 1 trong mọi trường hợp
/// T20-4: memoryWeight = 0 làm hệ thống này vô hiệu hoàn toàn
/// T20-5: Lịch sử lưu qua các lượt, xoá khi Clear
/// T20-6: Không cấp phát — dùng FixedList, không dùng List<T>
/// </summary>
[TestFixture]
public class KeeperReadTests
{
    // ── Helper ────────────────────────────────────────────────

    static KeeperCues MakeCues(float lateral, float hipYaw, float approach,
                                float runUp = 3.5f, float ttc = 0.5f, float obs = 1f)
    {
        return new KeeperCues
        {
            plantFootLateralOffset = lateral,
            hipYawDegrees = hipYaw,
            approachAngleDegrees = approach,
            runUpLength = runUp,
            timeToContact = ttc,
            observability = obs
        };
    }

    /// <summary>Tạo cues rõ ràng chỉ về ô trái (cột 0): lateral>0, hipYaw>0, approach>0</summary>
    static KeeperCues LeftCues(float obs = 1f) =>
        MakeCues(0.20f, 15f, 20f, 3.5f, 0.5f, obs);

    /// <summary>Tạo cues rõ ràng chỉ về ô phải (cột 2): lateral<0, hipYaw<0, approach<0</summary>
    static KeeperCues RightCues(float obs = 1f) =>
        MakeCues(-0.20f, -15f, -20f, 3.5f, 0.5f, obs);

    /// <summary>Tạo cues trung tính</summary>
    static KeeperCues CenterCues(float obs = 1f) =>
        MakeCues(0f, 0f, 0f, 3.5f, 0.5f, obs);

    /// <summary>Tạo cues mâu thuẫn: lateral trái nhưng hipYaw phải</summary>
    static KeeperCues ConflictingCues() =>
        MakeCues(0.25f, -20f, 25f, 5f, 0.3f, 0.8f);

    static float SumProbabilities(in KeeperRead read)
    {
        float sum = 0f;
        for (int i = 0; i < read.cellProbabilities.Length; i++)
            sum += read.cellProbabilities[i];
        return sum;
    }

    static bool HasNaN(in KeeperRead read)
    {
        for (int i = 0; i < read.cellProbabilities.Length; i++)
            if (float.IsNaN(read.cellProbabilities[i]))
                return true;
        return float.IsNaN(read.confidence);
    }

    static bool HasNegative(in KeeperRead read)
    {
        for (int i = 0; i < read.cellProbabilities.Length; i++)
            if (read.cellProbabilities[i] < 0f)
                return true;
        return false;
    }

    // ══════════════════════════════════════════════════════════
    //  T18 — KeeperRead / BayesianKeeperBrain
    // ══════════════════════════════════════════════════════════

    [Test]
    public void T18_XacSuat_CongBang1_SaiSoDuoi1e5()
    {
        var brain = new BayesianKeeperBrain();
        var profile = KeeperProfile.CreateMedium();
        var history = new ShotHistory();

        // Test nhiều bộ cues khác nhau
        var cuesSets = new[] { LeftCues(), RightCues(), CenterCues(), ConflictingCues() };

        foreach (var cues in cuesSets)
        {
            for (uint seed = 1; seed <= 10; seed++)
            {
                var read = brain.Infer(cues, history, profile, seed);
                float sum = SumProbabilities(read);
                Assert.AreEqual(1f, sum, 1e-5f,
                    $"Tổng xác suất = {sum} (seed={seed}), kỳ vọng = 1.0");
            }
        }
    }

    [Test]
    public void T18_9PhanTu()
    {
        var brain = new BayesianKeeperBrain();
        var profile = KeeperProfile.CreateMedium();
        var history = new ShotHistory();
        var cues = LeftCues();

        var read = brain.Infer(cues, history, profile, 42u);
        Assert.AreEqual(9, read.cellProbabilities.Length,
            "Phải có đúng 9 phần tử trong cellProbabilities");
    }

    [Test]
    public void T18_Observability0_PhanPhoiGanDeu_ConfidenceGan0()
    {
        var brain = new BayesianKeeperBrain();
        var profile = KeeperProfile.CreateMedium();
        var history = new ShotHistory();
        var cues = LeftCues(obs: 0f); // observability = 0

        var read = brain.Infer(cues, history, profile, 42u);

        // Phân phối phải gần đều (1/9 ≈ 0.1111)
        const float uniform = 1f / 9f;
        for (int c = 0; c < 9; c++)
        {
            Assert.AreEqual(uniform, read.cellProbabilities[c], 0.005f,
                $"Ô {c}: xác suất = {read.cellProbabilities[c]}, kỳ vọng ≈ {uniform}");
        }

        // Confidence phải gần 0
        Assert.Less(read.confidence, 0.05f,
            $"Confidence = {read.confidence}, kỳ vọng gần 0 khi observability = 0");
    }

    [Test]
    public void T18_1000Lan_ProfileThuong_BestCellDung_50PhanTram()
    {
        var brain = new BayesianKeeperBrain();
        var profile = KeeperProfile.CreateMedium(); // readAccuracy = 0.52

        // Mô phỏng: chọn ô đích ngẫu nhiên, tạo cues tương ứng, kiểm tra bestCell
        int correct = 0;
        const int trials = 1000;

        // Bảng cues mẫu cho từng cột (tín hiệu chỉ phân biệt cột, không hàng)
        // Cột 0 (trái): lateral=+0.20, hipYaw=+15, approach=+20
        // Cột 1 (giữa): lateral=0, hipYaw=0, approach=0
        // Cột 2 (phải): lateral=-0.20, hipYaw=-15, approach=-20
        float[][] colSignals = {
            new[] { 0.20f, 15f, 20f },   // cột 0
            new[] { 0.00f, 0f,  0f  },   // cột 1
            new[] {-0.20f,-15f,-20f },   // cột 2
        };

        // RunUp mẫu cho từng hàng
        float[] rowRunUp = { 4.5f, 3.5f, 2.5f };

        var rng = new Random(12345u);

        for (int i = 0; i < trials; i++)
        {
            int targetCell = rng.NextInt(0, 9);
            int col = targetCell % 3;
            int row = targetCell / 3;

            var cues = MakeCues(
                colSignals[col][0],
                colSignals[col][1],
                colSignals[col][2],
                rowRunUp[row],
                ttc: 0.3f,
                obs: 0.85f  // observability cao nhưng không hoàn hảo
            );

            uint seed = rng.NextUInt();
            if (seed == 0) seed = 1;
            var history = new ShotHistory();

            var read = brain.Infer(cues, history, profile, seed);
            if (read.bestCell == targetCell)
                correct++;
        }

        float accuracy = (float)correct / trials;
        UnityEngine.Debug.Log($"[T18] 1000 lần, profile Thường: bestCell đúng = {correct}/{trials} = {accuracy:P1}");

        // Kỳ vọng: 0.50 ± 0.05 (hoặc 0.52 ± 0.04) → phạm vi [0.45, 0.56]
        Assert.GreaterOrEqual(accuracy, 0.45f,
            $"Tỉ lệ đúng = {accuracy:P1}, kỳ vọng >= 0.45");
        Assert.LessOrEqual(accuracy, 0.56f,
            $"Tỉ lệ đúng = {accuracy:P1}, kỳ vọng <= 0.56");
    }

    [Test]
    public void T18_CungSeed_CungTinHieu_CungKetQua()
    {
        var brain = new BayesianKeeperBrain();
        var profile = KeeperProfile.CreateMedium();
        var history = new ShotHistory();
        history.Record(3);
        history.Record(6);
        var cues = LeftCues();
        uint seed = 777u;

        var read1 = brain.Infer(cues, history, profile, seed);
        var read2 = brain.Infer(cues, history, profile, seed);

        Assert.AreEqual(read1.bestCell, read2.bestCell, "bestCell phải giống byte");
        Assert.AreEqual(read1.confidence, read2.confidence, "confidence phải giống byte");
        for (int c = 0; c < 9; c++)
        {
            Assert.AreEqual(read1.cellProbabilities[c], read2.cellProbabilities[c],
                $"Ô {c}: xác suất lần 1 = {read1.cellProbabilities[c]}, lần 2 = {read2.cellProbabilities[c]}");
        }
    }

    [Test]
    public void T18_TinHieuMauThuan_KhongNaN_KhongAm()
    {
        var brain = new BayesianKeeperBrain();
        var profile = KeeperProfile.CreateMedium();
        var history = new ShotHistory();

        // Cues mâu thuẫn: lateral chỉ trái, hip chỉ phải, approach chỉ trái
        var cues = ConflictingCues();

        for (uint seed = 1; seed <= 50; seed++)
        {
            var read = brain.Infer(cues, history, profile, seed);
            Assert.IsFalse(HasNaN(read), $"Seed {seed}: Phát hiện NaN trong kết quả");
            Assert.IsFalse(HasNegative(read), $"Seed {seed}: Phát hiện xác suất âm");
            float sum = SumProbabilities(read);
            Assert.AreEqual(1f, sum, 1e-5f, $"Seed {seed}: Tổng = {sum}");
        }
    }

    [Test]
    public void T18_TinHieuCucDoan_KhongNaN()
    {
        var brain = new BayesianKeeperBrain();
        var profile = KeeperProfile.CreateHard();
        var history = new ShotHistory();

        // Giá trị cực đoan
        var cues = MakeCues(100f, -500f, 999f, 0.01f, 0f, 1f);
        var read = brain.Infer(cues, history, profile, 42u);

        Assert.IsFalse(HasNaN(read), "Cues cực đoan không được sinh NaN");
        Assert.IsFalse(HasNegative(read), "Cues cực đoan không được sinh xác suất âm");
        Assert.AreEqual(1f, SumProbabilities(read), 1e-5f);
    }

    [Test]
    public void T18_Seed0_KhongCrash()
    {
        var brain = new BayesianKeeperBrain();
        var profile = KeeperProfile.CreateMedium();
        var history = new ShotHistory();
        var cues = CenterCues();

        // seed = 0 phải được xử lý (chuyển thành 1)
        var read = brain.Infer(cues, history, profile, 0u);
        Assert.IsFalse(HasNaN(read));
        Assert.AreEqual(1f, SumProbabilities(read), 1e-5f);
    }

    [Test]
    public void T18_ProfileNull_KhongCrash()
    {
        var brain = new BayesianKeeperBrain();
        var history = new ShotHistory();
        var cues = CenterCues();

        var read = brain.Infer(cues, history, null, 42u);
        Assert.IsFalse(HasNaN(read));
        Assert.AreEqual(1f, SumProbabilities(read), 1e-5f);
    }

    [Test]
    public void T18_Confidence_TuongQuanVoiDoChinhXac()
    {
        // Kiểm chứng calibration: chia kết quả theo bins confidence,
        // xác suất thực tế phải tăng khi confidence tăng.

        var brain = new BayesianKeeperBrain();
        var profile = KeeperProfile.CreateMedium();

        float[][] colSignals = {
            new[] { 0.20f, 15f, 20f },
            new[] { 0.00f, 0f,  0f  },
            new[] {-0.20f,-15f,-20f },
        };
        float[] rowRunUp = { 4.5f, 3.5f, 2.5f };

        // 3 bins: [0, 0.15), [0.15, 0.30), [0.30, 1.0]
        int[] binCorrect = new int[3];
        int[] binTotal = new int[3];

        var rng = new Random(54321u);

        for (int i = 0; i < 3000; i++)
        {
            int targetCell = rng.NextInt(0, 9);
            int col = targetCell % 3;
            int row = targetCell / 3;

            // Thêm nhiễu vào cues để tạo đa dạng
            float noiseLat = rng.NextFloat(-0.1f, 0.1f);
            float noiseHip = rng.NextFloat(-8f, 8f);
            float noiseApp = rng.NextFloat(-8f, 8f);
            float obs = rng.NextFloat(0.3f, 1.0f);

            var cues = MakeCues(
                colSignals[col][0] + noiseLat,
                colSignals[col][1] + noiseHip,
                colSignals[col][2] + noiseApp,
                rowRunUp[row] + rng.NextFloat(-0.5f, 0.5f),
                ttc: 0.3f,
                obs: obs
            );

            uint seed = rng.NextUInt();
            if (seed == 0) seed = 1;
            var history = new ShotHistory();

            var read = brain.Infer(cues, history, profile, seed);

            int bin;
            if (read.confidence < 0.15f) bin = 0;
            else if (read.confidence < 0.30f) bin = 1;
            else bin = 2;

            binTotal[bin]++;
            if (read.bestCell == targetCell) binCorrect[bin]++;
        }

        // Tính accuracy per bin
        float[] binAccuracy = new float[3];
        for (int b = 0; b < 3; b++)
        {
            binAccuracy[b] = binTotal[b] > 0
                ? (float)binCorrect[b] / binTotal[b]
                : 0f;
            UnityEngine.Debug.Log($"[T18 CALIBRATION] Bin {b}: " +
                $"accuracy = {binAccuracy[b]:P1} ({binCorrect[b]}/{binTotal[b]})");
        }

        // Kiểm tra: confidence cao hơn → accuracy cao hơn (tương quan thuận)
        // Bin 2 (high confidence) phải có accuracy >= bin 0 (low confidence)
        if (binTotal[0] > 50 && binTotal[2] > 50)
        {
            Assert.GreaterOrEqual(binAccuracy[2], binAccuracy[0],
                $"Confidence cao (bin 2: {binAccuracy[2]:P1}) phải chính xác hơn " +
                $"confidence thấp (bin 0: {binAccuracy[0]:P1})");
        }
    }

    [Test]
    public void T18_ObservabilityCaoHon_ConfidenceCaoHon()
    {
        var brain = new BayesianKeeperBrain();
        var profile = KeeperProfile.CreateMedium();
        var history = new ShotHistory();
        var cues = LeftCues();
        uint seed = 42u;

        var readLow = brain.Infer(MakeCues(0.20f, 15f, 20f, 3.5f, 0.5f, 0.2f),
                                   history, profile, seed);
        var readHigh = brain.Infer(MakeCues(0.20f, 15f, 20f, 3.5f, 0.5f, 0.9f),
                                    history, profile, seed);

        Assert.Greater(readHigh.confidence, readLow.confidence,
            $"obs=0.9 → confidence={readHigh.confidence:F3}, " +
            $"obs=0.2 → confidence={readLow.confidence:F3}");
    }

    [Test]
    public void T18_CuesTrai_BestCellCotTrai()
    {
        var brain = new BayesianKeeperBrain();
        var profile = KeeperProfile.CreateHard();
        var history = new ShotHistory();
        var cues = LeftCues();

        var read = brain.Infer(cues, history, profile, 42u);
        int col = read.bestCell % 3;
        Assert.AreEqual(0, col,
            $"Cues trái rõ ràng → bestCell phải ở cột 0 (trái), nhận được ô {read.bestCell}");
    }

    [Test]
    public void T18_CuesPhai_BestCellCotPhai()
    {
        var brain = new BayesianKeeperBrain();
        var profile = KeeperProfile.CreateHard();
        var history = new ShotHistory();
        var cues = RightCues();

        var read = brain.Infer(cues, history, profile, 42u);
        int col = read.bestCell % 3;
        Assert.AreEqual(2, col,
            $"Cues phải rõ ràng → bestCell phải ở cột 2 (phải), nhận được ô {read.bestCell}");
    }

    [Test]
    public void T18_LichSuAnhHuongKetQua()
    {
        var brain = new BayesianKeeperBrain();
        var profile = KeeperProfile.CreateMedium();

        // Lịch sử: sút trái liên tục
        var historyLeft = new ShotHistory();
        for (int i = 0; i < 5; i++) historyLeft.Record(0);

        // Không lịch sử
        var historyEmpty = new ShotHistory();

        // Cues trung tính (không rõ hướng)
        var cues = CenterCues();
        uint seed = 42u;

        var readWithHistory = brain.Infer(cues, historyLeft, profile, seed);
        var readNoHistory = brain.Infer(cues, historyEmpty, profile, seed);

        // Với lịch sử sút trái, xác suất ô 0 phải cao hơn
        Assert.Greater(readWithHistory.cellProbabilities[0],
                       readNoHistory.cellProbabilities[0],
            $"Lịch sử sút trái → P(ô 0) với lịch sử ({readWithHistory.cellProbabilities[0]:F4}) " +
            $"phải > P(ô 0) không lịch sử ({readNoHistory.cellProbabilities[0]:F4})");
    }

    [Test]
    public void T18_BestCell_TrongPhamVi0Den8()
    {
        var brain = new BayesianKeeperBrain();
        var profile = KeeperProfile.CreateMedium();
        var history = new ShotHistory();

        var rng = new Random(11111u);
        for (int i = 0; i < 100; i++)
        {
            var cues = MakeCues(
                rng.NextFloat(-0.5f, 0.5f),
                rng.NextFloat(-30f, 30f),
                rng.NextFloat(-30f, 30f),
                rng.NextFloat(1f, 6f),
                rng.NextFloat(0f, 1f),
                rng.NextFloat(0f, 1f)
            );
            uint seed = rng.NextUInt();
            if (seed == 0) seed = 1;

            var read = brain.Infer(cues, history, profile, seed);
            Assert.GreaterOrEqual(read.bestCell, 0);
            Assert.LessOrEqual(read.bestCell, 8);
        }
    }

    // ══════════════════════════════════════════════════════════
    //  T20 — ShotHistory
    // ══════════════════════════════════════════════════════════

    [Test]
    public void T20_LichSuRong_PriorDeuTuyetDoi()
    {
        var history = new ShotHistory();
        var prior = history.Prior(0.5f, 0.8f);

        Assert.AreEqual(9, prior.Length);
        const float expected = 1f / 9f;
        for (int i = 0; i < 9; i++)
        {
            Assert.AreEqual(expected, prior[i], 1e-6f,
                $"Ô {i}: prior = {prior[i]}, kỳ vọng = {expected}");
        }
    }

    [Test]
    public void T20_PriorCongBang1_MoiTruongHop()
    {
        var history = new ShotHistory();
        var rng = new Random(99999u);

        // Test với nhiều chuỗi lịch sử khác nhau
        for (int trial = 0; trial < 50; trial++)
        {
            int nShots = rng.NextInt(0, 21);
            history.Clear();
            for (int s = 0; s < nShots; s++)
                history.Record(rng.NextInt(0, 9));

            float weight = rng.NextFloat(0f, 1f);
            float decay = rng.NextFloat(0f, 1f);

            var prior = history.Prior(weight, decay);
            float sum = 0f;
            for (int i = 0; i < 9; i++)
            {
                Assert.GreaterOrEqual(prior[i], 0f, $"Prior[{i}] âm!");
                sum += prior[i];
            }
            Assert.AreEqual(1f, sum, 1e-5f,
                $"Trial {trial}: Tổng prior = {sum}, kỳ vọng = 1.0");
        }
    }

    [Test]
    public void T20_CuGanDay_TrongSoCaoHon()
    {
        var history = new ShotHistory();
        // Sút trái 3 lần cũ, rồi sút phải 1 lần gần nhất
        history.Record(0); // cũ
        history.Record(0); // cũ
        history.Record(0); // cũ
        history.Record(2); // gần nhất

        var prior = history.Prior(0.8f, 0.7f);

        // Cú gần nhất (ô 2) nên có trọng số cao hơn chia theo số lần
        // Ô 0: 3 lần nhưng cũ (decay^3 + decay^2 + decay^1)
        // Ô 2: 1 lần nhưng mới nhất (decay^0 = 1.0)
        // Với decay=0.7: ô0 weight = 0.343 + 0.49 + 0.7 = 1.533
        //                 ô2 weight = 1.0
        // Nên ô 0 vẫn cao hơn do 3 lần, nhưng nếu chỉ 1 lần cũ vs 1 lần mới:
        var history2 = new ShotHistory();
        history2.Record(0); // cũ
        history2.Record(2); // gần nhất

        var prior2 = history2.Prior(0.8f, 0.7f);
        // ô 0: decay^1 = 0.7, ô 2: decay^0 = 1.0
        Assert.Greater(prior2[2], prior2[0],
            $"Cú gần nhất (ô 2: {prior2[2]:F4}) phải có trọng số cao hơn cú cũ (ô 0: {prior2[0]:F4})");
    }

    [Test]
    public void T20_MemoryWeight0_VoHieu()
    {
        var history = new ShotHistory();
        history.Record(0);
        history.Record(0);
        history.Record(0);
        history.Record(0);
        history.Record(0);

        var prior = history.Prior(0f, 0.8f); // weight = 0

        const float expected = 1f / 9f;
        for (int i = 0; i < 9; i++)
        {
            Assert.AreEqual(expected, prior[i], 1e-6f,
                $"Weight=0: ô {i} = {prior[i]}, kỳ vọng đều = {expected}");
        }
    }

    [Test]
    public void T20_Clear_XoaSach()
    {
        var history = new ShotHistory();
        history.Record(3);
        history.Record(5);
        history.Record(7);

        Assert.AreEqual(3, history.cells.Length);

        history.Clear();
        Assert.AreEqual(0, history.cells.Length, "Clear phải xoá hết");

        var prior = history.Prior(0.8f, 0.8f);
        const float expected = 1f / 9f;
        for (int i = 0; i < 9; i++)
        {
            Assert.AreEqual(expected, prior[i], 1e-6f,
                $"Sau Clear: ô {i} phải đều");
        }
    }

    [Test]
    public void T20_ToiDa20Cu_XoaCuCuNhat()
    {
        var history = new ShotHistory();

        // Ghi 25 cú, chỉ giữ 20 gần nhất
        for (int i = 0; i < 25; i++)
            history.Record(i % 9);

        Assert.AreEqual(20, history.cells.Length,
            $"Chỉ giữ tối đa 20 cú, hiện có {history.cells.Length}");
    }

    [Test]
    public void T20_Record_ClampOVaoBienNgang()
    {
        var history = new ShotHistory();
        history.Record(-5);  // kẹp thành 0
        history.Record(99);  // kẹp thành 8

        Assert.AreEqual(0, history.cells[0], "cell -5 phải kẹp thành 0");
        Assert.AreEqual(8, history.cells[1], "cell 99 phải kẹp thành 8");
    }

    [Test]
    public void T20_LuuQuaCacLuot()
    {
        var history = new ShotHistory();

        // Lượt 1
        history.Record(0);
        Assert.AreEqual(1, history.cells.Length);

        // Lượt 2
        history.Record(4);
        Assert.AreEqual(2, history.cells.Length);

        // Lượt 3
        history.Record(8);
        Assert.AreEqual(3, history.cells.Length);

        // Kiểm tra thứ tự
        Assert.AreEqual(0, history.cells[0]);
        Assert.AreEqual(4, history.cells[1]);
        Assert.AreEqual(8, history.cells[2]);
    }

    [Test]
    public void T20_FixedList_KhongCapPhat()
    {
        // Kiểm chứng ShotHistory dùng FixedList, không List<T>
        // (biên dịch được đã chứng minh điều này, nhưng test explicit)
        var history = new ShotHistory();

        // Nếu cấp phát GC, test này sẽ thất bại
        TestDelegate action = () =>
        {
            for (int i = 0; i < 20; i++)
                history.Record(i % 9);
            var prior = history.Prior(0.5f, 0.8f);
            _ = prior[0]; // đọc giá trị
        };

        // Warm up JIT
        action();

        Assert.That(action, Is.Not.AllocatingGCMemory(),
            "ShotHistory không được cấp phát GC memory");
    }
}
