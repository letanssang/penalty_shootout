using System;
using System.Collections;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;
using Eleven.Ball;

namespace Eleven.Tests.PlayMode
{
    /// <summary>
    /// GHI CHÚ CŨ ĐÃ SAI, GIỮ LẠI ĐỂ KHÔNG AI ĐI LẠI ĐƯỜNG ĐÓ: bản đầu của file này viết rằng
    /// "chạy ở 30fps và 60fps cho ra cùng quỹ đạo" không kiểm được đáng tin trong test tự động.
    /// Kiểm được — bằng <see cref="Time.captureDeltaTime"/>, thứ ép <c>Time.deltaTime</c> về
    /// đúng một giá trị bất kể máy chạy test nhanh chậm ra sao. Nhịp khung trở thành ĐẦU VÀO
    /// của test chứ không còn là nhiễu của môi trường.
    ///
    /// Ba thứ được canh ở đây, và chỉ thứ ba là còn cần người:
    ///   1. Nhịp cố định khác nhau (30 vs 60 fps) → cùng quỹ đạo.
    ///   2. Nhịp BIẾN THIÊN theo lịch định sẵn → vẫn cùng quỹ đạo. Đây mới là điều kiện thật:
    ///      máy thật không bao giờ cho khung hình đều tăm tắp.
    ///   3. Còn lại cho người: khung hình biến thiên *ngẫu nhiên* trên thiết bị thật, kèm GC
    ///      spike và nhiệt độ máy. Test không dựng lại được cái đó, nhưng nó là biến thể của
    ///      (2) chứ không phải một hiện tượng khác.
    ///
    /// Mọi test so quỹ đạo ở đây đều đo THỜI GIAN GAME TRÔI QUA THẬT bằng Time.time chứ không
    /// tin rằng "N khung × delta = N·delta". Nếu captureDeltaTime có độ trễ một khung thì phép
    /// đo đó bắt được ngay, thay vì âm thầm so hai quỹ đạo ở hai mốc thời gian khác nhau.
    /// </summary>
    public class BallDriverTests
    {
        GameObject go;
        BallDriver driver;

        /// <summary>Cú sút dùng chung cho mọi phép so quỹ đạo — có cả xoáy để Magnus tham gia.</summary>
        static BallState CuSutChuan() => new BallState
        {
            position = float3.zero,
            velocity = new float3(1.5f, 4f, 24f),
            spin = new float3(0f, 6f, 0f)
        };

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("BallDriverTest");
            driver = go.AddComponent<BallDriver>();
        }

        [TearDown]
        public void TearDown()
        {
            Time.captureDeltaTime = 0f; // trả lại nhịp khung thật, không ép nữa
            UnityEngine.Object.Destroy(go);   // ghi đủ tên: file có 'using System' nên 'Object' nhập nhằng
        }

        [UnityTest]
        public IEnumerator OnSimStep_BanDung120Lan_Trong1GiayThoiGianGame()
        {
            // Ép mỗi khung đúng 1/60s — không phụ thuộc tốc độ máy chạy test thật.
            Time.captureDeltaTime = 1f / 60f;
            int stepCount = 0;
            driver.OnSimStep += _ => stepCount++;

            driver.Launch(new BallState
            {
                position = float3.zero,
                velocity = new float3(0f, 5f, 20f),
                spin = float3.zero
            });

            for (int i = 0; i < 60; i++)
                yield return null;

            Assert.AreEqual(120, stepCount,
                "60 khung ở 1/60s (= 1 giây thời gian game) phải bắn đúng 120 bước sim 1/120s");
        }

        [UnityTest]
        public IEnumerator Freeze_DungBanOnSimStep_NgayLapTuc()
        {
            Time.captureDeltaTime = 1f / 60f;
            int stepCount = 0;
            driver.OnSimStep += _ => stepCount++;

            driver.Launch(new BallState
            {
                position = float3.zero,
                velocity = new float3(0f, 5f, 20f),
                spin = float3.zero
            });

            yield return null;
            yield return null;
            driver.Freeze();
            int countAtFreeze = stepCount;

            for (int i = 0; i < 10; i++)
                yield return null;

            Assert.AreEqual(countAtFreeze, stepCount, "Sau Freeze, OnSimStep không được bắn thêm");
            Assert.IsFalse(driver.IsLive, "Freeze phải đặt IsLive = false");
        }

