using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Unity.Mathematics;
using Eleven.Core;
using Eleven.Presentation.Skin;
using Is = NUnit.Framework.Is;

namespace Eleven.Tests.EditMode
{
    /// <summary>
    /// T31 — Shader da tán xạ dưới bề mặt.
    ///
    /// BỐN ô nghiệm thu của T31 KHÔNG kiểm được trong EditMode vì chúng đòi máy thật hoặc build
    /// thật: số đo 0.5ms trên máy bậc B, cặp ảnh so sánh bật/tắt SSS, kiểm biến thể trên build,
    /// và thời gian biên dịch ở màn hình đầu tiên. Test ở đây kiểm ba thứ EditMode kiểm được:
    ///   · vật lý của LUT (hàm thuần, tất định — GPU sẽ đọc đúng những con số này);
    ///   · cấu trúc file .shader (keyword khai đúng loại, số biến thể không vượt trần);
    ///   · và quan trọng nhất: KHÔNG có đường nào để mã tự hạ chất lượng khi vượt ngân sách.
    /// Xem docs/backlog/phase-5-trinh-dien.md mục T31 để biết ô nào cần người đo.
    /// </summary>
    [TestFixture]
    public class SkinSssTests
    {
        private static string ShaderPath =>
            Path.Combine(Application.dataPath, "_Project", "Art", "Shaders", "Skin.shader");

        /// <summary>
        /// Tên máy giả định dùng trong test đo cấp phát. Để ở trường tĩnh chứ KHÔNG viết thẳng
        /// trong vòng lặp: một literal chuỗi xuất hiện lần đầu trong một hàm sẽ được Mono nội suy
        /// (intern) ngay tại đó, và lần đó tính là một lần cấp phát GC. Đó là cấp phát MỘT LẦN
        /// của thời gian chạy, không phải rò rỉ mỗi khung hình — nhưng đủ để làm đỏ một test đo
        /// cấp phát và khiến người đọc đi tìm một lỗi không tồn tại.
        /// </summary>
        private static readonly string TenMayGiaDinh = "máy đo giả định";

        private static string DocShader()
        {
            Assert.IsTrue(File.Exists(ShaderPath),
                $"Không tìm thấy Skin.shader tại '{ShaderPath}'. HẬU QUẢ: mọi test đọc file dưới " +
                "đây sẽ XANH GIẢ nếu bỏ qua kiểm tra này.");
            return File.ReadAllText(ShaderPath);
        }

