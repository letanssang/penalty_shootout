using System.Collections;
using Eleven.Ball;
using Eleven.Core;
using Eleven.Core.Diagnostics;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;

namespace Eleven.Tests.PlayMode
{
    /// <summary>
    /// Các mục nghiệm thu Phase 0 mà EditMode KHÔNG kiểm được, vì chúng nói về hành vi của
    /// build IL2CPP trên phần cứng thật: hash tất định qua IL2CPP, HUD vẽ lên màn hình,
    /// nhiệt độ máy, chi phí của chính HUD, và CSV ghi ra persistentDataPath.
    ///
    /// Chạy: Unity -runTests -testPlatform Android (Unity tự dựng APK test và đẩy sang máy).
    /// Mọi con số đo được đều Debug.Log để đọc lại bằng `adb logcat`.
    /// </summary>
    public class DeviceAcceptanceTests
    {
        /// <summary>
        /// Hash quỹ đạo do BallSolverTests.GoldenHash_... in ra trong Editor (Mono, macOS ARM64).
        /// Build IL2CPP phải cho ĐÚNG con số này, không phải "xấp xỉ".
        /// </summary>
        const uint EditorGoldenHash = 4094678572u;

        static uint HashTrajectory(BallState initial, BallParams p, int steps, float dt)
        {
            uint h = 0;
            var s = initial;
            for (int i = 0; i < steps; i++)
            {
                s = BallSolver.Step(s, p, dt);
                h ^= math.asuint(s.position.x) + (uint)i * 2654435761u;
                h ^= math.asuint(s.position.y) + (uint)i * 2246822519u;
                h ^= math.asuint(s.position.z) + (uint)i * 3266489917u;
                h ^= math.asuint(s.velocity.x);
                h ^= math.asuint(s.velocity.y);
                h ^= math.asuint(s.velocity.z);
            }
            return h;
        }

        // ─── T07: solver tất định qua IL2CPP ───

        [Test]
        public void T07_GoldenHash_TrenIL2CPP_KhopTungBitVoiEditor()
        {
            var s = new BallState
            {
                position = float3.zero,
                velocity = new float3(3f, 8f, 28f),
                spin = new float3(10f, -20f, 5f)
            };
            uint hash = HashTrajectory(s, BallParams.Default, 200, 1f / 200f);

            Debug.Log($"[T07 THIET BI] hash={hash} editor={EditorGoldenHash} " +
                      $"backend=IL2CPP model={SystemInfo.deviceModel}");

            Assert.AreEqual(EditorGoldenHash, hash,
                "Hash trên build IL2CPP phải khớp từng bit với Editor — khác là solver không tất định giữa hai backend");
        }

        // ─── T03: phân bậc theo năng lực thật của máy ───

        [Test]
        public void T03_DeviceTier_PhatHienTrenPhanCungThat()
        {
            var tier = DeviceTier.Detect();
            Debug.Log($"[T03 THIET BI] tier={tier} model={SystemInfo.deviceModel} " +
                      $"RAM={SystemInfo.systemMemorySize}MB VRAM={SystemInfo.graphicsMemorySize}MB " +
                      $"cores={SystemInfo.processorCount} gfx={SystemInfo.graphicsDeviceType}");

            Assert.That(tier, Is.EqualTo(QualityTier.A).Or.EqualTo(QualityTier.B).Or.EqualTo(QualityTier.C));
            Assert.NotNull(DeviceTier.CurrentProfile,
                "TierBootstrap trong scene Boot phải đã gọi Initialize — CurrentProfile không được null trên máy thật");
            Assert.AreEqual(tier, DeviceTier.CurrentProfile.tier,
                "Profile đang dùng phải đúng là profile của bậc phát hiện được");
        }

        // ─── T03: đổi bậc lúc chạy — không crash, không rò render texture ───

