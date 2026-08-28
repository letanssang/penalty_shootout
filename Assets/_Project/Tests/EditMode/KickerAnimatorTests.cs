using System;
using System.Linq;
using Eleven.Match;
using Eleven.Presentation.Kicker;
using Eleven.Shooter;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = NUnit.Framework.Is;

namespace Eleven.Tests.EditMode
{
    /// <summary>
    /// Danh sách kiểm T35. Phần lớn chạy trên <see cref="KickerClipSelector"/> — thuần logic,
    /// không cần scene, không cần tài sản nghệ thuật — nên các kiểm thử này vẫn có giá trị
    /// nguyên vẹn khi thay X Bot bằng model thật.
    /// </summary>
    public class KickerAnimatorTests
    {
        static readonly ShotType[] AllShotTypes = (ShotType[])Enum.GetValues(typeof(ShotType));
        static readonly KickPhase[] AllPhases = (KickPhase[])Enum.GetValues(typeof(KickPhase));

        // ── 1. Bốn kiểu sút cho bốn clip khác nhau ─────────────────────────────────

        [Test]
        public void BonKieuSut_ChoBonClipKhacNhau()
        {
            var clips = AllShotTypes.Select(KickerClipSelector.StrikeFor).ToArray();

            Assert.That(clips.Length, Is.EqualTo(4), "ShotType phải có đúng 4 giá trị.");
            Assert.That(clips.Distinct().Count(), Is.EqualTo(4),
                "Hai kiểu sút đang trỏ về cùng một clip: " + string.Join(", ", clips));
        }

        [Test]
        public void MoiClipSut_LaMotGiaTriStrike()
        {
            foreach (var type in AllShotTypes)
            {
                var clip = KickerClipSelector.StrikeFor(type);
                var strikes = new[] { KickerClip.StrikeInstep, KickerClip.StrikeInsideFoot,
                                      KickerClip.StrikeChip, KickerClip.StrikeKnuckle };
                Assert.That(strikes, Does.Contain(clip),
                    $"{type} ánh xạ ra {clip} — không phải clip sút.");
            }
        }

        // ── 2. Khung chạm khớp số đo, sai không quá một khung ở 60 fps ─────────────

        [Test]
        public void ThoiDiemBatClipSut_NamGonTrongPhaRunUp()
        {
            var runUp = KickPhaseDurations.Default.runUp;

            foreach (var type in AllShotTypes)
            {
                var lead = MecanimKickerAnimator.StrikeLeadSeconds(type);
                Assert.That(lead, Is.GreaterThan(0f), $"{type}: khung chạm ở đầu clip là vô lý.");
                Assert.That(lead, Is.LessThanOrEqualTo(runUp + 1f / 60f),
                    $"{type}: clip sút cần bật trước {lead:F3}s nhưng pha RunUp chỉ dài {runUp:F3}s — " +
                    "khung chạm sẽ rơi muộn hơn lúc gameplay bắn bóng.");
            }
        }

        [Test]
        public void KhungCham_KhopSoDoTrongStrikeContactTsv()
        {
            // Sai số cho phép: một khung ở 60 fps của clip ngắn nhất (0.500 s) → 1/30 tỉ lệ.
            AssertLead(ShotType.Instep,     1.500f * 0.4889f);
            AssertLead(ShotType.InsideFoot, 0.500f * 0.8333f);
            AssertLead(ShotType.Chip,       0.567f * 0.9118f);
            AssertLead(ShotType.Knuckle,    1.500f * 0.4889f);   // tạm dùng lại PenaltyKick

            static void AssertLead(ShotType type, float expected)
                => Assert.That(MecanimKickerAnimator.StrikeLeadSeconds(type),
                               Is.EqualTo(expected).Within(1f / 60f),
                               $"{type}: lệch quá một khung so với docs/data/strike-contact.tsv.");
        }

        // ── 3. Không pha nào để lại tư thế kẹt ────────────────────────────────────

        [Test]
        public void MoiPha_ChoRaDungMotClip_KhongPhaNaoKet()
        {
            var selector = new KickerClipSelector();
            selector.Reset();

            foreach (var phase in AllPhases)
            {
                var clip = selector.Resolve(phase, 0f, 0.733f);
                Assert.That(Enum.IsDefined(typeof(KickerClip), clip), Is.True,
                    $"Pha {phase} cho ra giá trị KickerClip không hợp lệ.");
            }

            Assert.That(AllPhases.Length, Is.EqualTo(8), "KickPhase phải có đúng 8 pha.");
        }

        [Test]
        public void HuyGiuaChung_VeIdle_KhongGiuKetQuaLuotTruoc()
        {
            var selector = new KickerClipSelector();
            selector.PrepareFor(ShotType.Chip);
            selector.SetOutcome(KickResult.Scored);

            // Abort ném chuỗi pha về Complete/Placing; cả hai đều phải ra Idle.
            Assert.That(selector.Resolve(KickPhase.Complete, 0f, 0.5f), Is.EqualTo(KickerClip.Idle));

            selector.Reset();
            Assert.That(selector.Resolve(KickPhase.Placing, 0f, 0.5f), Is.EqualTo(KickerClip.Idle));
            Assert.That(selector.PendingShot, Is.EqualTo(ShotType.Instep));
            Assert.That(selector.Outcome, Is.EqualTo(KickResult.Pending));

            // Sau Reset mà vào Reaction thì đứng thở, KHÔNG ăn mừng bằng kết quả lượt cũ.
            Assert.That(selector.Resolve(KickPhase.Reaction, 0f, 0.5f),
                        Is.EqualTo(KickerClip.FollowThrough));
        }

