using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Unity.Mathematics;
using Eleven.Core;
using Eleven.Presentation.Grass;
using Is = NUnit.Framework.Is;

namespace Eleven.Tests.EditMode
{
    /// <summary>
    /// T29 — Hệ thống cỏ instanced.
    ///
    /// BỐN ô nghiệm thu của T29 KHÔNG kiểm được trong EditMode vì chúng đòi một GPU thật:
    /// đo overdraw bằng debug view, số đo 2.0ms trên máy có tên, bảng tám dòng có số thật,
    /// và chênh lệch frame time của bậc C. Test ở đây kiểm phần kiểm được — cấu trúc dữ liệu,
    /// tính tất định, và quan trọng nhất: KHÔNG có đường nào để mã tự hạ chất lượng khi vượt
    /// ngân sách. Xem docs/backlog/phase-5-trinh-dien.md mục T29 để biết ô nào cần người đo.
    /// </summary>
    [TestFixture]
    public class GrassSystemTests
    {
        private static string ShaderPath =>
            Path.Combine(Application.dataPath, "_Project", "Art", "Shaders", "Grass.shader");

        private static TierProfile MakeProfile(QualityTier tier, float grassDensity)
        {
            var profile = ScriptableObject.CreateInstance<TierProfile>();
            profile.tier = tier;
            profile.grassDensity = grassDensity;
            return profile;
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Ô 1 — Mật độ giảm dần theo bán kính, đọc từ TierProfile.grassDensity
        // ─────────────────────────────────────────────────────────────────────────────

        [Test]
        public void MatDo_GiamDanTheoBanKinh_KhongBaoGioTang()
        {
            float truoc = float.PositiveInfinity;

            for (float r = 0f; r <= GrassDensityField.FadeEndRadius + 5f; r += 0.25f)
            {
                float mat = GrassDensityField.DensityAt(r, 1.0f);

                Assert.LessOrEqual(mat, truoc + 1e-6f,
                    $"Mật độ TĂNG khi ra xa: tại r={r:F2}m là {mat:F3} túm/m², trong khi ở " +
                    $"vòng trong là {truoc:F3}. Ô nghiệm thu đòi giảm dần theo bán kính.");

                Assert.GreaterOrEqual(mat, 0f, $"Mật độ âm tại r={r:F2}m.");
                truoc = mat;
            }

            Assert.AreEqual(GrassDensityField.BaseTuftsPerSquareMetre, GrassDensityField.DensityAt(0f, 1.0f), 1e-5f,
                "Tại chấm phạt đền — chỗ camera nhìn kỹ nhất — mật độ phải đạt mức tối đa.");

            Assert.AreEqual(0f, GrassDensityField.DensityAt(GrassDensityField.FadeEndRadius, 1.0f), 1e-6f,
                "Ra tới bán kính kết thúc mà vẫn còn cỏ.");

            Assert.AreEqual(0f, GrassDensityField.DensityAt(100f, 1.0f), 1e-6f,
                "Ngoài vùng phủ mà vẫn sinh cỏ — đó là chi phí thuần lãng phí.");
        }

        [Test]
        public void MatDo_DocTuTierProfile_KhongVietCungTrongMa()
        {
            var profileA = MakeProfile(QualityTier.A, 1.0f);
            var profileB = MakeProfile(QualityTier.B, 0.4f);
            var profileC = MakeProfile(QualityTier.C, 0.0f);

            try
            {
                var a = GrassTierSettings.FromProfile(profileA);
                var b = GrassTierSettings.FromProfile(profileB);
                var c = GrassTierSettings.FromProfile(profileC);

                Assert.AreEqual(1.0f, a.densityScale, 1e-6f, "Bậc A không đọc đúng grassDensity từ profile.");
                Assert.AreEqual(0.4f, b.densityScale, 1e-6f, "Bậc B không đọc đúng grassDensity từ profile.");
                Assert.AreEqual(0.0f, c.densityScale, 1e-6f, "Bậc C không đọc đúng grassDensity từ profile.");

                // Giá trị BẤT THƯỜNG: nếu ai đó sửa profile thành 0.7 thì hệ thống phải theo,
                // chứ không được rơi về bảng hằng số nội bộ.
                var profileLa = MakeProfile(QualityTier.A, 0.7f);
                try
                {
                    var la = GrassTierSettings.FromProfile(profileLa);
                    Assert.AreEqual(0.7f, la.densityScale, 1e-6f,
                        "Đổi grassDensity trong profile mà hệ thống vẫn dùng hằng số của bậc — " +
                        "tức là mật độ đang viết cứng trong mã chứ không đọc từ TierProfile.");

                    using (var field = new GrassField(in la))
                    using (var fieldA = new GrassField(in a))
                    {
                        Assert.Less(field.InstanceCount, fieldA.InstanceCount,
                            "Hạ grassDensity từ 1.0 xuống 0.7 mà số túm cỏ không giảm.");
                        Assert.Greater(field.InstanceCount, 0, "0.7 mà không rải được túm nào.");
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(profileLa); }

                foreach (float r in new[] { 0f, 5f, 12f, 20f, 30f })
                {
                    float mA = GrassDensityField.DensityAt(r, a.densityScale);
                    float mB = GrassDensityField.DensityAt(r, b.densityScale);

                    Assert.AreEqual(mA * 0.4f, mB, 1e-5f,
                        $"Tại r={r}m, mật độ bậc B không đúng 40% của bậc A.");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profileA);
                UnityEngine.Object.DestroyImmediate(profileB);
                UnityEngine.Object.DestroyImmediate(profileC);
            }
        }

        [Test]
        public void RaiCo_MatDoThucTe_GiamDanRaNgoai()
        {
            using (var field = new GrassField(QualityTier.A))
            {
                Assert.Greater(field.InstanceCount, 5000,
                    $"Chỉ rải được {field.InstanceCount} túm ở bậc A — sân sẽ trọc.");
                Assert.LessOrEqual(field.InstanceCount, GrassBudget.MaxInstances,
                    $"Rải {field.InstanceCount} túm, vượt trần {GrassBudget.MaxInstances}.");

                var instances = field.Instances;

                int trong = 0;   // r < 12  (vùng mật độ tối đa)
                int ngoai = 0;   // 20 <= r < 24
                for (int i = 0; i < instances.Length; i++)
                {
                    float3 p = instances[i].position;
                    float r = math.length(new float2(p.x, p.z));

                    Assert.AreEqual(0f, p.y, 1e-6f, $"Túm {i} không nằm trên mặt sân (y = {p.y}).");
                    Assert.LessOrEqual(r, GrassDensityField.FadeEndRadius + 1e-3f,
                        $"Túm {i} nằm ngoài vùng phủ, r = {r:F2}m.");

                    if (r < GrassDensityField.FullDensityRadius) trong++;
                    else if (r >= 20f && r < 24f) ngoai++;
                }

                float dienTichTrong = math.PI * GrassDensityField.FullDensityRadius * GrassDensityField.FullDensityRadius;
                float dienTichNgoai = math.PI * (24f * 24f - 20f * 20f);

                float matDoTrong = trong / dienTichTrong;
                float matDoNgoai = ngoai / dienTichNgoai;

                Assert.Greater(matDoTrong, matDoNgoai * 1.4f,
                    $"Mật độ thực tế sau khi rải không giảm rõ theo bán kính: " +
                    $"trong = {matDoTrong:F2} túm/m², ngoài = {matDoNgoai:F2} túm/m².");

                Assert.AreEqual(GrassDensityField.BaseTuftsPerSquareMetre, matDoTrong, 1.0f,
                    $"Mật độ thực tế ở vùng lõi ({matDoTrong:F2}) lệch quá xa mức khai báo " +
                    $"({GrassDensityField.BaseTuftsPerSquareMetre}) — bộ rải đang không tuân theo trường mật độ.");
            }
        }

        [Test]
        public void RaiCo_TatDinhTheoSeed()
        {
            using (var a = new GrassField(QualityTier.A, 4242u))
            using (var b = new GrassField(QualityTier.A, 4242u))
            using (var c = new GrassField(QualityTier.A, 777u))
            {
                Assert.AreEqual(a.InstanceCount, b.InstanceCount,
                    "Cùng seed mà số túm khác nhau — bộ rải không tất định.");

                var ia = a.Instances;
                var ib = b.Instances;

                for (int i = 0; i < ia.Length; i++)
                {
                    Assert.AreEqual(ia[i].position.x, ib[i].position.x, 0f, $"Túm {i} lệch theo X.");
                    Assert.AreEqual(ia[i].position.z, ib[i].position.z, 0f, $"Túm {i} lệch theo Z.");
                    Assert.AreEqual(ia[i].yaw, ib[i].yaw, 0f, $"Túm {i} lệch góc xoay.");
                    Assert.AreEqual(ia[i].windPhase, ib[i].windPhase, 0f, $"Túm {i} lệch pha gió.");
                }

                bool khac = a.InstanceCount != c.InstanceCount;
                if (!khac)
                {
                    var ic = c.Instances;
                    for (int i = 0; i < ia.Length && !khac; i++)
                    {
                        if (math.abs(ia[i].position.x - ic[i].position.x) > 1e-6f) khac = true;
                    }
                }

                Assert.IsTrue(khac, "Đổi seed mà sân cỏ y hệt — seed không có tác dụng.");
            }
        }

        [Test]
        public void ChieuCaoVaPhaGio_NamTrongDaiHopLy()
        {
            using (var field = new GrassField(QualityTier.A))
            {
                var instances = field.Instances;
                var histogram = new int[8];

                for (int i = 0; i < instances.Length; i++)
                {
                    GrassInstance g = instances[i];

                    Assert.GreaterOrEqual(g.height, GrassField.MinHeight, $"Túm {i} thấp bất thường.");
                    Assert.LessOrEqual(g.height, GrassField.MaxHeight,
                        $"Túm {i} cao {g.height:F3}m — cỏ sân bóng được cắt ngắn, không phải cỏ dại.");

                    Assert.GreaterOrEqual(g.windPhase, 0f);
                    Assert.Less(g.windPhase, 1f);
                    Assert.GreaterOrEqual(g.yaw, 0f);
                    Assert.LessOrEqual(g.yaw, 2f * math.PI);

                    histogram[math.min(7, (int)(g.windPhase * 8f))]++;
                }

                int nguong = instances.Length / 16;
                for (int i = 0; i < histogram.Length; i++)
                {
                    Assert.Greater(histogram[i], nguong,
                        $"Pha gió dồn cục: khoảng {i} chỉ có {histogram[i]}/{instances.Length} túm. " +
                        "HẬU QUẢ: cả sân lượn cùng một nhịp, trông như tấm vải chứ không phải cỏ.");
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Ô 4 — Bảng so sánh tám dòng: có/không alpha clip, đổ bóng, gió
        // ─────────────────────────────────────────────────────────────────────────────

        [Test]
        public void TamBienThe_DuTamToHop_KhongTrungNhau()
        {
            GrassRenderSettings[] variants = GrassRenderSettings.AllVariants();

            Assert.AreEqual(8, variants.Length,
                "Ba công tắc nhị phân phải cho đúng tám tổ hợp — đúng bằng số dòng của bảng đo.");
            Assert.AreEqual(8, GrassRenderSettings.VariantCount);

            var seen = new bool[8];
            int clipOn = 0, shadowOn = 0, windOn = 0;

            foreach (GrassRenderSettings v in variants)
            {
                int idx = v.VariantIndex;
                Assert.IsFalse(seen[idx], $"Biến thể {idx} ({v.Label}) xuất hiện hai lần trong bảng.");
                seen[idx] = true;

                GrassRenderSettings roundTrip = GrassRenderSettings.FromVariantIndex(idx);
                Assert.AreEqual(v.alphaClip, roundTrip.alphaClip, $"Biến thể {idx}: alphaClip không khớp khi dựng lại.");
                Assert.AreEqual(v.castShadows, roundTrip.castShadows, $"Biến thể {idx}: castShadows không khớp khi dựng lại.");
                Assert.AreEqual(v.wind, roundTrip.wind, $"Biến thể {idx}: wind không khớp khi dựng lại.");

                if (v.alphaClip) clipOn++;
                if (v.castShadows) shadowOn++;
                if (v.wind) windOn++;
            }

            Assert.AreEqual(4, clipOn, "Alpha clip phải bật ở đúng bốn trong tám dòng.");
            Assert.AreEqual(4, shadowOn, "Đổ bóng phải bật ở đúng bốn trong tám dòng.");
            Assert.AreEqual(4, windOn, "Gió phải bật ở đúng bốn trong tám dòng.");
        }

        [Test]
        public void BangDo_ThieuDongThiKhongKetLuan_DuMoiDongDaDoDeuDat()
        {
            var table = new GrassMeasurementTable();
            var full = GrassRenderSettings.Full;

            Assert.AreEqual(GrassVerdict.ChuaDoDu, table.Evaluate(in full),
                "Bảng trống mà đã kết luận được — đó là cách một ô nghiệm thu bị tick khống.");

            // Đo bảy dòng, tất cả đều rẻ. Vẫn không được kết luận.
            for (int i = 0; i < 7; i++)
            {
                table.Record(new GrassMeasurement
                {
                    variantIndex = i,
                    gpuMs = 0.9f,
                    averageOverdraw = 1.8f,
                    instanceCount = 13900,
                    frameTimeWithGrassMs = 15.1f,
                    frameTimeWithoutGrassMs = 14.2f,
                    deviceName = "Pixel 6a"
                });
            }

            Assert.AreEqual(7, table.RecordedCount);
            Assert.IsFalse(table.IsComplete, "Bảy dòng mà đã coi là đủ tám dòng.");
            Assert.AreEqual(GrassVerdict.ChuaDoDu, table.Evaluate(in full));

            table.Record(new GrassMeasurement
            {
                variantIndex = 7,
                gpuMs = 1.6f,
                averageOverdraw = 2.3f,
                instanceCount = 13900,
                frameTimeWithGrassMs = 15.8f,
                frameTimeWithoutGrassMs = 14.2f,
                deviceName = "Pixel 6a"
            });

            Assert.IsTrue(table.IsComplete);
            Assert.AreEqual(GrassVerdict.Dat, table.Evaluate(in full));
            Assert.AreEqual(7, table.WorstVariantIndex, "Dòng đắt nhất phải là dòng bật cả ba công tắc.");
            Assert.AreEqual(0.7f, table.SpreadMs(), 1e-4f);
        }

        [Test]
        public void DongDo_ThieuTenMay_KhongTinhLaDaDo()
        {
            var thieuTenMay = new GrassMeasurement
            {
                variantIndex = 0,
                gpuMs = 1.4f,
                instanceCount = 13900,
                deviceName = null
            };

            Assert.IsFalse(thieuTenMay.IsRecorded,
                "Một con số không có tên máy đi kèm thì không so sánh được với bất cứ dòng nào — " +
                "quy tắc 7 của docs/backlog/README.md.");

            var chuaDo = new GrassMeasurement { variantIndex = 0, deviceName = "Pixel 6a" };
            Assert.IsFalse(chuaDo.IsRecorded, "gpuMs = 0 mà đã coi là đã đo.");

            var daDo = new GrassMeasurement
            {
                variantIndex = 0,
                gpuMs = 1.4f,
                instanceCount = 13900,
                frameTimeWithGrassMs = 16.0f,
                frameTimeWithoutGrassMs = 14.2f,
                deviceName = "Pixel 6a"
            };
            Assert.IsTrue(daDo.IsRecorded);
            Assert.AreEqual(1.8f, daDo.FrameTimeDeltaMs, 1e-4f);
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Ô 3 + ô 7 — Ngân sách 2.0ms, và vượt thì BÁO CÁO chứ không tự hạ chất lượng
        // ─────────────────────────────────────────────────────────────────────────────

        [Test]
        public void NganSachBacA_Dung2ms()
        {
            Assert.AreEqual(2.0f, GrassBudget.MaxTierAGpuMs, 1e-6f);

            var a = GrassTierSettings.ForTier(QualityTier.A);
            Assert.LessOrEqual(a.maxGpuBudgetMs, GrassBudget.MaxTierAGpuMs,
                "Ngân sách cỏ bậc A vượt trần 2.0ms.");

            var b = GrassTierSettings.ForTier(QualityTier.B);
            Assert.Less(b.maxGpuBudgetMs, a.maxGpuBudgetMs, "Bậc B phải rẻ hơn bậc A.");
        }

        [Test]
        public void VuotNganSach_PhaiBaoCao_KhongTuHaChatLuong()
        {
            var table = new GrassMeasurementTable();
            for (int i = 0; i < GrassRenderSettings.VariantCount; i++)
            {
                table.Record(new GrassMeasurement
                {
                    variantIndex = i,
                    gpuMs = 3.4f,               // vượt xa trần 2.0ms
                    averageOverdraw = 4.1f,
                    instanceCount = 13900,
                    frameTimeWithGrassMs = 19.9f,
                    frameTimeWithoutGrassMs = 14.2f,
                    deviceName = "Máy bậc A giả định"
                });
            }

            var settingsTruoc = GrassTierSettings.ForTier(QualityTier.A);

            using (var field = new GrassField(in settingsTruoc, 31337u))
            {
                int soTumTruoc = field.InstanceCount;

                var full = GrassRenderSettings.Full;
                GrassVerdict verdict = table.Evaluate(in full);

                Assert.AreEqual(GrassVerdict.VuotNganSach_PhaiBaoCao, verdict,
                    "3.4ms so với trần 2.0ms mà không ra kết luận phải báo cáo.");

                // Đây là điều ô nghiệm thu thật sự đòi: "báo cáo lại thay vì TỰ Ý giảm chất
                // lượng — quyết định cắt là của bạn". Đọc bảng đo không được phép làm cỏ thưa đi.
                var settingsSau = GrassTierSettings.ForTier(QualityTier.A);

                Assert.AreEqual(settingsTruoc.densityScale, settingsSau.densityScale, 0f,
                    "Sau khi đọc bảng vượt ngân sách, mật độ bậc A đã tự đổi. " +
                    "HẬU QUẢ: mã tự cắt chất lượng sau lưng người ra quyết định.");
                Assert.AreEqual(settingsTruoc.maxInstances, settingsSau.maxInstances, 0,
                    "Trần số túm tự đổi sau khi đánh giá bảng đo.");
                Assert.AreEqual(soTumTruoc, field.InstanceCount,
                    "Sân cỏ đang rải bị thưa đi sau khi đánh giá bảng đo.");
                Assert.IsTrue(field.IsRendered, "Cỏ bị tự tắt sau khi đánh giá bảng đo.");
            }
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Ô 5 — Bậc C tắt hoàn toàn, thay bằng texture
        // ─────────────────────────────────────────────────────────────────────────────

        [Test]
        public void BacC_TatHoanToan_ThayBangTextureMatSan()
        {
            var c = GrassTierSettings.ForTier(QualityTier.C);

            Assert.IsFalse(c.enabled, "Bậc C phải tắt cỏ hoàn toàn.");
            Assert.IsTrue(c.useGroundTexture, "Bậc C phải thay cỏ bằng texture mặt sân, không để sân trọc.");
            Assert.AreEqual(0f, c.densityScale, 1e-6f);
            Assert.AreEqual(0, c.maxInstances);

            using (var field = new GrassField(in c))
            {
                Assert.AreEqual(0, field.InstanceCount, "Bậc C vẫn rải cỏ.");
                Assert.AreEqual(0, field.VisibleInstanceCount);
                Assert.IsFalse(field.IsRendered);

                GrassRenderBatch batch = field.BuildBatch();
                Assert.AreEqual(0, batch.drawCallCount, "Bậc C vẫn tốn draw call cho cỏ.");
                Assert.AreEqual(0, batch.instanceCount);
                Assert.AreEqual(0, batch.GpuBufferBytes);
                Assert.IsTrue(batch.usesGroundTexture);
            }
        }

        [Test]
        public void SoSanhBacC_PhaiCoCaHaiSoDo_VaTenMay()
        {
            var thieu = new GrassTierCComparison
            {
                frameTimeWithGrassMs = 28.4f,
                deviceName = "Galaxy A13"
            };
            Assert.IsFalse(thieu.IsRecorded, "Thiếu số đo phía texture mà đã coi là đã ghi lại chênh lệch.");

            var du = new GrassTierCComparison
            {
                frameTimeWithGrassMs = 28.4f,
                frameTimeWithTextureMs = 22.1f,
                deviceName = "Galaxy A13"
            };
            Assert.IsTrue(du.IsRecorded);
            Assert.AreEqual(6.3f, du.DeltaMs, 1e-4f);
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Ô 6 — Cờ tắt riêng để đo đóng góp của riêng cỏ vào frame time
        // ─────────────────────────────────────────────────────────────────────────────

        [Test]
        public void CoTatRieng_TatDuocMaKhongRaiLaiCo()
        {
            using (var field = new GrassField(QualityTier.A))
            {
                int soTum = field.InstanceCount;
                Assert.Greater(soTum, 0);
                Assert.IsTrue(field.IsEnabled, "Cờ tắt riêng phải mặc định BẬT.");

                field.IsEnabled = false;

                Assert.AreEqual(0, field.VisibleInstanceCount, "Tắt cờ mà cỏ vẫn được vẽ.");
                Assert.IsFalse(field.IsRendered);
                Assert.AreEqual(soTum, field.InstanceCount,
                    "Tắt cờ làm mất dữ liệu đã rải. HẬU QUẢ: bật lại phải rải lại, và chênh lệch " +
                    "frame time đo được sẽ lẫn chi phí sinh dữ liệu chứ không còn là chi phí vẽ.");

                GrassRenderBatch tat = field.BuildBatch();
                Assert.AreEqual(0, tat.drawCallCount);
                Assert.AreEqual(0, tat.instanceCount);

                field.IsEnabled = true;

                Assert.AreEqual(soTum, field.VisibleInstanceCount, "Bật lại mà cỏ không trở lại đủ.");
                GrassRenderBatch bat = field.BuildBatch();
                Assert.AreEqual(GrassBudget.MaxDrawCalls, bat.drawCallCount,
                    "Toàn bộ cỏ phải nằm trong đúng một draw call.");
                Assert.AreEqual(soTum, bat.instanceCount);
                Assert.AreEqual(soTum * GrassInstanceGpu.SizeInBytes, bat.GpuBufferBytes);
            }
        }

        [Test]
        public void VongLapMoiKhungHinh_KhongCapPhatGC()
        {
            using (var field = new GrassField(QualityTier.A))
            {
                // Warm-up JIT
                field.Tick(0.016f);
                GrassRenderBatch warm = field.BuildBatch();
                Assert.AreEqual(1, warm.drawCallCount);

                Assert.That(() =>
                {
                    for (int i = 0; i < 500; i++)
                    {
                        field.Tick(0.016f);
                        GrassRenderBatch batch = field.BuildBatch();
                        if (batch.instanceCount < 0) throw new InvalidOperationException();
                    }
                }, Is.Not.AllocatingGCMemory(),
                   "Phần CPU của cỏ cấp phát mỗi khung hình — GC sẽ nuốt mất ngân sách 2.0ms.");
            }
        }

        [Test]
        public void DongHoGio_BocVongKhongTangVoHan()
        {
            using (var field = new GrassField(QualityTier.A))
            {
                for (int i = 0; i < 20000; i++)
                {
                    field.Tick(0.016f);
                }

                Assert.Less(field.WindTime, GrassField.WindCycleSeconds,
                    $"Đồng hồ gió đã chạy tới {field.WindTime}s mà không bọc vòng. " +
                    "HẬU QUẢ: sau vài giờ chơi, float32 mất hết phần lẻ và cỏ giật từng nấc.");
                Assert.GreaterOrEqual(field.WindTime, 0f);
            }
        }

        [Test]
        public void GrassInstanceGpu_Dung32Byte()
        {
            int actual = Unity.Collections.LowLevel.Unsafe.UnsafeUtility.SizeOf<GrassInstanceGpu>();
            Assert.AreEqual(GrassInstanceGpu.SizeInBytes, actual,
                $"GrassInstanceGpu chiếm {actual} byte chứ không phải {GrassInstanceGpu.SizeInBytes}. " +
                "HẬU QUẢ: bố cục lệch với struct cùng tên trong Grass.shader — cỏ mọc ở toạ độ rác.");

            Assert.LessOrEqual(GrassBudget.MaxInstances * GrassInstanceGpu.SizeInBytes, 1024 * 1024,
                "Bộ đệm instance vượt 1 MB — đây là bộ đệm đọc mỗi đỉnh, nó phải nằm gọn " +
                "trong băng thông chứ không chỉ trong bộ nhớ.");
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Shader: kiểm tĩnh những thứ quyết định có đo được tám dòng hay không
        // ─────────────────────────────────────────────────────────────────────────────

        [Test]
        public void ShaderCo_BaCongTacDeuDoDuoc_VaKhongBiLuocKhiBuild()
        {
            Assert.IsTrue(File.Exists(ShaderPath),
                $"Không tìm thấy Grass.shader tại '{ShaderPath}'. HẬU QUẢ: test này XANH GIẢ.");

            string source = File.ReadAllText(ShaderPath);

            Assert.IsTrue(source.Contains("#pragma target 4.5"),
                "Đọc StructuredBuffer trong vertex shader cần shader model 4.5.");

            Assert.AreEqual(1, DemDongKhaiBao(source, "StructuredBuffer<"),
                "Toàn bộ cỏ phải nằm trong ĐÚNG một StructuredBuffer — đó là điều kiện của một draw call.");

            Assert.IsTrue(source.Contains("_GRASS_ALPHACLIP"),
                "Không có keyword alpha clip thì không đo được bốn dòng 'clip-' của bảng.");
            Assert.IsTrue(source.Contains("_GRASS_WIND"),
                "Không có keyword gió thì không đo được bốn dòng 'gió-' của bảng.");

            // shader_feature bị LƯỢC khi build nếu không vật liệu nào bật keyword đó. Cả bốn
            // biến thể ở đây được bật lúc chạy để đo — lược mất là lúc đo trong build sẽ ra số
            // của biến thể khác mà không báo gì.
            Assert.AreEqual(0, DemDongCoChua(source, "shader_feature"),
                "Ba công tắc của T29 phải khai bằng multi_compile, không được dùng shader_feature.");

            Assert.AreEqual(1, DemDongCoChua(source, "\"LightMode\" = \"ShadowCaster\""),
                "Phải có đúng một pass ShadowCaster — không có nó thì không bật lên đo được " +
                "bốn dòng 'bóng+' của bảng.");

            Assert.IsTrue(source.Contains("clip("), "Shader cỏ phải cắt alpha khi bật keyword.");
            Assert.IsTrue(source.Contains("Cull Off"), "Túm cỏ là tấm phẳng, nhìn từ mặt sau vẫn phải thấy.");
            Assert.IsTrue(source.Contains("_GrassWindTime"),
                "Gió phải chạy theo đồng hồ riêng của GrassField chứ không đọc _Time — " +
                "nếu không, replay (T27) sẽ phát lại cỏ lượn khác lúc ghi.");
        }

        /// <summary>Số dòng (bỏ chú thích) bắt đầu bằng <paramref name="prefix"/> sau khi bỏ thụt lề.</summary>
        private static int DemDongKhaiBao(string source, string prefix)
        {
            int count = 0;
            foreach (string raw in source.Split('\n'))
            {
                string line = raw.Trim();
                if (line.StartsWith("//", StringComparison.Ordinal)) continue;
                if (line.StartsWith(prefix, StringComparison.Ordinal)) count++;
            }
            return count;
        }

        /// <summary>Số dòng (bỏ chú thích) có chứa <paramref name="needle"/>.</summary>
        private static int DemDongCoChua(string source, string needle)
        {
            int count = 0;
            foreach (string raw in source.Split('\n'))
            {
                string line = raw.Trim();
                if (line.StartsWith("//", StringComparison.Ordinal)) continue;
                if (line.Contains(needle)) count++;
            }
            return count;
        }
    }
}