        private static TierProfile MakeProfile(QualityTier tier, bool sss)
        {
            var profile = ScriptableObject.CreateInstance<TierProfile>();
            profile.tier = tier;
            profile.subsurfaceScattering = sss;
            return profile;
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Ô 1 — HLSL, tương thích URP Forward+
        // ─────────────────────────────────────────────────────────────────────────────

        [Test]
        public void ShaderDa_DungVongLapDenPhanCum_ChuKhongTuVietVongLap()
        {
            string source = DocShader();

            Assert.AreEqual(1, DemDongCoChua(source, "_CLUSTER_LIGHT_LOOP"),
                "Phải có đúng MỘT dòng khai _CLUSTER_LIGHT_LOOP (keyword Forward+ của URP 17; " +
                "URP 6.1 đổi tên từ _FORWARD_PLUS). Thiếu nó thì shader chạy đường Forward cũ.");

            Assert.AreEqual(1, DemDongCoChua(source, "LIGHT_LOOP_BEGIN("),
                "Đèn phụ phải duyệt qua macro LIGHT_LOOP_BEGIN của URP.");
            Assert.AreEqual(1, DemDongCoChua(source, "LIGHT_LOOP_END"),
                "Thiếu LIGHT_LOOP_END thì macro không đóng ngoặc — lỗi biên dịch, nhưng bắt ở đây " +
                "rẻ hơn bắt lúc build.");

            // Macro dạng phân cụm đọc THẲNG hai trường của một biến tên inputData. Đặt tên khác
            // là lỗi biên dịch chỉ hiện ra khi _CLUSTER_LIGHT_LOOP bật — tức là chỉ trên build thật.
            Assert.AreEqual(1, DemDongCoChua(source, "InputData inputData"),
                "LIGHT_LOOP_BEGIN đọc inputData.normalizedScreenSpaceUV và inputData.positionWS, " +
                "nên biến PHẢI tên đúng là inputData.");
            Assert.AreEqual(1, DemDongCoChua(source, "inputData.normalizedScreenSpaceUV ="),
                "Chưa gán normalizedScreenSpaceUV thì ClusterInit đọc rác — đèn phụ sẽ nhấp nháy " +
                "theo vị trí điểm ảnh mà không có lỗi nào báo ra.");
            Assert.AreEqual(1, DemDongCoChua(source, "inputData.positionWS ="),
                "ClusterInit cần positionWS để tìm cụm đèn.");

            // Vòng lặp tự viết theo GetAdditionalLightsCount() chạy SAI trong Forward+:
            // ở chế độ phân cụm hàm đó trả về 0, danh sách đèn nằm trong bit list của cụm.
            Assert.AreEqual(0, DemDongCoChua(source, "lightIndex < "),
                "Không được tự viết vòng for theo GetAdditionalLightsCount(): trong Forward+ hàm " +
                "này trả về 0 và toàn bộ đèn phụ sẽ biến mất.");
        }

        [Test]
        public void ShaderDa_TenKhopVoiHangSoTrongMa()
        {
            string source = DocShader();

            Assert.AreEqual(1, DemDongCoChua(source, "Shader \"" + SkinShaderKeywords.ShaderName + "\""),
                $"Tên shader trong file phải đúng '{SkinShaderKeywords.ShaderName}' — mã lúc chạy " +
                "tìm shader theo tên này.");
        }

        [Test]
        public void ShaderDa_CoDuPassDungHinh_BongDo_VaChieuSau()
        {
            string source = DocShader();

            Assert.AreEqual(1, DemDongCoChua(source, "\"LightMode\" = \"UniversalForward\""),
                "Thiếu pass UniversalForward thì nhân vật không được vẽ.");
            Assert.AreEqual(1, DemDongCoChua(source, "\"LightMode\" = \"ShadowCaster\""),
                "Thiếu pass ShadowCaster thì nhân vật không đổ bóng — thủ môn sẽ như lơ lửng.");
            Assert.AreEqual(1, DemDongCoChua(source, "\"LightMode\" = \"DepthOnly\""),
                "Thiếu pass DepthOnly thì depth prepass và các hiệu ứng hậu kỳ đọc thiếu nhân vật.");
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Ô 2 — ngân sách ≤ 0.5ms cho 2 nhân vật, số đo thật từ máy bậc B
        // ─────────────────────────────────────────────────────────────────────────────

        [Test]
        public void NganSach_Dung05ms_ChoDungHaiNhanVat()
        {
            Assert.AreEqual(0.5f, SkinBudget.MaxGpuBudgetMs, 1e-6f,
                "Ngân sách T31 là 0.5ms. Sửa hằng số này là sửa ô nghiệm thu.");
            Assert.AreEqual(2, SkinBudget.CharacterCount,
                "Ngân sách áp cho HAI nhân vật: thủ môn và người sút.");
        }

        [Test]
        public void SoDo_ThieuTenMay_KhongTinhLaDaDo()
        {
            var thieuTen = new SkinGpuMeasurement
            {
                gpuMs = 0.31f,
                characterCount = 2,
                sssEnabled = true,
                tier = QualityTier.B,
                deviceName = ""      // "đo trên Android" không phải là một số đo
            };

            Assert.IsFalse(thieuTen.IsRecorded, "Số đo không có tên máy thì không so sánh được với gì.");
            Assert.AreEqual(SkinVerdict.ChuaDoDu, SkinBudgetCheck.Evaluate(in thieuTen),
                "Thiếu tên máy mà vẫn kết luận Đạt là tick khống ô nghiệm thu.");

            var duTen = thieuTen;
            duTen.deviceName = "Redmi Note 11 (Snapdragon 680)";
            Assert.IsTrue(duTen.IsRecorded);
            Assert.AreEqual(SkinVerdict.Dat, SkinBudgetCheck.Evaluate(in duTen));
        }

        [Test]
        public void SoDoMotNhanVat_PhaiQuyVeHaiNhanVat_TruocKhiKetLuan()
        {
            // Đo một nhân vật thấy 0.30ms — nghe như trong ngân sách 0.5ms. Nhưng ngân sách là
            // cho HAI nhân vật, tức là 0.60ms. Đây là cái bẫy mà kiểu dữ liệu phải chặn.
            var motNhanVat = new SkinGpuMeasurement
            {
                gpuMs = 0.30f,
                characterCount = 1,
                sssEnabled = true,
                tier = QualityTier.B,
                deviceName = "Galaxy A54 (Exynos 1380)"
            };

            Assert.AreEqual(0.60f, motNhanVat.NormalizedToTwoCharactersMs, 1e-5f);
            Assert.AreEqual(SkinVerdict.VuotNganSach_PhaiBaoCao, SkinBudgetCheck.Evaluate(in motNhanVat),
                "0.30ms cho một nhân vật là 0.60ms cho hai — vượt trần 0.5ms.");
        }

        [Test]
        public void VuotNganSach_PhaiBaoCao_KhongTuTatSss()
        {
            SkinSssSettings truoc = SkinSssSettings.ForTier(QualityTier.B);

            var vuot = new SkinGpuMeasurement
            {
                gpuMs = 1.4f,
                characterCount = 2,
                sssEnabled = true,
                tier = QualityTier.B,
                deviceName = "Galaxy A54 (Exynos 1380)"
            };

            Assert.AreEqual(SkinVerdict.VuotNganSach_PhaiBaoCao, SkinBudgetCheck.Evaluate(in vuot));

            // Đọc kết luận KHÔNG được đụng vào cấu hình. Ô nghiệm thu bắt báo cáo lại;
            // quyết định tắt SSS là của người, không phải của mã.
            SkinSssSettings sau = SkinSssSettings.ForTier(QualityTier.B);
            Assert.AreEqual(truoc.enabled, sau.enabled, "Đánh giá ngân sách đã tự tắt SSS.");
            Assert.AreEqual(truoc.sssStrength, sau.sssStrength, 1e-6f,
                "Đánh giá ngân sách đã tự hạ cường độ tán xạ.");
            Assert.AreEqual(truoc.transmission, sau.transmission,
                "Đánh giá ngân sách đã tự tắt ánh sáng xuyên.");
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Ô 3 — so sánh cạnh nhau, cùng góc cùng ánh sáng
        // ─────────────────────────────────────────────────────────────────────────────

        [Test]
        public void SoSanhCanhNhau_DuThongTinThiMoiTinhLaDaChup()
        {
            var du = new SkinSideBySideComparison
            {
                sssOnImagePath = "docs/anh/t31-sss-bat.png",
                sssOffImagePath = "docs/anh/t31-sss-tat.png",
                cameraSetup = "T26 shot 'ChoDoi', fov 38, cao 1.65m",
                lightingSetup = "đèn pha sân mặc định, 4 nguồn, không đổi giữa hai ảnh",
                deviceName = "Galaxy A54 (Exynos 1380)"
            };

            Assert.IsTrue(du.IsRecorded, "Cặp ảnh đủ thông tin mà vẫn không tính là đã chụp.");
        }

        [Test]
        public void SoSanhCanhNhau_ThieuGocHoacAnhSang_ThiKhongChungMinhDuocGi()
        {
            var mau = new SkinSideBySideComparison
            {
                sssOnImagePath = "a.png",
                sssOffImagePath = "b.png",
                cameraSetup = "T26 shot 'ChoDoi'",
                lightingSetup = "đèn pha sân mặc định",
                deviceName = "Galaxy A54"
            };

            var thieuGoc = mau; thieuGoc.cameraSetup = "";
            Assert.IsFalse(thieuGoc.IsRecorded,
                "Không ghi góc camera thì hai ảnh có thể chụp ở hai góc khác nhau — cặp ảnh vô nghĩa.");

            var thieuDen = mau; thieuDen.lightingSetup = "";
            Assert.IsFalse(thieuDen.IsRecorded,
                "Không ghi cấu hình đèn thì khác biệt nhìn thấy có thể chỉ là khác đèn.");

            var trungAnh = mau; trungAnh.sssOffImagePath = trungAnh.sssOnImagePath;
            Assert.IsFalse(trungAnh.IsRecorded,
                "Hai đường dẫn trùng nhau nghĩa là chỉ có MỘT ảnh — không có gì để so.");

            var thieuMay = mau; thieuMay.deviceName = "";
            Assert.IsFalse(thieuMay.IsRecorded, "Thiếu tên máy.");
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Ô 4 — không có shader variant nào bị strip nhầm
        // ─────────────────────────────────────────────────────────────────────────────

        [Test]
        public void BaKeyword_PhaiKhaiBangMultiCompile_KhongPhaiShaderFeature()
        {
            string source = DocShader();

            Assert.AreEqual(0, DemDongCoChua(source, "shader_feature"),
                "shader_feature bị LƯỢC khi build nếu không vật liệu nào trong build bật keyword đó. " +
                "Ở đây keyword được bật/tắt LÚC CHẠY theo bậc thiết bị, nên vật liệu trong build " +
                "không chứng minh được gì cho bộ lọc. Phải dùng multi_compile.");

            foreach (string keyword in SkinShaderKeywords.MustUseMultiCompile)
            {
                bool khaiBangMultiCompile = false;

                foreach (string dong in DongPragma(source))
                {
                    if (!dong.Contains(keyword)) continue;
                    if (dong.Contains("multi_compile")) khaiBangMultiCompile = true;
                }

                Assert.IsTrue(khaiBangMultiCompile,
                    $"Keyword '{keyword}' phải được khai bằng multi_compile trong Skin.shader. " +
                    "Bị lược thì nhân vật hoặc thành màu hồng, hoặc — tệ hơn — trông vẫn bình " +
                    "thường nhưng chạy nhánh sai.");
            }
        }

        [Test]
        public void KiemBienThe_ThieuBuildHoacMay_KhongTinhLaDaKiem()
        {
            var audit = new SkinVariantAudit();
            Assert.IsFalse(audit.IsRecorded, "Chưa ghi gì mà đã tính là đã kiểm.");

            foreach (SkinShaderVariant v in SkinVariantManifest.Required())
            {
                audit.RecordSurvivor(in v);
            }

            Assert.IsTrue(audit.AllRequiredSurvived());
            Assert.IsFalse(audit.IsRecorded,
                "Đủ biến thể nhưng chưa ghi build target và tên máy — ô nghiệm thu đòi 'kiểm bằng " +
                "build thật, không phải Editor', nên phải biết đó là build nào trên máy nào.");

            audit.buildTarget = "Android ARM64 IL2CPP Vulkan";
            audit.deviceName = "Galaxy A54 (Exynos 1380)";
            Assert.IsTrue(audit.IsRecorded);
        }

        [Test]
        public void KiemBienThe_ThieuMotBienThe_ThiKhongDat()
        {
            SkinShaderVariant[] batBuoc = SkinVariantManifest.Required();

            Assert.AreEqual(3, batBuoc.Length, "Ba bậc thiết bị, ba biến thể bắt buộc.");

            for (int boQua = 0; boQua < batBuoc.Length; boQua++)
            {
                var audit = new SkinVariantAudit
                {
                    buildTarget = "Android ARM64 IL2CPP Vulkan",
                    deviceName = "Galaxy A54 (Exynos 1380)"
                };

                for (int i = 0; i < batBuoc.Length; i++)
                {
                    if (i == boQua) continue;
                    audit.RecordSurvivor(in batBuoc[i]);
                }

                Assert.IsFalse(audit.AllRequiredSurvived(),
                    $"Thiếu biến thể '{batBuoc[boQua].Label}' mà vẫn kết luận đủ.");
            }
        }

        [Test]
        public void MoiBienThe_BatBuoc_ChayForwardPlus_VaKhacNhau()
        {
            SkinShaderVariant[] batBuoc = SkinVariantManifest.Required();
            var thay = new HashSet<SkinShaderVariant>();

            foreach (SkinShaderVariant v in batBuoc)
            {
                Assert.IsTrue(v.clusterLightLoop,
                    $"Biến thể '{v.Label}' không bật _CLUSTER_LIGHT_LOOP. Dự án chạy Forward+ ở " +
                    "CẢ BA bậc (docs/plan.md), nên không có biến thể nào chạy đường Forward cũ.");
                Assert.IsTrue(thay.Add(v), $"Biến thể '{v.Label}' bị lặp — ba bậc phải ra ba biến thể khác nhau.");
            }
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Ô 5 — tắt được ở bậc C qua TierProfile.subsurfaceScattering, về Lit thường
        // ─────────────────────────────────────────────────────────────────────────────

        [Test]
        public void BacC_TatSss_VeLitThuong()
        {
            SkinSssSettings c = SkinSssSettings.FromProfile(MakeProfile(QualityTier.C, sss: false));

            Assert.IsFalse(c.enabled, "Bậc C phải tắt tán xạ.");
            Assert.IsTrue(c.useLitFallback, "Bậc C phải về đường Lit thường (Lambert + GGX).");
            Assert.AreEqual(0f, c.sssStrength, 1e-6f);
            Assert.IsFalse(c.transmission, "Tắt tán xạ thì ánh sáng xuyên cũng phải tắt theo.");

            SkinKeywordSet keywords = SkinKeywordSet.For(in c);
            Assert.IsFalse(keywords.sss);
            Assert.IsFalse(keywords.transmission);
            Assert.AreEqual(0, keywords.EnabledCount,
                "Bậc C không được bật keyword riêng nào của T31.");
        }

        [Test]
        public void CoTrongProfile_ThangBangMacDinhCuaBac()
        {
            // Bậc A theo mặc định BẬT tán xạ. Nhưng người dùng tắt trong profile thì phải tắt thật —
            // ô nghiệm thu nói tắt "qua TierProfile.subsurfaceScattering", không phải "ở bậc C".
            SkinSssSettings aTat = SkinSssSettings.FromProfile(MakeProfile(QualityTier.A, sss: false));

            Assert.IsFalse(aTat.enabled,
                "Cờ subsurfaceScattering=false trong profile phải thắng bảng mặc định của bậc A.");
            Assert.IsTrue(aTat.useLitFallback);
            Assert.IsFalse(aTat.transmission);
            Assert.AreEqual(0, SkinKeywordSet.For(in aTat).EnabledCount);

            // Và chiều ngược lại vẫn đi qua profile chứ không đọc hằng số trong mã.
            SkinSssSettings bBat = SkinSssSettings.FromProfile(MakeProfile(QualityTier.B, sss: true));
            Assert.IsTrue(bBat.enabled, "Bậc B bật tán xạ trong profile mà lại tắt.");
            Assert.IsFalse(bBat.transmission,
                "Bậc B giữ tán xạ nhưng bỏ ánh sáng xuyên — hiệu ứng ngược sáng chỉ thấy ở vài " +
                "góc, còn giá thì trả ở mọi điểm ảnh da.");
            Assert.AreEqual(1, SkinKeywordSet.For(in bBat).EnabledCount);
        }

        [Test]
        public void NhanhTatSss_TrongShader_LaLambert_TucLitThuong()
        {
            string source = DocShader();

            Assert.AreEqual(1, DemDongCoChua(source, "SAMPLE_TEXTURE2D(_SssLut"),
                "Pre-integrated SSS phải chỉ tốn ĐÚNG MỘT lần lấy mẫu LUT. Hai lần trở lên là " +
                "đã đi chệch khỏi lý do duy nhất khiến kỹ thuật này chạy nổi trên GPU di động.");

            Assert.AreEqual(1, DemDongCoChua(source, "#ifdef _SKIN_SSS_ON"),
                "Đường LUT phải nằm sau đúng một #ifdef _SKIN_SSS_ON — tắt keyword là tắt hẳn " +
                "nhánh đó khỏi mã máy, không phải nhân với 0.");

            Assert.AreEqual(1, DemDongCoChua(source, "half3 lambert ="),
                "Nhánh tắt SSS phải là Lambert thuần — cộng với GGX ở SkinSpecular thì đúng bằng " +
                "Lit thường, nên bậc C không cần đổi vật liệu.");
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Ô 6 — thời gian biên dịch không làm màn hình đầu tiên delay quá 1 giây
        // ─────────────────────────────────────────────────────────────────────────────

        [Test]
        public void SoBienTheDungHinh_KhongVuotTran()
        {
            string source = DocShader();
            string passDungHinh = CatPass(source, "SkinForward", "SkinShadowCaster");

            int soBienThe = 1;
            var chiTiet = new List<string>();

            foreach (string dong in DongPragma(passDungHinh))
            {
                if (!dong.Contains("multi_compile")) continue;

                int soLuaChon = DemLuaChonKeyword(dong);
                Assert.GreaterOrEqual(soLuaChon, 2,
                    $"Dòng pragma '{dong}' khai ít hơn hai lựa chọn — vô nghĩa.");

                soBienThe *= soLuaChon;
                chiTiet.Add($"{soLuaChon}× {dong}");
            }

            Assert.Greater(chiTiet.Count, 0, "Không đọc được dòng multi_compile nào trong pass dựng hình.");

            Assert.LessOrEqual(soBienThe, SkinBudget.MaxForwardVariants,
                $"Pass dựng hình sinh {soBienThe} biến thể, vượt trần {SkinBudget.MaxForwardVariants}. " +
                "Mỗi biến thể là một lần biên dịch, và lần đầu tiên CHẶN khung hình — đây chính là " +
                "ô 'màn hình đầu tiên không delay quá 1 giây'.\nChi tiết:\n  " +
                string.Join("\n  ", chiTiet));

            Assert.AreEqual(192, soBienThe,
                "Số biến thể đổi so với lúc thiết kế (192 = 2×3×2×2×2×2×2). Không phải lỗi, nhưng " +
                "thêm một multi_compile là NHÂN ĐÔI thời gian biên dịch màn hình đầu tiên — sửa " +
                "con số này chỉ sau khi đã đo lại trên máy thật.\nChi tiết:\n  " +
                string.Join("\n  ", chiTiet));
        }

        [Test]
        public void ShaderDa_KhongKeoTheoNhungMultiCompileKhongDung()
        {
            string source = DocShader();
            string passDungHinh = CatPass(source, "SkinForward", "SkinShadowCaster");

            // Mỗi dòng dưới đây, nếu khai, sẽ nhân đôi (hoặc hơn) số biến thể để đổi lấy một tính
            // năng mà dự án đã quyết là không dùng. Xem docs/plan.md và T32.
            string[] khongDuoc =
            {
                "LIGHTMAP_ON",                  // nhân vật động, không có lightmap
                "_SCREEN_SPACE_OCCLUSION",      // T32 cấm SSAO ở mọi bậc
                "_ADDITIONAL_LIGHT_SHADOWS",    // đèn pha sân không đổ bóng
                "_REFLECTION_PROBE_BLENDING",   // da gần như không phản chiếu môi trường
                "LOD_FADE_CROSSFADE",
                "_LIGHT_COOKIES"
            };

            foreach (string keyword in khongDuoc)
            {
                foreach (string dong in DongPragma(passDungHinh))
                {
                    Assert.IsFalse(dong.Contains(keyword),
                        $"Pass dựng hình khai '{keyword}' — dòng '{dong}'. Mỗi multi_compile thừa " +
                        "là gấp đôi thời gian biên dịch màn hình đầu tiên để đổi lấy một tính năng " +
                        "dự án không dùng.");
                }
            }
        }

        [Test]
        public void SoDoBienDich_PhaiLaLanChayDauTien_CacheConTrong()
        {
            var trongEditor = new SkinCompileMeasurement
            {
                firstScreenCompileMs = 40.0f,
                compiledVariantCount = 3,
                usedWarmup = false,
                coldStart = false,          // shader đã nằm sẵn trong cache
                deviceName = "MacBook Pro M1 (Editor)"
            };

            Assert.IsFalse(trongEditor.IsRecorded,
                "Đo trong Editor hoặc ở lần chạy thứ hai thì shader đã nằm trong cache — con số " +
                "đó không nói gì về màn hình đầu tiên của người chơi.");
            Assert.AreEqual(SkinVerdict.ChuaDoDu, trongEditor.Evaluate());

            var buildThat = trongEditor;
            buildThat.coldStart = true;
            buildThat.deviceName = "Galaxy A54 (Exynos 1380), build Android ARM64";
            buildThat.firstScreenCompileMs = 320.0f;

            Assert.IsTrue(buildThat.IsRecorded);
            Assert.AreEqual(SkinVerdict.Dat, buildThat.Evaluate());

            var qualau = buildThat;
            qualau.firstScreenCompileMs = 1400.0f;
            Assert.AreEqual(SkinVerdict.VuotNganSach_PhaiBaoCao, qualau.Evaluate(),
                "1.4 giây vượt trần 1 giây — phải báo cáo, không tự ý cắt biến thể.");
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Vật lý của LUT — GPU sẽ đọc đúng những con số này
        // ─────────────────────────────────────────────────────────────────────────────

        [Test]
        public void Lut_SangDanTheoGocChieu_KhongBaoGioToiDi()
        {
            foreach (float radiusMm in new[] { SkinSssLut.MinRadiusMm, 15.0f, 50.0f, SkinSssLut.MaxRadiusMm })
            {
                float3 truoc = new float3(-1.0f);

                for (int x = 0; x < SkinSssLut.Width; x++)
                {
                    float3 giaTri = SkinSssLut.Integrate(SkinSssLut.NdotLForColumn(x), radiusMm);

                    for (int k = 0; k < 3; k++)
                    {
                        Assert.GreaterOrEqual(giaTri[k], truoc[k] - 1e-5f,
                            $"Ở bán kính {radiusMm}mm, kênh {k} TỐI ĐI khi đèn quay về phía bề mặt: " +
                            $"cột {x} (NdotL={SkinSssLut.NdotLForColumn(x):F3}) là {giaTri[k]:F5}, " +
                            $"cột trước là {truoc[k]:F5}.");
                    }

                    truoc = giaTri;
                }
            }
        }

        [Test]
        public void Lut_MoiGiaTriNamTrong01()
        {
            for (int y = 0; y < SkinSssLut.Height; y += 3)
            {
                float radiusMm = SkinSssLut.RadiusForRow(y);

                for (int x = 0; x < SkinSssLut.Width; x += 5)
                {
                    float3 giaTri = SkinSssLut.Integrate(SkinSssLut.NdotLForColumn(x), radiusMm);

                    for (int k = 0; k < 3; k++)
                    {
                        Assert.GreaterOrEqual(giaTri[k], 0.0f, $"Giá trị ÂM tại (x={x}, y={y}).");
                        Assert.LessOrEqual(giaTri[k], 1.0f + 1e-5f,
                            $"Giá trị > 1 tại (x={x}, y={y}) — LUT lưu bằng RGB24 nên sẽ bị kẹp và " +
                            "hiệu ứng biến mất ở đúng chỗ nó đáng thấy nhất.");
                    }
                }
            }
        }

        [Test]
        public void Lut_DoLanQuaVungGiaoRanh_XaHonXanhVaLam()
        {
            // Đây là TOÀN BỘ hiệu ứng mà T31 phải tái tạo: hemoglobin ít hấp thụ ánh sáng đỏ nên
            // đỏ đi xa nhất trong hạ bì, khiến rìa bóng trên mặt người luôn ửng đỏ.
            float radiusMm = SkinSssLut.MinRadiusMm;   // cánh mũi / vành tai

            float3 quaGiaoRanh = SkinSssLut.Integrate(-0.1f, radiusMm);

            Assert.Greater(quaGiaoRanh.x, 0.01f,
                $"Ở NdotL = -0.1 (Lambert đã tắt hẳn), kênh đỏ chỉ đạt {quaGiaoRanh.x:F5} — " +
                "gần như không thấy được. Hồ sơ khuếch tán đang bị dìm.");

            Assert.Greater(quaGiaoRanh.x, quaGiaoRanh.y * 3.0f,
                $"Đỏ ({quaGiaoRanh.x:F5}) phải vòng qua vùng tối XA HƠN HẲN xanh lá ({quaGiaoRanh.y:F5}).");
            Assert.Greater(quaGiaoRanh.x, quaGiaoRanh.z * 3.0f,
                $"Đỏ ({quaGiaoRanh.x:F5}) phải vòng qua vùng tối XA HƠN HẲN lam ({quaGiaoRanh.z:F5}).");

            float3 taiGiaoRanh = SkinSssLut.Integrate(0.0f, radiusMm);
            Assert.Greater(taiGiaoRanh.x, taiGiaoRanh.y,
                "Ngay tại đường giao ranh, đỏ vẫn phải trội hơn xanh lá.");
            Assert.Greater(taiGiaoRanh.y, taiGiaoRanh.z,
                "Xanh lá phải đi xa hơn lam — thứ tự bước sóng.");
        }

        [Test]
        public void Lut_DoCongCangGat_AnhSangVongCangXa()
        {
            // Cánh mũi (6mm) phải ửng hơn hẳn gò má (50mm) và trán (200mm) ở cùng góc chiếu.
            float ndotl = -0.1f;

            float mui = SkinSssLut.Integrate(ndotl, 6.0f).x;
            float ma = SkinSssLut.Integrate(ndotl, 50.0f).x;
            float tran = SkinSssLut.Integrate(ndotl, SkinSssLut.MaxRadiusMm).x;

            Assert.Greater(mui, ma,
                $"Chỗ cong gắt (6mm, {mui:F5}) phải vòng ánh sáng nhiều hơn chỗ phẳng (50mm, {ma:F5}).");
            Assert.Greater(ma, tran - 1e-6f,
                $"Gò má (50mm, {ma:F5}) phải vòng ánh sáng ít nhất bằng trán (200mm, {tran:F5}).");
        }

        [Test]
        public void Lut_BeMatPhang_TraVeGanDungLambert()
        {
            // Bảo toàn năng lượng: chỗ phẳng thì không có gì để vòng qua, kết quả phải trùng
            // Lambert. Lệch ở đây nghĩa là bật SSS làm ĐỔI MÀU cả những chỗ đáng lẽ không đổi.
            float radiusMm = SkinSssLut.MaxRadiusMm;
            float lechLonNhat = 0.0f;
            float taiNdotL = 0.0f;

            for (int x = 0; x < SkinSssLut.Width; x++)
            {
                float ndotl = SkinSssLut.NdotLForColumn(x);
                float lambert = math.saturate(ndotl);
                float3 giaTri = SkinSssLut.Integrate(ndotl, radiusMm);

                float lech = math.cmax(math.abs(giaTri - lambert));
                if (lech > lechLonNhat)
                {
                    lechLonNhat = lech;
                    taiNdotL = ndotl;
                }
            }

            Assert.Less(lechLonNhat, 0.01f,
                $"Bề mặt gần phẳng lệch Lambert tới {lechLonNhat:F5} tại NdotL={taiNdotL:F3}. " +
                "Chỗ phẳng phải gần như y hệt Lit thường.");
        }

        [Test]
        public void Lut_TatDinh_ChayLaiRaTungBitGiongNhau()
        {
            for (int y = 0; y < SkinSssLut.Height; y += 7)
            {
                float radiusMm = SkinSssLut.RadiusForRow(y);

                for (int x = 0; x < SkinSssLut.Width; x += 11)
                {
                    float ndotl = SkinSssLut.NdotLForColumn(x);

                    float3 lan1 = SkinSssLut.Integrate(ndotl, radiusMm);
                    float3 lan2 = SkinSssLut.Integrate(ndotl, radiusMm);

                    Assert.IsTrue(lan1.Equals(lan2),
                        $"Tích phân không tất định tại (x={x}, y={y}): {lan1} rồi {lan2}. " +
                        "LUT được nướng một lần và nằm trong build — nó PHẢI ra cùng một kết quả.");
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Bố cục LUT và ánh xạ toạ độ — phải khớp từng dòng với shader
        // ─────────────────────────────────────────────────────────────────────────────

        [Test]
        public void ToaDoLayMau_KhopVoiHangSoTrongShader()
        {
            string source = DocShader();

            float minTrongShader = DocDefineSo(source, "SKIN_MIN_CURVATURE");
            float maxTrongShader = DocDefineSo(source, "SKIN_MAX_CURVATURE");

            Assert.AreEqual(SkinSssLut.MinCurvature, minTrongShader, 1e-4f,
                "SKIN_MIN_CURVATURE trong shader lệch SkinSssLut.MinCurvature. Lệch một phép ánh xạ " +
                "là toàn bộ sắc da lệch theo mà KHÔNG có lỗi nào báo ra.");
            Assert.AreEqual(SkinSssLut.MaxCurvature, maxTrongShader, 1e-4f,
                "SKIN_MAX_CURVATURE trong shader lệch SkinSssLut.MaxCurvature.");

            Assert.AreEqual(1, DemDongCoChua(source, "ndotl * 0.5 + 0.5"),
                "Ánh xạ trục U trong shader phải giống hệt SkinSssLut.Uv: u = ndotl*0.5 + 0.5.");

            // Và kiểm chính hàm Uv ở hai đầu dải.
            float2 traiDuoi = SkinSssLut.Uv(-1.0f, SkinSssLut.MinCurvature);
            float2 phaiTren = SkinSssLut.Uv(1.0f, SkinSssLut.MaxCurvature);

            Assert.AreEqual(0.0f, traiDuoi.x, 1e-6f);
            Assert.AreEqual(0.0f, traiDuoi.y, 1e-6f);
            Assert.AreEqual(1.0f, phaiTren.x, 1e-6f);
            Assert.AreEqual(1.0f, phaiTren.y, 1e-6f);

            // Ngoài dải phải kẹp, không được cuộn vòng: cuộn vòng nghĩa là chỗ phẳng nhất đột
            // ngột được tô như chỗ cong gắt nhất.
            Assert.AreEqual(0.0f, SkinSssLut.Uv(-5.0f, 0.0f).x, 1e-6f);
            Assert.AreEqual(1.0f, SkinSssLut.Uv(5.0f, 10.0f).y, 1e-6f);
        }

        [Test]
        public void HangCuaLut_ChiaDeuTheoDoCong_KhongPhaiTheoBanKinh()
        {
            // Chia đều theo bán kính sẽ dồn gần hết 32 hàng vào vùng phẳng (100–200mm) — đúng vùng
            // KHÔNG có gì xảy ra — và để lại vài hàng cho vùng cong gắt, nơi toàn bộ hiệu ứng nằm.
            float buoc = SkinSssLut.CurvatureForRow(1) - SkinSssLut.CurvatureForRow(0);

            for (int y = 1; y < SkinSssLut.Height; y++)
            {
                float buocNay = SkinSssLut.CurvatureForRow(y) - SkinSssLut.CurvatureForRow(y - 1);
                Assert.AreEqual(buoc, buocNay, 1e-6f,
                    $"Bước độ cong ở hàng {y} là {buocNay:F6}, khác bước đầu {buoc:F6}.");
            }

            Assert.AreEqual(SkinSssLut.MinCurvature, SkinSssLut.CurvatureForRow(0), 1e-6f);
            Assert.AreEqual(SkinSssLut.MaxCurvature, SkinSssLut.CurvatureForRow(SkinSssLut.Height - 1), 1e-6f);

            Assert.AreEqual(SkinSssLut.MaxRadiusMm, SkinSssLut.RadiusForRow(0), 1e-2f,
                "Hàng đầu là chỗ phẳng nhất — trán.");
            Assert.AreEqual(SkinSssLut.MinRadiusMm, SkinSssLut.RadiusForRow(SkinSssLut.Height - 1), 1e-3f,
                "Hàng cuối là chỗ cong gắt nhất — cánh mũi, vành tai.");
        }

        [Test]
        public void Bake_LapDayToanBoMang_VaTuChoiMangNgan()
        {
            var lut = new float3[SkinSssLut.Width * SkinSssLut.Height];
            for (int i = 0; i < lut.Length; i++) lut[i] = new float3(-1.0f);

            SkinSssLut.Bake(lut);

            for (int i = 0; i < lut.Length; i++)
            {
                Assert.GreaterOrEqual(lut[i].x, 0.0f, $"Ô {i} không được nướng.");
            }

            // Một ô lấy ngẫu nhiên phải khớp đúng hàm tích phân — chứng minh Bake không đảo trục.
            int y = 21, x = 97;
            float3 mong = SkinSssLut.Integrate(SkinSssLut.NdotLForColumn(x), SkinSssLut.RadiusForRow(y));
            Assert.IsTrue(lut[y * SkinSssLut.Width + x].Equals(mong),
                "Bake xếp sai thứ tự hàng/cột — texture sẽ bị xoay 90 độ.");

            Assert.Throws<ArgumentException>(() => SkinSssLut.Bake(new float3[10]),
                "Mảng ngắn phải bị từ chối, không được nướng một phần rồi im lặng.");
            Assert.Throws<ArgumentException>(() => SkinSssLut.Bake(null));
        }

        [Test]
        public void KichThuocTexture_Dung12KB()
        {
            Assert.AreEqual(128, SkinSssLut.Width);
            Assert.AreEqual(32, SkinSssLut.Height);
            Assert.AreEqual(128 * 32 * 3, SkinSssLut.TextureBytes);
            Assert.AreEqual(12288, SkinSssLut.TextureBytes,
                "12 KB — nhỏ hơn ngân sách texture của bậc C (140 MB) hàng nghìn lần, nên LUT này " +
                "không bao giờ là thứ phải cắt.");
        }

        [Test]
        public void MaHoaRgb24_TronVenHaiDauDai()
        {
            var bytes = new byte[3];

            SkinSssLut.EncodeRgb24(new float3(0.0f, 0.5f, 1.0f), bytes, 0);
            Assert.AreEqual(0, bytes[0]);
            Assert.AreEqual(128, bytes[1], "0.5 phải làm tròn thành 128 (0.5 × 255 = 127.5).");
            Assert.AreEqual(255, bytes[2]);

            // Giá trị ngoài dải phải kẹp chứ không cuộn vòng: cuộn vòng biến chỗ sáng nhất thành đen.
            SkinSssLut.EncodeRgb24(new float3(-0.3f, 1.4f, 0.999f), bytes, 0);
            Assert.AreEqual(0, bytes[0]);
            Assert.AreEqual(255, bytes[1]);
            Assert.AreEqual(255, bytes[2]);
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Đổi bậc lúc chạy
        // ─────────────────────────────────────────────────────────────────────────────

        [Test]
        public void DoiBac_KhongCapPhatGC()
        {
            // Đổi bậc xảy ra giữa trận (T32 hạ bậc khi máy nóng). Cấp phát ở đây là một lần GC
            // spike đúng vào lúc máy đã đuối.
            // Gọi trước một lượt để JIT xong mọi hàm sẽ chạy trong vòng đo — JIT lần đầu tự nó
            // cấp phát, và đó không phải thứ test này muốn bắt.
            SkinSssSettings warm = SkinSssSettings.ForTier(QualityTier.A);
            SkinKeywordSet warmKeywords = SkinKeywordSet.For(in warm);
            var warmMeasurement = new SkinGpuMeasurement
            {
                gpuMs = 0.4f, characterCount = 2, deviceName = TenMayGiaDinh, tier = warm.tier
            };
            SkinVerdict warmVerdict = SkinBudgetCheck.Evaluate(in warmMeasurement);

            if (warmKeywords.EnabledCount < 0 || warmVerdict == (SkinVerdict)(-1))
            {
                throw new InvalidOperationException();
            }

            Assert.That(() =>
            {
                for (int i = 0; i < 1000; i++)
                {
                    SkinSssSettings s = SkinSssSettings.ForTier((QualityTier)(i % 3));
                    SkinKeywordSet k = SkinKeywordSet.For(in s);
                    var m = new SkinGpuMeasurement
                    {
                        gpuMs = 0.4f, characterCount = 2, deviceName = TenMayGiaDinh, tier = s.tier
                    };
                    if (k.EnabledCount < 0 || SkinBudgetCheck.Evaluate(in m) == (SkinVerdict)(-1))
                    {
                        throw new InvalidOperationException();
                    }
                }
            }, Is.Not.AllocatingGCMemory(),
                "Đổi bậc hoặc đọc kết luận ngân sách đang cấp phát — cấm theo docs/backlog/README.md.");
        }

        [Test]
        public void CuongDoTanXa_LuonNamTrong01()
        {
            foreach (QualityTier tier in new[] { QualityTier.A, QualityTier.B, QualityTier.C })
            {
                SkinSssSettings s = SkinSssSettings.ForTier(tier);

                Assert.GreaterOrEqual(s.sssStrength, 0.0f, $"Bậc {tier}: cường độ âm.");
                Assert.LessOrEqual(s.sssStrength, 1.0f,
                    $"Bậc {tier}: cường độ {s.sssStrength} > 1 — lerp sẽ vượt quá kết quả LUT và " +
                    "da sẽ ửng đỏ quá mức ở rìa bóng.");

                Assert.AreEqual(!s.enabled, s.useLitFallback,
                    $"Bậc {tier}: enabled và useLitFallback phải luôn ngược nhau.");

                if (!s.enabled)
                {
                    Assert.IsFalse(s.transmission, $"Bậc {tier}: tắt tán xạ mà còn bật ánh sáng xuyên.");
                    Assert.AreEqual(0.0f, s.sssStrength, 1e-6f);
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Trợ giúp đọc file .shader
        // ─────────────────────────────────────────────────────────────────────────────

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

        /// <summary>Mọi dòng <c>#pragma</c>, đã bỏ chú thích đuôi dòng.</summary>
        private static IEnumerable<string> DongPragma(string source)
        {
            foreach (string raw in source.Split('\n'))
            {
                string line = raw.Trim();
                if (!line.StartsWith("#pragma", StringComparison.Ordinal)) continue;

                int comment = line.IndexOf("//", StringComparison.Ordinal);
                if (comment >= 0) line = line.Substring(0, comment).Trim();

                yield return line;
            }
        }

        /// <summary>Số lựa chọn keyword của một dòng pragma, tức là số biến thể nó nhân lên.</summary>
        private static int DemLuaChonKeyword(string pragmaLine)
        {
            string[] tokens = pragmaLine.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

            // tokens[0] = "#pragma", tokens[1] = "multi_compile..." — phần còn lại là các lựa chọn.
            return math.max(0, tokens.Length - 2);
        }

        /// <summary>Cắt phần văn bản của một pass, từ tên pass này tới tên pass kế tiếp.</summary>
        private static string CatPass(string source, string tenPass, string tenPassKeTiep)
        {
            int batDau = source.IndexOf("Name \"" + tenPass + "\"", StringComparison.Ordinal);
            Assert.GreaterOrEqual(batDau, 0, $"Không tìm thấy pass '{tenPass}' trong Skin.shader.");

            int ketThuc = source.IndexOf("Name \"" + tenPassKeTiep + "\"", StringComparison.Ordinal);
            Assert.Greater(ketThuc, batDau,
                $"Không tìm thấy pass '{tenPassKeTiep}' sau '{tenPass}' — bố cục file đã đổi, " +
                "test đếm biến thể sẽ đọc nhầm phạm vi.");

            return source.Substring(batDau, ketThuc - batDau);
        }

        /// <summary>Đọc giá trị số của một <c>#define</c> trong shader.</summary>
        private static float DocDefineSo(string source, string ten)
        {
            foreach (string raw in source.Split('\n'))
            {
                string line = raw.Trim();
                if (!line.StartsWith("#define " + ten, StringComparison.Ordinal)) continue;

                int comment = line.IndexOf("//", StringComparison.Ordinal);
                if (comment >= 0) line = line.Substring(0, comment).Trim();

                string[] tokens = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                Assert.AreEqual(3, tokens.Length, $"Dòng '#define {ten}' không có đúng một giá trị.");

                return float.Parse(tokens[2], CultureInfo.InvariantCulture);
            }

            Assert.Fail($"Không tìm thấy '#define {ten}' trong Skin.shader.");
            return 0.0f;
        }
    }
}
