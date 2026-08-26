using System.Collections;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;
using Eleven.Ball;

namespace Eleven.Tests.PlayMode
{
    // GHI CHÚ: "Chạy ở 30fps và 60fps cho ra cùng quỹ đạo, sai số dưới 1e-3" và hành vi
    // trần bước khi máy khựng thật KHÔNG kiểm được đáng tin trong test tự động ở đây —
    // cần đo trên thiết bị thật với khung hình biến thiên thật. CẦN NGƯỜI KIỂM.

    public class BallDriverTests
    {
        GameObject go;
        BallDriver driver;

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
            Object.Destroy(go);
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
