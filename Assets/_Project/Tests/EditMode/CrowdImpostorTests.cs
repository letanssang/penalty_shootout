using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using Eleven.Core;
using Eleven.Match;
using Eleven.Presentation.Crowd;
using Is = NUnit.Framework.Is;

namespace Eleven.Tests.EditMode
{
    /// <summary>
    /// T30 — Khán giả impostor. Sáu ô nghiệm thu, mỗi ô một test.
    /// Ô "≤ 0.8ms" không kiểm được trong Editor (không có GPU thật, không có nhiệt độ thật) —
    /// phần kiểm được ở đây là: hằng số ngân sách đúng, phía CPU không cấp phát và không
    /// phụ thuộc số lượng khán giả. Số đo GPU phải lấy từ máy, xem
    /// docs/phase-5-do-hieu-nang.md.
    /// </summary>
    [TestFixture]
    public class CrowdImpostorTests
    {
        private static string ShaderPath =>
            Path.Combine(Application.dataPath, "_Project", "Art", "Shaders", "CrowdImpostor.shader");

        // ─────────────────────────────────────────────────────────────────────────────
        // Ô 1 — Một atlas duy nhất, một draw call cho toàn bộ khán giả
        // ─────────────────────────────────────────────────────────────────────────────

        [Test]
        public void MotDrawCall_MotAtlas_ChoToanBoKhanGia()
        {
            foreach (QualityTier tier in Enum.GetValues(typeof(QualityTier)))
            {
                var director = new CrowdDirector(tier);
                CrowdRenderBatch batch = director.BuildBatch();

                Assert.AreEqual(1, batch.drawCallCount,
                    $"Bậc {tier}: khán đài đang tốn {batch.drawCallCount} draw call. " +
                    "HẬU QUẢ: mỗi draw call thêm vào là một lần đổi trạng thái vật liệu trên " +
                    "CPU lẫn GPU — ngân sách 0.8ms vỡ ngay ở con số thứ hai.");

                Assert.AreEqual(0, batch.atlasId,
                    $"Bậc {tier}: khán đài đang tham chiếu atlas khác 0, tức là có nhiều hơn một texture.");

                Assert.AreEqual(director.InstanceCount, batch.instanceCount,
                    $"Bậc {tier}: batch chỉ gói {batch.instanceCount}/{director.InstanceCount} khán giả — " +
                    "phần còn lại sẽ phải vẽ bằng draw call thứ hai.");

                Assert.Greater(batch.instanceCount, 500,
                    $"Bậc {tier}: chỉ có {batch.instanceCount} khán giả — khán đài trống trơn.");
            }
        }

        [Test]
        public void AtlasCoDungMotBoOKhongDeChongLen_VaNamTronTrongUV()
        {
            int cellCount = CrowdAtlas.Rows * CrowdAtlas.FramesPerMood;
            var rects = new float4[cellCount];
            int index = 0;

            foreach (CrowdMood mood in Enum.GetValues(typeof(CrowdMood)))
            {
                for (int frame = 0; frame < CrowdAtlas.FramesPerMood; frame++)
                {
                    float4 uv = CrowdAtlas.GetCellUv(mood, frame);
                    rects[index++] = uv;

                    Assert.GreaterOrEqual(uv.x, 0f, $"Ô ({mood},{frame}) tràn mép trái atlas.");
                    Assert.GreaterOrEqual(uv.y, 0f, $"Ô ({mood},{frame}) tràn mép dưới atlas.");
                    Assert.LessOrEqual(uv.x + uv.z, 1f, $"Ô ({mood},{frame}) tràn mép phải atlas.");
                    Assert.LessOrEqual(uv.y + uv.w, 1f, $"Ô ({mood},{frame}) tràn mép trên atlas.");
                    Assert.Greater(uv.z, 0f, $"Ô ({mood},{frame}) có bề rộng âm hoặc bằng 0.");
                    Assert.Greater(uv.w, 0f, $"Ô ({mood},{frame}) có chiều cao âm hoặc bằng 0.");
                }
            }

            for (int a = 0; a < cellCount; a++)
            {
                for (int b = a + 1; b < cellCount; b++)
                {
                    bool overlap =
                        rects[a].x < rects[b].x + rects[b].z &&
                        rects[b].x < rects[a].x + rects[a].z &&
                        rects[a].y < rects[b].y + rects[b].w &&
                        rects[b].y < rects[a].y + rects[a].w;

                    Assert.IsFalse(overlap,
                        $"Ô {a} và ô {b} chồng lên nhau trong atlas. " +
                        "HẬU QUẢ: ở mip cao hai ô rỉ màu sang nhau, khán giả viền màu áo của người bên cạnh.");
                }
            }
        }

