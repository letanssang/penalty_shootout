using Eleven.Shooter;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace Eleven.Tests.EditMode {
    /// <summary>
    /// Phần toán cắt tia–mặt phẳng được test kỹ vì đó là chỗ chứa toàn bộ logic.
    /// Phần đọc Camera chỉ test được ở mức khứ hồi, và test đó tự tuyên bố bỏ qua nếu
    /// môi trường batchmode không cấp nổi một viewport có kích thước thật — thà bỏ qua
    /// công khai còn hơn xanh giả.
    /// </summary>
    [TestFixture]
    public class AimProjectorTests {
        const float GoalPlaneZ = 11f;   // khớp GoalGeometry.PenaltyDistance

        [Test]
        public void TiaThang_CatMatPhangDungChoMongDoi() {
            // Từ chấm phạt đền, ngắm lên góc cao bên phải.
            bool ok = AimProjector.TryRayToPlaneZ(
                origin: new float3(0f, 0f, 0f),
                direction: new float3(3f, 2f, 11f),
                planeZ: GoalPlaneZ,
                out float3 hit);

            Assert.IsTrue(ok);
            Assert.AreEqual(3f, hit.x, 1e-4f);
            Assert.AreEqual(2f, hit.y, 1e-4f);
            Assert.AreEqual(GoalPlaneZ, hit.z, 1e-4f);
        }

        [Test]
        public void HuongKhongCanChuanHoa() {
            AimProjector.TryRayToPlaneZ(float3.zero, new float3(3f, 2f, 11f), GoalPlaneZ, out float3 a);
            AimProjector.TryRayToPlaneZ(float3.zero, math.normalize(new float3(3f, 2f, 11f)),
                                        GoalPlaneZ, out float3 b);
            Assert.AreEqual(0f, math.distance(a, b), 1e-3f);
        }

        [Test]
        public void CameraLuiVeSau_VanCatDungMatPhang() {
            // Camera thật sẽ nằm phía sau chấm phạt đền, không phải ở gốc toạ độ.
            bool ok = AimProjector.TryRayToPlaneZ(new float3(0f, 2.9f, -6f),
                                                  new float3(0f, -0.9f, 17f), GoalPlaneZ, out float3 hit);
            Assert.IsTrue(ok);
            Assert.AreEqual(GoalPlaneZ, hit.z, 1e-4f);
            Assert.AreEqual(2f, hit.y, 1e-3f);   // 2.9 - 0.9*(17/17)
        }

        [Test]
        public void TiaSongSongMatPhang_TraVeFalse() {
            Assert.IsFalse(AimProjector.TryRayToPlaneZ(
                new float3(0f, 1f, 0f), new float3(1f, 0f, 0f), GoalPlaneZ, out _));
        }

        [Test]
        public void MatPhangNamSauLung_TraVeFalse() {
            // Ngắm ra xa khung thành: giao điểm nằm phía sau tia. Trả về điểm "sau lưng"
            // sẽ là cái bẫy im lặng, nên phải báo hỏng.
            Assert.IsFalse(AimProjector.TryRayToPlaneZ(
                float3.zero, new float3(0f, 0f, -1f), GoalPlaneZ, out _));
        }

        [Test]
        public void DungTrenMatPhang_VanCatDuocTaiCho() {
            bool ok = AimProjector.TryRayToPlaneZ(new float3(1f, 2f, GoalPlaneZ),
                                                  new float3(0f, 0f, 1f), GoalPlaneZ, out float3 hit);
            Assert.IsTrue(ok);
            Assert.AreEqual(GoalPlaneZ, hit.z, 1e-4f);
        }

        [Test]
        public void CameraNull_TraVeFalse_KhongNemLoi() {
            Assert.IsFalse(AimProjector.TryScreenToGoalPlane(new Vector2(100f, 200f), null,
                                                             GoalPlaneZ, out _));
        }

        [Test]
        public void KhuHoi_QuaCameraThat_VeDungDiemBanDau() {
            var go = new GameObject("test-cam");
            try {
                var cam = go.AddComponent<Camera>();
                cam.transform.position = new Vector3(0f, 2.5f, -5f);
                cam.transform.rotation = Quaternion.identity;   // nhìn theo +Z
                cam.fieldOfView = 55f;
                cam.nearClipPlane = 0.1f;
                cam.farClipPlane = 100f;

                if (cam.pixelWidth <= 1 || cam.pixelHeight <= 1) {
                    Assert.Ignore("Môi trường không cấp viewport thật nên không khứ hồi được qua Camera.");
                }

                var mong = new Vector3(1.8f, 1.4f, GoalPlaneZ);
                Vector3 pixel = cam.WorldToScreenPoint(mong);

                bool ok = AimProjector.TryScreenToGoalPlane(new Vector2(pixel.x, pixel.y), cam,
                                                            GoalPlaneZ, out float3 hit);
                Assert.IsTrue(ok);
                Assert.AreEqual(mong.x, hit.x, 1e-2f);
                Assert.AreEqual(mong.y, hit.y, 1e-2f);
                Assert.AreEqual(GoalPlaneZ, hit.z, 1e-3f);
            } finally {
                Object.DestroyImmediate(go);
            }
        }
    }
}