        [UnityTest]
        public IEnumerator ResetTo_DatLaiViTriVaTrangThai()
        {
            driver.Launch(new BallState
            {
                position = float3.zero,
                velocity = new float3(0f, 5f, 20f),
                spin = float3.zero
            });
            yield return null;

            var target = new float3(1f, 2f, 3f);
            driver.ResetTo(target);

            Assert.IsFalse(driver.IsLive, "ResetTo phải đặt IsLive = false");
            Assert.Less(math.distance(driver.State.position, target), 1e-5f,
                "State.position phải bằng vị trí reset");
            Assert.Less(math.distance((float3)go.transform.position, target), 1e-5f,
                "Transform hiển thị phải khớp vị trí reset ngay lập tức, không đợi nội suy");
        }

        [UnityTest]
        public IEnumerator Nhip30fpsVaNhip60fps_ChoCungQuyDao()
        {
            // 1/30 = 4 bước sim, 1/60 = 2 bước sim. Cùng 1 giây thời gian game thì cả hai phải
            // chạy đúng 120 bước và bóng phải ở cùng một chỗ. Sai ở đây nghĩa là bộ tích luỹ
            // đang nuốt hoặc lặp bước, và người chơi máy yếu sẽ thấy quỹ đạo khác người chơi
            // máy khoẻ — cùng một cú sút, cùng một seed.
            yield return DoMotLuot(1f / 60f, 60);
            float3 viTri60 = ketQuaViTri;
            int buoc60 = ketQuaSoBuoc;
            float giay60 = ketQuaThoiGian;

            yield return DoMotLuot(1f / 30f, 30);
            float3 viTri30 = ketQuaViTri;
            int buoc30 = ketQuaSoBuoc;
            float giay30 = ketQuaThoiGian;

            // Canh trước: hai lượt có thật sự đo ở cùng một mốc thời gian game không.
            Assert.AreEqual(1f, giay60, 1e-3f, $"Lượt 60fps trôi {giay60:F5} s chứ không phải 1 s.");
            Assert.AreEqual(1f, giay30, 1e-3f, $"Lượt 30fps trôi {giay30:F5} s chứ không phải 1 s.");

            Assert.AreEqual(120, buoc60, "1 giây game ở 60fps phải là đúng 120 bước sim.");
            Assert.AreEqual(120, buoc30, "1 giây game ở 30fps phải là đúng 120 bước sim.");

            float lech = math.distance(viTri60, viTri30);
            Assert.Less(lech, 1e-3f,
                $"Quỹ đạo lệch {lech:F6} m giữa 30fps và 60fps (60fps: {viTri60}, 30fps: {viTri30}). " +
                "Bộ tích luỹ phải làm hai nhịp khung cho ra cùng số bước sim ở cùng thời gian game.");
        }

        [UnityTest]
        public IEnumerator NhipKhungHinhBienThien_VanChoCungQuyDaoVoiNhipDeu()
        {
            // Đây mới là điều kiện của máy thật: khung hình không đều. Lịch dưới đây là bội số
            // của SimDt, lặp lại cho tới khi tổng đúng 120 bước — nghĩa là cùng 1 giây thời gian
            // game với hai lượt ở test trên, nhưng chia thành những khung dài ngắn khác nhau.
            //
            // Không dùng số ngẫu nhiên: lịch cố định thì lần chạy nào cũng dựng lại được đúng
            // tình huống đã hỏng, còn ngẫu nhiên thì lỗi hiện một lần rồi biến mất.
            int[] boiSo = { 1, 4, 2, 7, 3, 5, 2, 6 };   // tổng 30, lặp 4 lần = 120 bước
            var lich = new float[boiSo.Length * 4];
            for (int i = 0; i < lich.Length; i++)
                lich[i] = boiSo[i % boiSo.Length] * BallDriver.SimDt;

            yield return DoMotLuot(1f / 60f, 60);
            float3 viTriDeu = ketQuaViTri;

            yield return DoMotLuotTheoLich(lich);
            float3 viTriBienThien = ketQuaViTri;
            int buocBienThien = ketQuaSoBuoc;
            float giayBienThien = ketQuaThoiGian;

            Assert.AreEqual(1f, giayBienThien, 1e-3f,
                $"Lượt nhịp biến thiên trôi {giayBienThien:F5} s chứ không phải 1 s — " +
                "lịch khung hình không tổng đúng như tính toán, so quỹ đạo sẽ vô nghĩa.");
            Assert.AreEqual(120, buocBienThien,
                $"Nhịp biến thiên cho {buocBienThien} bước thay vì 120 — bộ tích luỹ đánh rơi " +
                "hoặc lặp bước khi khung hình dài ngắn xen kẽ.");

            float lech = math.distance(viTriDeu, viTriBienThien);
            Assert.Less(lech, 1e-3f,
                $"Khung hình dài ngắn xen kẽ làm quỹ đạo lệch {lech:F6} m so với nhịp đều " +
                $"(đều: {viTriDeu}, biến thiên: {viTriBienThien}).");
        }