        [Test]
        public void ShaderKhanGia_ChiKhaiBaoMotTexture_MotBuffer_VaKhongDoBong()
        {
            Assert.IsTrue(File.Exists(ShaderPath),
                $"Không tìm thấy shader khán giả tại '{ShaderPath}'. " +
                "HẬU QUẢ: test này XANH GIẢ — nó không kiểm được gì.");

            string source = File.ReadAllText(ShaderPath);

            Assert.AreEqual(1, DemDongKhaiBao(source, "StructuredBuffer<"),
                "Shader khán giả phải có ĐÚNG một StructuredBuffer chứa toàn bộ instance.");

            // Đếm theo DÒNG KHAI BÁO, không đếm chuỗi con: SAMPLE_TEXTURE2D(...) trong thân
            // hàm cũng chứa "TEXTURE2D(" và sẽ làm test báo động giả.
            Assert.AreEqual(1, DemDongKhaiBao(source, "TEXTURE2D("),
                "Shader khán giả phải có ĐÚNG một texture — đó là điều kiện của một draw call.");

            // Bỏ qua dòng chú thích: shader có hẳn một đoạn giải thích VÌ SAO không đổ bóng,
            // và test không được coi lời giải thích đó là vi phạm.
            Assert.AreEqual(0, DemDongCoChua(source, "ShadowCaster"),
                "Shader khán giả không được có pass ShadowCaster: vài nghìn tấm phẳng ghi vào " +
                "shadow map là chi phí thuần lãng phí.");

            Assert.IsTrue(source.Contains("clip("),
                "Shader khán giả phải cắt alpha sớm trước khi tính sáng.");

            Assert.IsTrue(source.Contains("#pragma target 4.5"),
                "Đọc StructuredBuffer trong vertex shader cần shader model 4.5.");

            Assert.IsTrue(source.Contains("_CrowdQuadAspect"),
                "Tỉ lệ tấm bảng phải do C# truyền vào (CrowdBillboard.QuadAspect), không hard-code trong shader.");
        }