        /// <summary>
        /// Ô nghiệm thu T03 "đổi bậc lúc chạy không crash, không rò render texture".
        ///
        /// Không cần Profiler có giao diện: đổi bậc là URP đổi pipeline asset, và mọi
        /// render texture của bậc cũ phải được thu hồi. Nếu rò, số RenderTexture còn
        /// sống sẽ TĂNG DẦN sau mỗi vòng — nên ta quay 12 vòng qua A→B→C rồi so số đếm
        /// ở cùng một bậc giữa vòng đầu và vòng cuối. Cùng bậc thì số phải xấp xỉ bằng
        /// nhau; chỉ khi rò nó mới leo lên.
        /// </summary>
        [UnityTest]
        public IEnumerator T03_DoiBacLucChay_KhongCrash_KhongRoRenderTexture()
        {
            bool coOverride = PlayerPrefs.HasKey(DeviceTier.OverrideKey);
            int overrideCu = PlayerPrefs.GetInt(DeviceTier.OverrideKey, -1);

            const int soVong = 12; // 4 lượt đầy đủ qua cả ba bậc
            var dem = new int[soVong];
            var bacDo = new QualityTier[soVong];
            bool lechBac = false;

            for (int v = 0; v < soVong; v++)
            {
                var bac = (QualityTier)(v % 3);
                PlayerPrefs.SetInt(DeviceTier.OverrideKey, (int)bac);
                DeviceTier.RefreshOverride();

                // Đủ khung để URP giải phóng render texture của bậc cũ và dựng bậc mới.
                for (int i = 0; i < 12; i++) yield return null;

                bacDo[v] = DeviceTier.Current;
                if (bacDo[v] != bac) lechBac = true;
                dem[v] = Resources.FindObjectsOfTypeAll<RenderTexture>().Length;
                Debug.Log($"[T03 THIET BI] vong={v} bac={bac} thuc te={bacDo[v]} renderTexture={dem[v]}");
            }

            // Trả PlayerPrefs về nguyên trạng TRƯỚC khi assert, để test hỏng cũng không
            // để lại bậc ép cho các test chạy sau.
            if (coOverride) PlayerPrefs.SetInt(DeviceTier.OverrideKey, overrideCu);
            else PlayerPrefs.DeleteKey(DeviceTier.OverrideKey);
            DeviceTier.RefreshOverride();

            int dauA = dem[0];   // bậc A, lượt 1
            int cuoiA = dem[9];  // bậc A, lượt 4
            Debug.Log($"[T03 THIET BI] doi bac {soVong} lan xong — renderTexture bac A: " +
                      $"luot dau={dauA} luot cuoi={cuoiA} chenh={cuoiA - dauA}");

            Assert.IsFalse(lechBac, "Mỗi lần RefreshOverride xong, DeviceTier.Current phải đúng bậc vừa ép");
            Assert.LessOrEqual(cuoiA - dauA, 2,
                $"Rò render texture: cùng bậc A mà sau 4 lượt đổi bậc số RT tăng từ {dauA} lên {cuoiA}");
        }

        // ─── T04: HUD vẽ được lên màn hình thiết bị ───

        [UnityTest]
        public IEnumerator T04_Hud_HienLenManHinhThietBi()
        {
            PerfHud.Visible = true;
            Assert.IsTrue(PerfHud.Visible, "Bật Visible xong PerfHud phải báo là đang hiện");

            // Giữ HUD trên màn hình đủ lâu để chụp lại bằng `adb shell screencap` từ ngoài.
            // 8 giây cũng đủ để chữ được làm mới nhiều lần (chu kỳ 4 lần/giây).
            Debug.Log("[T04 THIET BI] HUD dang hien — chup man hinh trong 8 giay toi");
            float until = Time.unscaledTime + 8f;
            while (Time.unscaledTime < until) yield return null;

            Assert.IsTrue(PerfHud.Visible, "HUD phải còn hiện sau 8 giây");
            Debug.Log("[T04 THIET BI] het 8 giay, HUD van dang hien");
        }

        // ─── T04: nhiệt độ máy ───

        [UnityTest]
        public IEnumerator T04_DocDuocNhietDoMay()
        {
            for (int i = 0; i < 30; i++) yield return null;

            var cur = PerfHud.Current;
            Debug.Log($"[T04 THIET BI] thermalState={cur.thermalState} battery={cur.batteryLevel}");

            Assert.That(cur.thermalState, Is.InRange(0, 3),
                "thermalState phải nằm trong 0..3 theo hợp đồng T04");
        }

        // ─── T04: chi phí của chính HUD ───