        [Test]
        public void PhaReaction_ChonTheoKetQua()
        {
            var selector = new KickerClipSelector();

            selector.SetOutcome(KickResult.Scored);
            Assert.That(selector.Resolve(KickPhase.Reaction, 0f, 0.5f), Is.EqualTo(KickerClip.Celebrate));

            selector.SetOutcome(KickResult.Missed);
            Assert.That(selector.Resolve(KickPhase.Reaction, 0f, 0.5f), Is.EqualTo(KickerClip.Dejected));
        }

        [Test]
        public void PhaRunUp_DoiSangClipSut_DungLucConLaiBangThoiGianDan()
        {
            var selector = new KickerClipSelector();
            selector.PrepareFor(ShotType.Instep);
            const float lead = 0.733f;

            Assert.That(selector.Resolve(KickPhase.RunUp, lead + 0.05f, lead),
                        Is.EqualTo(KickerClip.RunUp), "Còn sớm thì phải đang chạy đà.");
            Assert.That(selector.Resolve(KickPhase.RunUp, lead, lead),
                        Is.EqualTo(KickerClip.StrikeInstep), "Đúng mốc thì phải vào clip sút.");
            Assert.That(selector.Resolve(KickPhase.RunUp, 0f, lead),
                        Is.EqualTo(KickerClip.StrikeInstep));
        }

        // ── 4. Tên state trùng tên enum ───────────────────────────────────────────

        [Test]
        public void TenStateTrongController_TrungTungKyTuVoiEnum()
        {
            const string path = "Assets/_Project/Art/Animations/KickerAnimator.controller";
            var controller = UnityEditor.AssetDatabase.LoadAssetAtPath<
                UnityEditor.Animations.AnimatorController>(path);
            Assert.That(controller, Is.Not.Null,
                $"Chưa dựng {path}. Chạy Eleven ▸ Art ▸ Build Kicker Animator Controller.");

            var states = controller.layers[0].stateMachine.states.Select(s => s.state.name).ToArray();
            foreach (var name in Enum.GetNames(typeof(KickerClip)))
                Assert.That(states, Does.Contain(name),
                    $"Thiếu state '{name}'. CrossFadeInFixedTime tra theo hash tên nên lệch một " +
                    "ký tự là im lặng không đổi tư thế.");

            Assert.That(states.Length, Is.EqualTo(9), "Thừa state: " + string.Join(", ", states));

            foreach (var s in controller.layers[0].stateMachine.states)
            {
                Assert.That(s.state.motion, Is.Not.Null, $"State '{s.state.name}' chưa gán clip.");
                Assert.That(s.state.transitions.Length, Is.EqualTo(0),
                    $"State '{s.state.name}' có transition — runtime lái bằng CrossFade, " +
                    "đồ thị transition chỉ là thứ thừa dễ hỏng ngầm.");
            }
        }

        // ── 5. Không cấp phát GC ──────────────────────────────────────────────────

        [Test]
        public void Resolve_KhongCapPhatGC()
        {
            var selector = new KickerClipSelector();
            selector.PrepareFor(ShotType.Knuckle);
            selector.Resolve(KickPhase.RunUp, 0.4f, 0.733f);   // hâm nóng JIT

            Assert.That(() =>
            {
                for (int i = 0; i < 64; i++)
                    selector.Resolve(KickPhase.RunUp, 0.4f, 0.733f);
            }, UnityEngine.TestTools.Constraints.Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void Tick_KhongCapPhatGC()
        {
            var go = new GameObject("kicker-gc", typeof(Animator));
            try
            {
                var anim = go.AddComponent<MecanimKickerAnimator>();
                var api = (IKickerAnimator)anim;
                api.PrepareFor(ShotType.Instep);
                api.OnPhaseChanged(KickPhase.Aiming, KickPhase.RunUp);
                api.Tick(1f / 60f, 0.5f);                       // hâm nóng JIT

                Assert.That(() =>
                {
                    for (int i = 0; i < 64; i++) api.Tick(1f / 60f, 0.5f);
                }, UnityEngine.TestTools.Constraints.Is.Not.AllocatingGCMemory());
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        // ── 6. Không bao giờ ghi vào vật lý ───────────────────────────────────────

        [Test]
        public void LopHoatAnh_KhongThamChieuKieuVatLy()
        {
            // Định luật Phase 7: hoạt ảnh nhận ShotType, không bao giờ lái quả bóng.
            // Soi cả assembly Presentation thay vì chỉ một lớp, để lần sau ai thêm lớp
            // hoạt ảnh mới cũng bị chặn.
            var banned = new[] { "BallDriver", "BallState", "BallSolver" };
            var types = typeof(MecanimKickerAnimator).Assembly.GetTypes()
                                                     .Where(t => t.Namespace == "Eleven.Presentation.Kicker");

            foreach (var type in types)
            {
                foreach (var f in type.GetFields((System.Reflection.BindingFlags)(-1)))
                    Assert.That(banned, Does.Not.Contain(f.FieldType.Name),
                        $"{type.Name}.{f.Name} giữ tham chiếu tới {f.FieldType.Name}.");

                foreach (var m in type.GetMethods((System.Reflection.BindingFlags)(-1)))
                foreach (var p in m.GetParameters())
                    Assert.That(banned, Does.Not.Contain(p.ParameterType.Name),
                        $"{type.Name}.{m.Name} nhận tham số {p.ParameterType.Name}.");
            }
        }
    }
}
