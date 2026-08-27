using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine.TestTools.Constraints;
using Eleven.Ball;
using Eleven.Keeper;
using Eleven.Match;
using Is = NUnit.Framework.Is;
using Random = Unity.Mathematics.Random;

namespace Eleven.Tests.EditMode
{
    /// <summary>
    /// T21 — Phân giải pha cản phá.
    ///
    /// Năm mục nghiệm thu của backlog: (1) bóng nhanh khó bắt dính hơn, (2) deflectVelocity
    /// không bao giờ nhanh hơn lúc tới, (3) tay xa hơn tầm với thì LUÔN Missed không có ngoại
    /// lệ ngẫu nhiên, (4) cùng seed cùng kết quả, (5) phân bố trên mẫu lớn khớp parryChance
    /// dưới 3%.
    ///
    /// Hai nhóm test đầu (GoalFrame, KeeperReach) không nằm trong checklist gốc nhưng bắt buộc
    /// phải có: hợp đồng T21 nhận handDistanceToBall làm THAM SỐ VÀO mà trong repo không có gì
    /// sinh ra nó, nên nếu không ràng phần hình học tay–bóng thì SaveResolver dù đúng vẫn không
    /// ai gọi được, và mọi con số tỉ lệ cản phá sau này đều vô nghĩa.
    ///
    /// DIỄN GIẢI MỤC (5) — cần ghi rõ vì đề bài không nói: "khớp parryChance" ở đây là
    /// tỉ lệ KHÔNG BẮT DÍNH trong số các quả CHẠM ĐƯỢC, đo ở tốc độ neo NominalSpeed và ở
    /// chất lượng tiếp xúc tốt. Ở tốc độ khác thì mục (1) yêu cầu con số này phải LỆCH đi —
    /// hai mục sẽ mâu thuẫn nhau nếu không có một tốc độ neo.
    /// </summary>
    public class SaveResolverTests
    {
        const float Tol = 1e-4f;