        [UnityTest]
        public IEnumerator KhungHinhKhungKhiep_ChayDungTranBuocRoiXaNoDi()
        {
            // Máy khựng một khung rất dài (nạp scene, GC lớn). Không có trần bước thì khung sau
            // phải trả nợ vài chục bước, làm khựng tiếp — đúng cái vòng xoáy chết. Trần bước
            // cắt nợ, đổi lại bóng "nhảy" một chút. Đó là đánh đổi có chủ ý, và test này khoá nó
            // lại để không ai lặng lẽ bỏ trần đi.
            int soBuoc = 0;
            driver.OnSimStep += _ => soBuoc++;

            Time.captureDeltaTime = 1f / 60f;
            yield return null;
            driver.Launch(CuSutChuan());
            yield return null;               // một khung bình thường: 2 bước
            int truocKhungDai = soBuoc;

            Time.captureDeltaTime = 40f * BallDriver.SimDt;   // khựng 1/3 giây
            yield return null;
            int trongKhungDai = soBuoc - truocKhungDai;

            Assert.AreEqual(8, trongKhungDai,
                $"Một khung dài 40 bước sim đã chạy {trongKhungDai} bước — trần 8 bước/khung " +
                "không còn hiệu lực, máy sẽ rơi vào vòng xoáy chết khi khựng.");

            // Và nợ phải được XẢ, không dồn sang khung sau.
            Time.captureDeltaTime = 1f / 60f;
            yield return null;
            int khungKeTiep = soBuoc - truocKhungDai - trongKhungDai;

            Assert.AreEqual(2, khungKeTiep,
                $"Khung ngay sau cú khựng chạy {khungKeTiep} bước thay vì 2 — nợ thời gian đang " +
                "được dồn sang thay vì xả, tức là vẫn khựng dây chuyền dù có trần bước.");
        }

        // ---- Máy chạy dùng chung ------------------------------------------------------------
        // Kết quả trả qua field vì coroutine của UnityTest không trả giá trị được. Field của
        // INSTANCE, không phải static: NUnit dựng instance mới cho mỗi test nên không rò rỉ.

        float3 ketQuaViTri;
        int ketQuaSoBuoc;
        float ketQuaThoiGian;

        IEnumerator DoMotLuot(float nhipGiay, int soKhung)
        {
            var lich = new float[soKhung];
            for (int i = 0; i < soKhung; i++) lich[i] = nhipGiay;
            yield return DoMotLuotTheoLich(lich);
        }

        IEnumerator DoMotLuotTheoLich(float[] lich)
        {
            int soBuoc = 0;
            Action<BallState> dem = _ => soBuoc++;

            // Đặt nhịp rồi bỏ MỘT khung trước khi Launch: captureDeltaTime có hiệu lực từ khung
            // kế tiếp, nên khung đầu của lượt đo phải đã ở đúng nhịp rồi.
            Time.captureDeltaTime = lich[0];
            yield return null;

            driver.OnSimStep += dem;
            float bomoc = Time.time;
            driver.Launch(CuSutChuan());

            for (int i = 0; i < lich.Length; i++)
            {
                // ĐO ĐƯỢC, không phải suy đoán: đặt captureDeltaTime rồi `yield return null`
                // thì CHÍNH khung vừa trôi qua đã dùng giá trị mới, không phải khung kế tiếp.
                // Bản đầu viết lookahead `lich[i+1]` và lượt đo trôi 1.04167 s thay vì 1 s —
                // đúng bằng lich[0] bị bỏ và lich[31] bị tính hai lần. Phép đo Time.time ở dưới
                // là thứ bắt được chuyện đó; nếu chỉ tin "32 khung × delta" thì đã âm thầm so
                // hai quỹ đạo ở hai mốc thời gian lệch nhau 5 bước sim.
                Time.captureDeltaTime = lich[i];
                yield return null;
            }

            driver.Freeze();
            driver.OnSimStep -= dem;

            ketQuaViTri = driver.State.position;
            ketQuaSoBuoc = soBuoc;
            ketQuaThoiGian = Time.time - bomoc;
        }

        [UnityTest]
        public IEnumerator ChuaLaunch_KhongTuChaySim()
        {
            Time.captureDeltaTime = 1f / 60f;
            int stepCount = 0;
            driver.OnSimStep += _ => stepCount++;

            for (int i = 0; i < 10; i++)
                yield return null;

            Assert.AreEqual(0, stepCount, "Chưa gọi Launch thì không được tự bắn OnSimStep");
            Assert.IsFalse(driver.IsLive);
        }
    }
}