        [Test]
        public void CrowdInstanceGpu_DungKichThuoc48Byte()
        {
            int actual = UnsafeUtility.SizeOf<CrowdInstanceGpu>();
            Assert.AreEqual(CrowdInstanceGpu.SizeInBytes, actual,
                $"CrowdInstanceGpu chiếm {actual} byte chứ không phải {CrowdInstanceGpu.SizeInBytes}. " +
                "HẬU QUẢ: bố cục lệch với struct cùng tên trong shader — khán giả sẽ mọc ở toạ độ rác.");
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Ô 2 — Ngân sách ≤ 0.8ms (phần kiểm được trong Editor)
        // ─────────────────────────────────────────────────────────────────────────────

        [Test]
        public void NganSachGPU_BacA_Duoi0_8ms_VaCpuKhongCapPhat()
        {
            var director = new CrowdDirector(QualityTier.A);

            Assert.LessOrEqual(director.MaxGpuBudgetMs, CrowdBudget.MaxGpuBudgetMs,
                "Ngân sách khán đài bậc A vượt trần 0.8ms.");

            // Warm-up
            director.Tick(0.016f);
            CrowdRenderBatch warm = director.BuildBatch();
            Assert.AreEqual(1, warm.drawCallCount);

            Assert.That(() =>
            {
                for (int i = 0; i < 500; i++)
                {
                    director.Tick(0.016f);
                    CrowdRenderBatch batch = director.BuildBatch();
                    if (batch.drawCallCount != 1) throw new InvalidOperationException();
                }
            }, Is.Not.AllocatingGCMemory(),
               "Phần CPU của khán đài cấp phát bộ nhớ mỗi khung hình — GC sẽ nuốt mất ngân sách.");
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Ô 3 — Phản ứng theo sự kiện
        // ─────────────────────────────────────────────────────────────────────────────

        [Test]
        public void PhanUngTheoSuKien_NhayKhiVaoBong_GucKhiHong_ImKhiDatBong()
        {
            var director = new CrowdDirector(QualityTier.A);

            director.OnKickPhaseChanged(KickPhase.Placing);
            Assert.AreEqual(CrowdMood.Hushed, director.Mood,
                "Lúc đặt bóng khán đài phải im — đây là khoảnh khắc căng nhất của luân lưu.");

            director.OnKickPhaseChanged(KickPhase.Aiming);
            Assert.AreEqual(CrowdMood.Anticipation, director.Mood);

            director.OnKickPhaseChanged(KickPhase.RunUp);
            Assert.AreEqual(CrowdMood.Anticipation, director.Mood);

            director.OnKickPhaseChanged(KickPhase.Flight);
            Assert.AreEqual(CrowdMood.Anticipation, director.Mood,
                "Bóng còn đang bay mà khán đài đã ăn mừng là lỗi ai cũng thấy.");

            director.OnOutcomeResolved(ShotOutcome.Goal);
            Assert.AreEqual(CrowdMood.Celebrate, director.Mood, "Bóng vào lưới thì khán đài phải nhảy lên.");

            director.OnKickPhaseChanged(KickPhase.Reaction);
            Assert.AreEqual(CrowdMood.Celebrate, director.Mood, "Pha ăn mừng phải giữ nguyên cảm xúc bàn thắng.");

            // Lượt kế tiếp: quên kết quả cũ, quay lại im lặng.
            director.OnKickPhaseChanged(KickPhase.Placing);
            Assert.AreEqual(CrowdMood.Hushed, director.Mood,
                "Lượt mới mà khán đài còn ăn mừng bàn thắng lượt trước.");

            director.OnOutcomeResolved(ShotOutcome.Saved);
            Assert.AreEqual(CrowdMood.Despair, director.Mood, "Bị thủ môn cản thì khán đài phải gục.");
        }

        [Test]
        public void BangAnhXaCamXuc_DayDu_ChoMoiPhaVaMoiKetQua()
        {
            foreach (ShotOutcome outcome in Enum.GetValues(typeof(ShotOutcome)))
            {
                bool laBanThang = outcome == ShotOutcome.Goal || outcome == ShotOutcome.PostIn;

                Assert.AreEqual(laBanThang, CrowdDirector.IsCelebration(outcome),
                    $"Kết quả {outcome} bị phân loại ăn mừng/thất vọng sai.");

                foreach (KickPhase phase in Enum.GetValues(typeof(KickPhase)))
                {
                    CrowdMood withOutcome = CrowdDirector.MoodFor(phase, outcome, true);
                    CrowdMood withoutOutcome = CrowdDirector.MoodFor(phase, outcome, false);

                    switch (phase)
                    {
                        case KickPhase.Placing:
                        case KickPhase.Complete:
                            Assert.AreEqual(CrowdMood.Hushed, withOutcome, $"{phase} phải im lặng.");
                            Assert.AreEqual(CrowdMood.Hushed, withoutOutcome, $"{phase} phải im lặng.");
                            break;

                        case KickPhase.Resolution:
                        case KickPhase.Reaction:
                            Assert.AreEqual(laBanThang ? CrowdMood.Celebrate : CrowdMood.Despair, withOutcome,
                                $"{phase} với kết quả {outcome} cho cảm xúc sai.");
                            Assert.AreEqual(CrowdMood.Anticipation, withoutOutcome,
                                $"{phase} khi CHƯA có kết quả thì không được đoán trước.");
                            break;

                        default:
                            Assert.AreEqual(CrowdMood.Anticipation, withOutcome, $"{phase} phải là chờ đợi.");
                            Assert.AreEqual(CrowdMood.Anticipation, withoutOutcome, $"{phase} phải là chờ đợi.");
                            break;
                    }
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Ô 4 — Pha animation lệch nhau theo instance
        // ─────────────────────────────────────────────────────────────────────────────

        [Test]
        public void PhaAnimation_LechNhauTheoInstance_KhongDongLoatNhuRobot()
        {
            var director = new CrowdDirector(QualityTier.A);
            director.SetMood(CrowdMood.Celebrate);

            float[] thoiDiemKiem = { 0f, 0.13f, 0.41f, 0.77f, 1.5f };

            foreach (float t in thoiDiemKiem)
            {
                var director2 = new CrowdDirector(QualityTier.A);
                director2.SetMood(CrowdMood.Celebrate);
                director2.Tick(t);

                var histogram = new int[CrowdAtlas.FramesPerMood];
                for (int i = 0; i < director2.InstanceCount; i++)
                {
                    histogram[director2.GetFrame(i)]++;
                }

                int nguongToiThieu = director2.InstanceCount / (CrowdAtlas.FramesPerMood * 4);

                for (int frame = 0; frame < histogram.Length; frame++)
                {
                    Assert.Greater(histogram[frame], nguongToiThieu,
                        $"Tại t={t}s chỉ có {histogram[frame]}/{director2.InstanceCount} khán giả ở khung {frame}. " +
                        "HẬU QUẢ: cả khán đài nhảy đồng loạt một nhịp như robot.");
                }
            }

            // Người ngồi cạnh nhau không được cùng khung hình quá nhiều — đó là thứ mắt bắt ngay.
            director.Tick(0.25f);
            int giongNhau = 0;
            for (int i = 1; i < director.InstanceCount; i++)
            {
                if (director.GetFrame(i) == director.GetFrame(i - 1)) giongNhau++;
            }

            float tiLe = (float)giongNhau / (director.InstanceCount - 1);
            Assert.Less(tiLe, 0.35f,
                $"{tiLe:P0} số cặp khán giả ngồi cạnh nhau đang ở cùng khung hình.");
        }

        [Test]
        public void KhanDai_TatDinhTheoSeed()
        {
            var a = new CrowdDirector(QualityTier.A, seed: 12345u);
            var b = new CrowdDirector(QualityTier.A, seed: 12345u);
            var c = new CrowdDirector(QualityTier.A, seed: 999u);

            Assert.AreEqual(a.InstanceCount, b.InstanceCount);

            bool khacSeedKhacNguoi = false;
            for (int i = 0; i < a.InstanceCount; i++)
            {
                Assert.AreEqual(a.Instances[i].position.x, b.Instances[i].position.x, 0f,
                    $"Ghế {i} lệch giữa hai lần dựng cùng seed — khán đài không tất định.");
                Assert.AreEqual(a.Instances[i].phase01, b.Instances[i].phase01, 0f);
                Assert.AreEqual(a.Instances[i].colorIndex, b.Instances[i].colorIndex);

                if (!khacSeedKhacNguoi && math.abs(a.Instances[i].phase01 - c.Instances[i].phase01) > 1e-6f)
                {
                    khacSeedKhacNguoi = true;
                }
            }

            Assert.IsTrue(khacSeedKhacNguoi, "Đổi seed mà khán đài y hệt — seed không có tác dụng.");
        }

        [Test]
        public void MoiGheNamNgoaiHopCameraDuocPhepDiVao()
        {
            var director = new CrowdDirector(QualityTier.A);

            for (int i = 0; i < director.InstanceCount; i++)
            {
                float3 pos = director.Instances[i].position;
                Assert.IsTrue(CrowdStandLayout.IsOutsideCameraBox(in pos),
                    $"Ghế {i} tại {pos} nằm TRONG hộp camera [-8..8, -5..15]. " +
                    "HẬU QUẢ: có góc quay chui vào giữa đám đông và lộ ra rằng họ chỉ là tấm phẳng.");
            }
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Ô 5 — Luôn hướng camera nhưng không lật khi camera đi qua ngang
        // ─────────────────────────────────────────────────────────────────────────────

        [Test]
        public void Billboard_LuonHuongCamera_VaKhongLatKhiCameraDiQuaNgang()
        {
            float3 seat = new float3(1.5f, 2.0f, 17.0f);
            const int buoc = 720;
            const float banKinh = 12.0f;

            float yaw = 0f;
            float3 rightTruoc = float3.zero;
            bool coRightTruoc = false;

            for (int i = 0; i <= buoc; i++)
            {
                float goc = (i / (float)buoc) * 2f * math.PI;

                // Camera đi trọn một vòng quanh ghế, đồng thời lên xuống qua cả mặt phẳng ngang —
                // đây chính là quỹ đạo làm billboard cầu lật ngược.
                float cao = seat.y + 6.0f * math.sin(goc * 2f);
                float3 cam = new float3(seat.x + banKinh * math.sin(goc), cao, seat.z + banKinh * math.cos(goc));

                yaw = CrowdBillboard.YawRadians(in seat, in cam, yaw);
                float3 normal = CrowdBillboard.Normal(yaw);
                float3 right = CrowdBillboard.Right(yaw);

                // 1. Luôn quay mặt về camera (xét trên mặt phẳng ngang).
                float3 toCameraFlat = math.normalize(new float3(cam.x - seat.x, 0f, cam.z - seat.z));
                Assert.Greater(math.dot(normal, toCameraFlat), 0.999f,
                    $"Bước {i}: tấm bảng không quay mặt về camera (dot = {math.dot(normal, toCameraFlat)}).");

                // 2. Không lật: hệ trục luôn thuận tay, right × up == normal ở MỌI bước.
                float3 tichHuong = math.cross(right, CrowdBillboard.Up);
                Assert.Greater(math.dot(tichHuong, normal), 0.999f,
                    $"Bước {i}: hệ trục billboard đã đổi chiều — khán đài lật ngược.");

                // 3. Liên tục: không có bước nhảy đột ngột nào của vector phải.
                if (coRightTruoc)
                {
                    float cos = math.dot(right, rightTruoc);
                    Assert.Greater(cos, math.cos(math.radians(3.0f)),
                        $"Bước {i}: vector phải nhảy {math.degrees(math.acos(math.clamp(cos, -1f, 1f))):F1}° " +
                        "trong một khung hình — đó là cú lật mà ô nghiệm thu cấm.");
                }

                rightTruoc = right;
                coRightTruoc = true;

                // 4. Không bao giờ nghiêng: trục lên cố định tuyệt đối.
                Assert.AreEqual(0f, CrowdBillboard.Up.x, 0f);
                Assert.AreEqual(1f, CrowdBillboard.Up.y, 0f);
                Assert.AreEqual(0f, CrowdBillboard.Up.z, 0f);
            }
        }

        [Test]
        public void Billboard_CameraThangDinhDau_GiuNguyenGocCu_KhongGiat()
        {
            float3 seat = new float3(0f, 1.0f, 16.0f);
            float gocCu = 1.2345f;

            float3 camTrenDau = new float3(seat.x, seat.y + 9.0f, seat.z);
            float yaw = CrowdBillboard.YawRadians(in seat, in camTrenDau, gocCu);

            Assert.AreEqual(gocCu, yaw, 0f,
                "Camera thẳng đỉnh đầu mà góc bị đặt lại về 0 — cả khán đài giật một cái rồi quay về.");
        }

        [Test]
        public void Billboard_BienDoiDinh_GocNamODuoiChan()
        {
            float3 seat = new float3(2f, 1.5f, 16f);
            float scale = 1.7f;
            float yaw = 0.7f;

            float3 chanTrai = CrowdBillboard.TransformVertex(in seat, scale, yaw, new float2(-0.5f, 0f));
            float3 chanPhai = CrowdBillboard.TransformVertex(in seat, scale, yaw, new float2(0.5f, 0f));
            float3 dinhDau = CrowdBillboard.TransformVertex(in seat, scale, yaw, new float2(0f, 1f));

            Assert.AreEqual(seat.y, chanTrai.y, 1e-5f, "Chân trái phải chạm đúng cao độ ghế.");
            Assert.AreEqual(seat.y, chanPhai.y, 1e-5f, "Chân phải phải chạm đúng cao độ ghế.");
            Assert.AreEqual(seat.y + scale, dinhDau.y, 1e-5f, "Đỉnh đầu phải cao đúng bằng scale mét.");

            float beRong = math.distance(chanTrai, chanPhai);
            Assert.AreEqual(scale * CrowdBillboard.QuadAspect, beRong, 1e-5f,
                "Bề ngang tấm bảng không khớp tỉ lệ đã khai báo.");
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Ô 6 — Bậc C dùng khán giả tĩnh, vẫn còn hình
        // ─────────────────────────────────────────────────────────────────────────────

        [Test]
        public void BacC_KhanGiaTinh_VanConHinh_KhongBienMat()
        {
            var bacA = new CrowdDirector(QualityTier.A);
            var bacC = new CrowdDirector(QualityTier.C);

            var settingsC = CrowdTierSettings.ForTier(QualityTier.C);
            Assert.IsTrue(settingsC.visible, "Bậc C vẫn phải vẽ khán giả — khán đài trống là thứ nhìn thấy ngay.");
            Assert.IsFalse(settingsC.animated, "Bậc C phải đóng băng animation.");

            Assert.AreEqual(bacA.InstanceCount, bacC.InstanceCount,
                "Bậc C bị mất khán giả so với bậc A — ô nghiệm thu đòi 'vẫn còn hình, không biến mất'.");

            bacC.SetMood(CrowdMood.Celebrate);
            bacC.Tick(5.0f);

            Assert.AreEqual(0f, bacC.AnimationTime, 0f, "Đồng hồ animation bậc C không được chạy.");

            for (int i = 0; i < bacC.InstanceCount; i++)
            {
                Assert.AreEqual(0, bacC.GetFrame(i),
                    $"Khán giả {i} ở bậc C đang chạy animation (khung {bacC.GetFrame(i)}).");
            }

            CrowdRenderBatch batch = bacC.BuildBatch();
            Assert.AreEqual(1, batch.drawCallCount, "Bậc C vẫn phải là một draw call.");
            Assert.IsFalse(batch.animated);
            Assert.AreEqual(bacC.InstanceCount, batch.instanceCount);

            // Đổi bậc lúc đang chạy: cùng đám đông, chỉ khác animation.
            bacC.ApplyTier(QualityTier.A);
            Assert.IsTrue(bacC.IsAnimated, "Nâng bậc lúc chạy phải bật lại animation.");
            Assert.AreEqual(bacA.InstanceCount, bacC.InstanceCount, "Đổi bậc không được sinh lại khán đài.");
        }

        /// <summary>Số dòng KHÔNG phải chú thích có chứa <paramref name="needle"/>.</summary>
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

        /// <summary>
        /// Đếm số DÒNG bắt đầu bằng <paramref name="prefix"/> sau khi bỏ thụt lề, bỏ qua dòng
        /// chú thích. Đếm như vậy để phân biệt khai báo tài nguyên với lần dùng nó trong thân
        /// hàm — "TEXTURE2D(" cũng nằm trong "SAMPLE_TEXTURE2D(".
        /// </summary>
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
    }
}