        readonly List<KeeperProfile> _temp = new List<KeeperProfile>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _temp.Count; i++)
                if (_temp[i] != null)
                    UnityEngine.Object.DestroyImmediate(_temp[i]);
            _temp.Clear();
        }

        KeeperProfile Easy() { var p = KeeperProfile.CreateEasy(); _temp.Add(p); return p; }
        KeeperProfile Medium() { var p = KeeperProfile.CreateMedium(); _temp.Add(p); return p; }
        KeeperProfile Hard() { var p = KeeperProfile.CreateHard(); _temp.Add(p); return p; }

        KeeperProfile[] AllProfiles() => new[] { Easy(), Medium(), Hard() };

        static DiveDecision Dive(int cell, bool fullDive = true)
        {
            DiveDecision d;
            d.targetCell = cell;
            d.commitTime = 0f;
            d.isFullDive = fullDive;
            return d;
        }

        static BallState Ball(float3 position, float speed)
        {
            // Bay thẳng theo +Z: mọi test ở đây quan tâm tới ĐỘ LỚN vận tốc và hình học mặt
            // phẳng, không quan tâm quỹ đạo cong — quỹ đạo là việc của T06/T10.
            return new BallState(position, new float3(0f, 0f, speed), float3.zero);
        }

        // ------------------------------------------------------------------
        // Nhóm 0 — GoalFrame là nguồn sự thật duy nhất
        // ------------------------------------------------------------------

        /// <summary>
        /// GoalGeometry (Eleven.Match) nay chỉ chuyển tiếp lên GoalFrame (Eleven.Keeper).
        /// Test này tồn tại để nếu ai đó gõ lại số vào một trong hai nơi thì vỡ ngay, chứ
        /// không im lặng tạo ra hai khung thành khác kích thước ở hai assembly.
        /// </summary>
        [Test]
        public void GoalFrame_KhopGoalGeometry_MotNguonSuThat()
        {
            Assert.AreEqual(GoalGeometry.Width, GoalFrame.Width, Tol, "Width lệch");
            Assert.AreEqual(GoalGeometry.Height, GoalFrame.Height, Tol, "Height lệch");
            Assert.AreEqual(GoalGeometry.PostRadius, GoalFrame.PostRadius, Tol, "PostRadius lệch");
            Assert.AreEqual(GoalGeometry.PenaltyDistance, GoalFrame.PenaltyDistance, Tol, "PenaltyDistance lệch");
        }

        [Test]
        public void GoalFrame_CellOfVaCellCenter_KhopGoalGeometry()
        {
            for (int cell = 0; cell < 9; cell++)
            {
                float3 a = GoalFrame.CellCenter(cell);
                float3 b = GoalGeometry.CellCenter(cell);
                Assert.AreEqual(b.x, a.x, Tol, $"CellCenter({cell}).x lệch");
                Assert.AreEqual(b.y, a.y, Tol, $"CellCenter({cell}).y lệch");
                Assert.AreEqual(b.z, a.z, Tol, $"CellCenter({cell}).z lệch");
            }

            int checkedPoints = 0;
            for (float x = -4f; x <= 4f; x += 0.13f)
            for (float y = -0.5f; y <= 3f; y += 0.11f)
            {
                var pt = new float3(x, y, GoalFrame.PenaltyDistance);
                Assert.AreEqual(GoalGeometry.CellOf(pt), GoalFrame.CellOf(pt), $"CellOf lệch tại ({x}, {y})");
                checkedPoints++;
            }

            Assert.Greater(checkedPoints, 1000, "Vòng quét quá thưa — test này gần như không kiểm gì");
        }

        // ------------------------------------------------------------------
        // Nhóm 1 — KeeperReach: hình học tay–bóng (phần T21 còn thiếu)
        // ------------------------------------------------------------------

        /// <summary>
        /// Bất biến cốt lõi: T21 KHÔNG được định nghĩa lại "kịp hay không kịp". Tay chạm tới
        /// đúng tâm ô (progress = 1) khi và chỉ khi ReachEnvelope.CanReach của T16 nói kịp.
        /// Nếu bất biến này vỡ thì T21 đang âm thầm làm thủ môn khoẻ hơn hoặc yếu hơn T16.
        /// </summary>
        [Test]
        public void ReachProgress_DatMot_KhiVaChiKhi_CanReach()
        {
            int compared = 0;
            int reachable = 0;

            foreach (var p in AllProfiles())
            for (int cell = 0; cell < 9; cell++)
            for (float arrival = 0.05f; arrival <= 1.60f; arrival += 0.017f)
            {
                float reach = ReachEnvelope.TimeToReach(cell, p);
                float budget = arrival - p.commitOffsetMs * 0.001f - p.reactionMs * 0.001f;

                // Bỏ qua đúng lưỡi dao: sát biên thì phép chia rồi saturate có thể lệch 1 ULP
                // so với phép so sánh cộng trừ của CanReach. Đó là nhiễu số học, không phải
                // khác biệt về luật — ràng nó vào sẽ tạo ra test dở chứng.
                if (math.abs(budget - reach) <= 1e-4f)
                    continue;

                bool canReach = ReachEnvelope.CanReach(cell, arrival, p);
                bool progressFull = KeeperReach.ReachProgress(cell, arrival, p) >= 1f;

                Assert.AreEqual(canReach, progressFull,
                    $"Lệch tại profile reach={p.reachScale} cell={cell} arrival={arrival:F3}");

                compared++;
                if (canReach) reachable++;
            }

            Assert.Greater(compared, 2000, "Quét quá ít trường hợp");
            Assert.Greater(reachable, 100, "Không có ca nào kịp — test đang chỉ kiểm một phía");
            Assert.Less(reachable, compared - 100, "Không có ca nào không kịp — test đang chỉ kiểm một phía");
        }

        [Test]
        public void HandPositionAt_KhiKipDayDu_NamDungTamO()
        {
            var p = Hard();
            for (int cell = 0; cell < 9; cell++)
            {
                // 5 giây là thừa thãi so với mọi TimeToReach, nên chắc chắn progress = 1.
                float3 hand = KeeperReach.HandPositionAt(cell, 5f, p);
                float3 center = GoalFrame.CellCenter(cell);
                Assert.AreEqual(center.x, hand.x, Tol, $"cell {cell} x");
                Assert.AreEqual(center.y, hand.y, Tol, $"cell {cell} y");
            }
        }

        [Test]
        public void HandPositionAt_KhiKhongCoThoiGian_VanODiemChuanBi()
        {
            var p = Easy();
            for (int cell = 0; cell < 9; cell++)
            {
                float3 hand = KeeperReach.HandPositionAt(cell, 0f, p);
                Assert.AreEqual(0f, hand.x, Tol, $"cell {cell} x");
                Assert.AreEqual(KeeperReach.RestHandHeight, hand.y, Tol, $"cell {cell} y");
            }
        }

        /// <summary>
        /// Khoảng cách phải đo trong mặt phẳng khung thành. Nếu ai đó đổi sang math.distance
        /// trên float3 thì sai số z của phép nội suy điểm cắt sẽ lẫn vào khoảng cách tay–bóng
        /// và làm thủ môn hụt tay vì một lý do hoàn toàn không có thật.
        /// </summary>
        [Test]
        public void HandDistanceToBall_DoTrongMatPhang_BoQuaZ()
        {
            var p = Medium();
            var dive = Dive(3);
            float baseline = KeeperReach.HandDistanceToBall(dive, new float3(-2f, 1.4f, GoalFrame.PenaltyDistance), 1f, p);

            foreach (float z in new[] { 10.5f, 10.9f, 11f, 11.3f, 12f })
            {
                float d = KeeperReach.HandDistanceToBall(dive, new float3(-2f, 1.4f, z), 1f, p);
                Assert.AreEqual(baseline, d, Tol, $"z = {z} làm đổi khoảng cách");
            }
        }

        [Test]
        public void CatchRadius_BayNguoi_NhoHon_DungTaiCho()
        {
            foreach (var p in AllProfiles())
            {
                float dive = KeeperReach.CatchRadius(true, p);
                float stand = KeeperReach.CatchRadius(false, p);
                Assert.Less(dive, stand,
                    "Bay người hết tầm chỉ với được bằng đầu ngón tay của một tay; đứng tại chỗ " +
                    "cản được bằng cả hai tay, thân và chân — bán kính phải lớn hơn");
                Assert.Greater(dive, 0f);
            }
        }

        [Test]
        public void CatchRadius_TangTheoReachScale_VaKepTrongDaiCuaT16()
        {
            var weak = Easy();     // reachScale 0.92
            var strong = Hard();   // reachScale 1.06
            Assert.Less(KeeperReach.CatchRadius(true, weak), KeeperReach.CatchRadius(true, strong));

            var absurd = Medium();
            absurd.reachScale = 99f;
            Assert.AreEqual(KeeperReach.DiveCatchRadius * ReachEnvelope.MaxReachScale,
                            KeeperReach.CatchRadius(true, absurd), Tol,
                            "reachScale phải bị kẹp trần đúng như ReachEnvelope kẹp");

            absurd.reachScale = 0.01f;
            Assert.AreEqual(KeeperReach.DiveCatchRadius * ReachEnvelope.MinReachScale,
                            KeeperReach.CatchRadius(true, absurd), Tol,
                            "reachScale phải bị kẹp sàn đúng như ReachEnvelope kẹp");
        }

        [Test]
        public void KeeperReach_KhongCapPhatGC()
        {
            var p = Medium();
            var dive = Dive(5);
            var point = new float3(2.5f, 1.1f, GoalFrame.PenaltyDistance);
            float sink = 0f;

            // Gọi trước một lần để JIT biên dịch xong: lần gọi đầu tiên của một phương thức
            // luôn cấp phát cho bản thân việc biên dịch, và Is.Not.AllocatingGCMemory() đếm
            // cả phần đó. Không hâm nóng thì test đỏ giả mỗi khi assembly vừa được build lại.
            sink += KeeperReach.HandDistanceToBall(dive, point, 0.44f, p);
            sink += KeeperReach.CatchRadius(true, p);
            sink += KeeperReach.ReachProgress(5, 0.44f, p);
            sink = 0f;

            Assert.That(() =>
            {
                sink += KeeperReach.HandDistanceToBall(dive, point, 0.44f, p);
                sink += KeeperReach.CatchRadius(true, p);
                sink += KeeperReach.ReachProgress(5, 0.44f, p);
            }, Is.Not.AllocatingGCMemory());

            Assert.AreNotEqual(0f, sink, "Vòng lặp bị tối ưu hoá mất thì test không đo gì cả");
        }

        // ------------------------------------------------------------------
        // Nhóm 2 — SaveResolver: mục nghiệm thu 3 (hụt tầm là tuyệt đối)
        // ------------------------------------------------------------------

        /// <summary>
        /// Mục 3: xa hơn tầm với thì LUÔN Missed, không có ngoại lệ ngẫu nhiên.
        /// Quét nhiều seed vì đúng thứ cần loại trừ là "thỉnh thoảng may mắn cản được".
        /// </summary>
        [Test]
        public void TayXaHonTamVoi_LuonMissed_KhongCoNgoaiLeNgauNhien()
        {
            var seedRng = new Random(0x51D3u);
            int cases = 0;

            foreach (var p in AllProfiles())
            foreach (bool fullDive in new[] { true, false })
            {
                float maxReach = KeeperReach.CatchRadius(fullDive, p);
                var dive = Dive(4, fullDive);

                foreach (float d in new[] { maxReach + 1e-3f, maxReach * 1.01f, maxReach + 0.5f, maxReach * 4f, 100f })
                for (int i = 0; i < 200; i++)
                {
                    uint seed = seedRng.NextUInt();
                    var result = SaveResolver.Resolve(Ball(new float3(0f, 1.22f, 11f), 25f), dive, d, p, seed,
                                                      out float3 deflect);

                    Assert.AreEqual(SaveResult.Missed, result,
                        $"d={d:F3} > tầm với {maxReach:F3} mà vẫn không phải Missed (seed {seed})");
                    Assert.AreEqual(0f, math.length(deflect), Tol, "Không chạm được thì không được đổi hướng bóng");
                    cases++;
                }
            }

            Assert.Greater(cases, 5000, "Quét quá ít seed để dám kết luận là không có ngoại lệ");
        }

        [Test]
        public void HandDistanceLaNaN_CoiLaHutTam_KhongNemLoi()
        {
            var p = Medium();
            Assert.AreEqual(SaveResult.Missed,
                SaveResolver.Resolve(Ball(new float3(0f, 1.22f, 11f), 25f), Dive(4), float.NaN, p, 7u, out float3 d1));
            Assert.AreEqual(0f, math.length(d1), Tol);

            // Khoảng cách âm là vô nghĩa về vật lý; im lặng coi là "chạm được" sẽ tặng không
            // thủ môn một pha cản phá mỗi lần thượng nguồn tính sai dấu.
            Assert.AreEqual(SaveResult.Missed,
                SaveResolver.Resolve(Ball(new float3(0f, 1.22f, 11f), 25f), Dive(4), -0.2f, p, 7u, out float3 d2));
            Assert.AreEqual(0f, math.length(d2), Tol);
        }

        [Test]
        public void TayVuaDuTamVoi_KhongBaoGioMissed()
        {
            var seedRng = new Random(0x7A11u);
            var p = Medium();

            foreach (bool fullDive in new[] { true, false })
            {
                float maxReach = KeeperReach.CatchRadius(fullDive, p);
                var dive = Dive(4, fullDive);

                foreach (float d in new[] { maxReach, maxReach * 0.999f, maxReach * 0.5f, 0f })
                for (int i = 0; i < 200; i++)
                {
                    var result = SaveResolver.Resolve(Ball(new float3(0f, 1.22f, 11f), 25f), dive, d, p,
                                                      seedRng.NextUInt(), out _);
                    Assert.AreNotEqual(SaveResult.Missed, result,
                        $"d={d:F3} nằm trong tầm với {maxReach:F3} mà vẫn Missed");
                }
            }
        }

        /// <summary>
        /// Chạm được bằng đầu ngón tay thì không thể ôm gọn. Nếu ngưỡng FingertipQuality bị
        /// bỏ đi, test này đỏ — và nếu không có nó thì một pha vươn hết cỡ chạm hờ vào bóng
        /// vẫn có thể ra kết quả "bắt dính", điều không xảy ra trên sân.
        /// </summary>
        [Test]
        public void ChamDauNgonTay_KhongBaoGioBatDinh()
        {
            var seedRng = new Random(0xF17Eu);
            var p = Hard(); // parryChance thấp nhất (0.28) — dễ ra Caught nhất, nên là ca khó nhất
            float maxReach = KeeperReach.CatchRadius(true, p);

            for (int i = 0; i < 1000; i++)
            {
                var result = SaveResolver.Resolve(Ball(new float3(0f, 1.22f, 11f), 25f), Dive(4),
                                                  maxReach * 0.97f, p, seedRng.NextUInt(), out _);
                Assert.AreNotEqual(SaveResult.Caught, result, "Chạm hờ đầu ngón tay mà vẫn bắt dính");
            }
        }

        // ------------------------------------------------------------------
        // Nhóm 3 — mục nghiệm thu 4 (tất định)
        // ------------------------------------------------------------------

        [Test]
        public void CungSeed_ChoCungKetQua_VaCungVectorBatRa()
        {
            var p = Medium();
            var ball = Ball(new float3(1.1f, 1.6f, 11f), 27f);
            var dive = Dive(1, false);

            var first = SaveResolver.Resolve(ball, dive, 0.31f, p, 123456u, out float3 firstDeflect);

            for (int i = 0; i < 200; i++)
            {
                var again = SaveResolver.Resolve(ball, dive, 0.31f, p, 123456u, out float3 againDeflect);
                Assert.AreEqual(first, again, $"Lần lặp {i} cho kết quả khác");
                Assert.AreEqual(firstDeflect.x, againDeflect.x, 0f, "deflect.x khác");
                Assert.AreEqual(firstDeflect.y, againDeflect.y, 0f, "deflect.y khác");
                Assert.AreEqual(firstDeflect.z, againDeflect.z, 0f, "deflect.z khác");
            }
        }

        /// <summary>
        /// Mặt kia của tính tất định: một cài đặt bỏ qua seed cũng "cùng seed cùng kết quả"
        /// một cách tầm thường. Test này bắt buộc seed phải thật sự có tác dụng.
        /// </summary>
        [Test]
        public void DoiSeed_ThiKetQuaPhaiThayDoi_KhongPhaiHangSo()
        {
            var p = Medium();
            var seedRng = new Random(0xBEEFu);
            var seen = new HashSet<SaveResult>();

            for (int i = 0; i < 300; i++)
                seen.Add(SaveResolver.Resolve(Ball(new float3(0f, 1.22f, 11f), 25f), Dive(4),
                                              0.1f, p, seedRng.NextUInt(), out _));

            Assert.Greater(seen.Count, 1, "Mọi seed cho cùng một kết quả — seed đang bị bỏ qua");
        }

        [Test]
        public void SeedBangKhong_KhongNemLoi()
        {
            // Unity.Mathematics.Random(0) ném lỗi; cài đặt phải tự chặn thay vì đổ lên người gọi.
            Assert.DoesNotThrow(() =>
                SaveResolver.Resolve(Ball(new float3(0f, 1.22f, 11f), 25f), Dive(4), 0.1f, Medium(), 0u, out _));
        }

        [Test]
        public void ProfileNull_KhongNemNullReference()
        {
            Assert.DoesNotThrow(() =>
            {
                var r = SaveResolver.Resolve(Ball(new float3(0f, 1.22f, 11f), 25f), Dive(4), 0.1f, null, 99u, out _);
                Assert.AreNotEqual(SaveResult.Missed, r, "Tay ngay trên bóng mà vẫn hụt");
            });
        }

        // ------------------------------------------------------------------
        // Nhóm 4 — mục nghiệm thu 1 (bóng nhanh khó bắt dính hơn)
        // ------------------------------------------------------------------

        [Test]
        public void BongCangNhanh_CangItBatDinh()
        {
            var p = Medium();
            float[] speeds = { 15f, 20f, 25f, 30f, 35f };
            var rates = new float[speeds.Length];

            for (int s = 0; s < speeds.Length; s++)
            {
                var seedRng = new Random(0xC0FFEEu);
                int caught = 0;
                const int n = 4000;

                for (int i = 0; i < n; i++)
                    if (SaveResolver.Resolve(Ball(new float3(0f, 1.22f, 11f), speeds[s]), Dive(4), 0.05f, p,
                                             seedRng.NextUInt(), out _) == SaveResult.Caught)
                        caught++;

                rates[s] = caught / (float)n;
            }

            UnityEngine.Debug.Log($"[T21] Tỉ lệ bắt dính theo tốc độ (Thường): " +
                                  $"15={rates[0]:P1} 20={rates[1]:P1} 25={rates[2]:P1} 30={rates[3]:P1} 35={rates[4]:P1}");

            for (int s = 1; s < speeds.Length; s++)
                Assert.Less(rates[s], rates[s - 1],
                    $"Bắt dính ở {speeds[s]} m/s không thấp hơn ở {speeds[s - 1]} m/s");

            Assert.Greater(rates[0] - rates[speeds.Length - 1], 0.20f,
                "Chênh lệch quá nhỏ để gọi là 'bóng nhanh khó bắt hơn' — gần như phẳng");
        }

        // ------------------------------------------------------------------
        // Nhóm 5 — mục nghiệm thu 2 (deflectVelocity hợp lý)
        // ------------------------------------------------------------------

        [Test]
        public void DeflectVelocity_KhongBaoGioNhanhHonLucToi()
        {
            var seedRng = new Random(0x0DEFu);
            int touched = 0;

            foreach (var p in AllProfiles())
            foreach (float speed in new[] { 8f, 15f, 22f, 25f, 30f, 38f })
            foreach (float d in new[] { 0f, 0.1f, 0.25f, 0.4f, 0.52f })
            for (int cell = 0; cell < 9; cell++)
            for (int i = 0; i < 12; i++)
            {
                var ball = Ball(GoalFrame.CellCenter(cell) + new float3(0.13f, -0.07f, 0f), speed);
                var result = SaveResolver.Resolve(ball, Dive(cell), d, p, seedRng.NextUInt(), out float3 deflect);

                Assert.LessOrEqual(math.length(deflect), speed + 1e-3f,
                    $"Bóng bật ra nhanh hơn lúc tới ({math.length(deflect):F3} > {speed:F3}) — thủ môn đang bơm năng lượng");

                if (result == SaveResult.Caught || result == SaveResult.Missed)
                    Assert.AreEqual(0f, math.length(deflect), Tol,
                        $"{result} mà vẫn trả về vector bật ra");
                else
                    touched++;
            }

            Assert.Greater(touched, 500, "Quá ít ca thật sự chạm bóng — test gần như không kiểm gì");
        }

        /// <summary>
        /// Hướng bật ra phải là hướng RA XA BÀN TAY. Nếu phép phản xạ bị đảo dấu thì bóng sẽ
        /// bị hút về phía tay — nhìn thì vẫn "có vector", chỉ sai chiều, và không test nào
        /// khác trong file này bắt được.
        /// </summary>
        [Test]
        public void DeflectVelocity_DayBongRaXaBanTay()
        {
            var p = Medium();
            p.parryChance = 1f; // ép luôn vào nhánh đẩy ra để test hướng, không phải test xác suất

            var right = SaveResolver.Resolve(Ball(new float3(0.4f, 1.22f, 11f), 25f), Dive(4), 0.1f, p, 11u, out float3 dRight);
            var left = SaveResolver.Resolve(Ball(new float3(-0.4f, 1.22f, 11f), 25f), Dive(4), 0.1f, p, 11u, out float3 dLeft);

            Assert.AreNotEqual(SaveResult.Caught, right);
            Assert.AreNotEqual(SaveResult.Caught, left);
            Assert.Greater(dRight.x, 0f, "Bóng ở bên phải tay mà bị đẩy sang trái");
            Assert.Less(dLeft.x, 0f, "Bóng ở bên trái tay mà bị đẩy sang phải");

            // Đúng giữa lòng bàn tay thì không có hướng ngang nào cả — bóng phải bật ngược ra sân.
            SaveResolver.Resolve(Ball(GoalFrame.CellCenter(4), 25f), Dive(4), 0.05f, p, 11u, out float3 dCenter);
            Assert.Less(dCenter.z, 0f, "Chặn chính diện mà bóng vẫn bay tiếp vào lưới");
        }

        /// <summary>
        /// Chất lượng tiếp xúc phải quyết định HƯỚNG bật, không chỉ độ mạnh.
        /// Đấm trúng giữa lòng bàn tay thì bóng bật ngược ra sân; quệt hờ mép ngoài tầm với
        /// thì bóng chỉ đổi hướng chút ít và vẫn đi gần như song song vạch vôi.
        /// Nếu pháp tuyến bị cố định 45 độ (bản đặc tả đầu tiên của tôi làm đúng như vậy) thì
        /// ca chính diện cũng bắn bóng ngang 90 độ — test này là thứ ràng lỗi đó lại.
        /// </summary>
        [Test]
        public void HuongBatRa_PhuThuocChatLuongTiepXuc()
        {
            var p = Medium();
            p.parryChance = 1f;
            float maxReach = KeeperReach.CatchRadius(true, p);

            const float nearOffset = 0.05f;
            SaveResolver.Resolve(Ball(new float3(nearOffset, 1.22f, 11f), 25f), Dive(4), nearOffset, p, 3u,
                                 out float3 nearDeflect);
            Assert.Greater(math.abs(nearDeflect.z), math.abs(nearDeflect.x),
                "Chặn chính diện mà bóng bay ngang nhiều hơn bật ngược");

            float edgeOffset = maxReach * 0.95f;
            SaveResolver.Resolve(Ball(new float3(edgeOffset, 1.22f, 11f), 25f), Dive(4), edgeOffset, p, 3u,
                                 out float3 edgeDeflect);
            Assert.Greater(math.abs(edgeDeflect.x), math.abs(edgeDeflect.z),
                "Quệt hờ mép ngoài mà bóng vẫn bị bật ngược mạnh như đấm trúng lòng bàn tay");
        }

        [Test]
        public void OntoPost_KhiBongBiDayVaoCotDoc()
        {
            var p = Medium();
            p.parryChance = 1f;

            // Bóng qua vạch sát cột phải, thủ môn bay tới ô 5 (giữa-phải) nên tay ở phía TRONG
            // bóng: cú đẩy hất bóng tiếp ra phía cột.
            var result = SaveResolver.Resolve(Ball(new float3(3.5f, 1.22f, 11f), 25f), Dive(5), 0.1f, p, 5u, out float3 deflect);

            Assert.AreEqual(SaveResult.OntoPost, result, $"Kỳ vọng OntoPost, nhận {result} (deflect {deflect})");
            Assert.Greater(deflect.x, 0f, "Phải bị hất tiếp về phía cột");
        }

        [Test]
        public void OntoPost_KhongXayRaOGiuaKhung()
        {
            var p = Medium();
            p.parryChance = 1f;
            var seedRng = new Random(0x9051u);

            for (int i = 0; i < 500; i++)
            {
                var result = SaveResolver.Resolve(Ball(new float3(0f, 1.22f, 11f), 25f), Dive(4), 0.1f, p,
                                                  seedRng.NextUInt(), out _);
                Assert.AreNotEqual(SaveResult.OntoPost, result,
                    "Cách cột hơn 3 m mà vẫn báo bật cột — điều kiện hình học đang sai");
            }
        }

        // ------------------------------------------------------------------
        // Nhóm 6 — mục nghiệm thu 5 (phân bố khớp parryChance)
        // ------------------------------------------------------------------

        float NotCaughtRate(KeeperProfile p, int n, uint seed)
        {
            var seedRng = new Random(seed);
            int notCaught = 0;

            for (int i = 0; i < n; i++)
                if (SaveResolver.Resolve(Ball(new float3(0f, 1.22f, 11f), SaveResolver.NominalSpeed), Dive(4),
                                         0f, p, seedRng.NextUInt(), out _) != SaveResult.Caught)
                    notCaught++;

            return notCaught / (float)n;
        }

        [Test]
        public void PhanBo1000Luot_KhopParryChance_SaiSoDuoi3PhanTram()
        {
            foreach (var p in AllProfiles())
            {
                float rate = NotCaughtRate(p, 1000, 0x1000u);
                UnityEngine.Debug.Log($"[T21] parryChance={p.parryChance:F2} đo được (1000 lượt) = {rate:P2}");
                Assert.AreEqual(p.parryChance, rate, 0.03f,
                    $"Phân bố lệch quá 3% so với parryChance {p.parryChance:F2}");
            }
        }

        /// <summary>
        /// 1000 mẫu chỉ cho sai số chuẩn ~1.6 điểm phần trăm, nên ngưỡng 3% của backlog chỉ
        /// cách khoảng 1.9 sigma — vượt qua nó có thể là may. Mẫu lớn dưới đây mới là bằng
        /// chứng thật rằng phân bố đúng chứ không phải seed đẹp.
        /// </summary>
        [Test]
        public void PhanBoMauLon_KhopParryChance_SaiSoDuoi1PhanTram()
        {
            foreach (var p in AllProfiles())
            {
                float rate = NotCaughtRate(p, 20000, 0x2000u);
                UnityEngine.Debug.Log($"[T21] parryChance={p.parryChance:F2} đo được (20000 lượt) = {rate:P2}");
                Assert.AreEqual(p.parryChance, rate, 0.01f,
                    $"Phân bố lệch quá 1% so với parryChance {p.parryChance:F2} trên mẫu lớn");
            }
        }

        [Test]
        public void Resolve_KhongCapPhatGC()
        {
            var p = Medium();
            var ball = Ball(new float3(0.3f, 1.3f, 11f), 26f);
            var dive = Dive(4);
            int sink = 0;

            // Hâm nóng JIT trước khi đo — xem ghi chú ở KeeperReach_KhongCapPhatGC.
            sink += (int)SaveResolver.Resolve(ball, dive, 0.12f, p, 4242u, out _);
            sink += (int)SaveResolver.Resolve(ball, dive, 9f, p, 4242u, out _);
            sink = 0;

            Assert.That(() =>
            {
                sink += (int)SaveResolver.Resolve(ball, dive, 0.12f, p, 4242u, out _);
                sink += (int)SaveResolver.Resolve(ball, dive, 9f, p, 4242u, out _);
            }, Is.Not.AllocatingGCMemory());

            Assert.AreNotEqual(0, sink, "Vòng lặp bị tối ưu hoá mất thì test không đo gì cả");
        }
    }
}