        [UnityTest]
        public IEnumerator T04_HudTonDuoi0_2ms()
        {
            const int warm = 30, sample = 180;

            PerfHud.Visible = false;
            for (int i = 0; i < warm; i++) yield return null;
            float sumOff = 0f;
            for (int i = 0; i < sample; i++) { yield return null; sumOff += PerfHud.Current.totalMs; }
            float avgOff = sumOff / sample;

            PerfHud.Visible = true;
            for (int i = 0; i < warm; i++) yield return null;
            float sumOn = 0f;
            for (int i = 0; i < sample; i++) { yield return null; sumOn += PerfHud.Current.totalMs; }
            float avgOn = sumOn / sample;

            float delta = avgOn - avgOff;
            Debug.Log($"[T04 THIET BI] frame tat HUD={avgOff:F4}ms bat HUD={avgOn:F4}ms chenh={delta:F4}ms");

            Assert.Less(delta, 0.2f, "Bản thân HUD phải tốn dưới 0.2ms mỗi khung");
        }

        // ─── T04: cấp phát GC mỗi khung khi HUD đang bật ───

        [UnityTest]
        public IEnumerator T04_DoCapPhatGcMoiKhungKhiHudBat()
        {
            // Đo thật trên Pixel 7 cho thấy framesWithAlloc == 240/240 CẢ KHI HUD TẮT (nền
            // ~500B/khung, không đổi theo HUD) — UnityTestRunner/NUnit tự cấp phát mỗi khung
            // khi chạy vòng lặp [UnityTest] (yield return null), nên "0 khung cấp phát" không
            // bao giờ đo được thật trong chính bài test này, bất kể HUD làm gì. Cách đo đúng:
            // trừ nền (HUD tắt) để lấy phần HUD tự thêm vào, rồi chặn phần thêm đó ở mức nhỏ.
            const int sample = 240;

            PerfHud.Visible = false;
            for (int i = 0; i < 30; i++) yield return null;
            long baseTotal = 0;
            for (int i = 0; i < sample; i++)
            {
                yield return null;
                baseTotal += PerfHud.Current.gcAllocBytes;
            }
            Debug.Log($"[T04 THIET BI] GC nen (HUD tat) qua {sample} khung: tong={baseTotal}B");

            PerfHud.Visible = true;
            for (int i = 0; i < 30; i++) yield return null;
            long total = 0; int framesWithAlloc = 0; long worst = 0;
            for (int i = 0; i < sample; i++)
            {
                yield return null;
                long a = PerfHud.Current.gcAllocBytes;
                if (a > 0) { framesWithAlloc++; total += a; if (a > worst) worst = a; }
            }

            float marginPerFrame = (total - baseTotal) / (float)sample;
            Debug.Log($"[T04 THIET BI] GC qua {sample} khung: tong={total}B " +
                      $"so khung co cap phat={framesWithAlloc} lon nhat={worst}B " +
                      $"phan HUD them vao/khung={marginPerFrame:F1}B");

            // Chặn hồi quy: nếu HUD tự thêm hơn 500B/khung là quay lại kiểu OnGUI (từng đo
            // ~3135B/khung, đỉnh 19592B) chứ không phải UGUI chỉ làm mới chữ 4 lần/giây.
            Assert.Less(marginPerFrame, 500f,
                "HUD không được tự thêm cấp phát đáng kể mỗi khung so với nền lúc tắt HUD");
        }

        // ─── T04: CSV ghi ra persistentDataPath ───

        [UnityTest]
        public IEnumerator T04_EndCapture_GhiCsvRaPersistentDataPath()
        {
            PerfHud.BeginCapture("device-acceptance");
            for (int i = 0; i < 120; i++) yield return null;
            string csv = PerfHud.EndCapture();

            Assert.IsNotNull(csv, "EndCapture phải trả CSV");
            StringAssert.StartsWith("frame,total_ms,gpu_ms", csv);

            var files = System.IO.Directory.GetFiles(Application.persistentDataPath, "eleven_device-acceptance_*.csv");
            Debug.Log($"[T04 THIET BI] persistentDataPath={Application.persistentDataPath} " +
                      $"so file CSV={files.Length} do dai CSV={csv.Length} ky tu");
            foreach (var f in files)
                Debug.Log($"[T04 THIET BI] CSV: {f} ({new System.IO.FileInfo(f).Length} bytes)");

            Assert.IsNotEmpty(files, "CSV phải nằm trên đĩa trong persistentDataPath để adb pull về được");
        }
    }
}
