using System.IO;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Eleven.Presentation;

namespace Eleven.Editor.SceneSetup
{
    /// <summary>
    /// Chụp khung hình của góc máy sau lưng người sút, ở đúng tỉ lệ màn hình máy thật, để
    /// KIỂM BẰNG MẮT thay vì tin vào phép tính trên giấy.
    ///
    /// Chạy trong EditMode nên Awake của CameraRig không chạy — hàm này tự đặt tư thế bằng
    /// PoseFor rồi kẹp đúng như Apply() làm. Nếu hai chỗ lệch nhau thì bức ảnh này nói dối,
    /// nên phần kẹp bên dưới phải bám sát CameraRig.Apply.
    ///
    /// CHỈ TIN ĐƯỢC PHẦN HÌNH HỌC — ai to bằng nào, đứng ở đâu, có bị cắt không. TUYỆT ĐỐI
    /// KHÔNG dùng để đánh giá ánh sáng hay màu sắc: đo ngày 2026-08-28, ba lần chạy liên tiếp
    /// cùng một scene cho ra ba độ sáng khác hẳn nhau (tối, xanh lam, cháy trắng), và mặt cỏ
    /// vốn màu xanh lá luôn hiện ra xanh lam. Batchmode không có dữ liệu ánh sáng đã bake nên
    /// chỉ còn ambient, và cái đó thì phụ thuộc trạng thái cache của Library. Muốn duyệt ánh
    /// sáng thì phải chụp trên máy thật.
    /// </summary>
    public static class CameraFramingProbe
    {
        const string ScenePath = "Assets/_Project/Scenes/Match.unity";

        [MenuItem("Eleven/Art/Chụp Khung Hình Góc Sau Lưng Người Sút")]
        public static void Capture()
        {
            string outPath = "Temp/camera-framing.png";
            int width = 2400, height = 1080;

            foreach (string a in System.Environment.GetCommandLineArgs())
            {
                if (a.StartsWith("-probeOut=")) outPath = a.Substring("-probeOut=".Length);
            }

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var rig = Object.FindFirstObjectByType<CameraRig>();
            var cam = rig != null ? rig.GetComponent<UnityEngine.Camera>() : null;
            if (cam == null) { Debug.LogError("[CameraFramingProbe] Không thấy CameraRig trong scene."); return; }

            CameraRig.Pose pose = rig.PoseFor(CameraShot.BehindShooter);
            float3 pos = math.clamp(pose.position, CameraAuthoredBounds.MinBounds, CameraAuthoredBounds.MaxBounds);
            cam.transform.position = (Vector3)pos;
            cam.transform.rotation = Quaternion.LookRotation((Vector3)(pose.lookAt - pos), Vector3.up);
            cam.fieldOfView = pose.fov;

            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
            var prev = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();

            RenderTexture prevActive = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = prevActive;
            cam.targetTexture = prev;

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath)));
            File.WriteAllBytes(outPath, tex.EncodeToPNG());

            MeasureFraming(cam);

            Object.DestroyImmediate(tex);
            rt.Release();
            Object.DestroyImmediate(rt);

            Debug.Log($"[CameraFramingProbe] Đã chụp {outPath} — máy tại {pos}, nhìn {pose.lookAt}, fov {pose.fov}.");
        }

        /// <summary>
        /// Đo khung hình bằng chính ma trận chiếu của camera rồi in ra toạ độ viewport
        /// (0 = mép dưới/trái, 1 = mép trên/phải). Đây mới là thứ đáng tin: nó không đụng
        /// tới ánh sáng, mà ảnh PNG ở trên thì lúc sáng lúc tối tuỳ cache Library.
        ///
        /// Chiều cao người sút lấy từ Bounds thật của các Renderer trong model, không phải
        /// từ con số 1.8m đoán trên giấy — model Mixamo đứng ở tư thế nào thì đo đúng tư
        /// thế đó.
        /// </summary>
        static void MeasureFraming(UnityEngine.Camera cam)
        {
            cam.aspect = 2400f / 1080f;   // đúng tỉ lệ Pixel 7 nằm ngang, đừng để aspect của cửa sổ editor xen vào

            var kicker = GameObject.Find("Kicker");
            if (kicker != null)
            {
                var rends = kicker.GetComponentsInChildren<Renderer>();
                if (rends.Length > 0)
                {
                    Bounds b = rends[0].bounds;
                    for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                    Report(cam, "bàn chân người sút", new Vector3(b.center.x, b.min.y, b.center.z));
                    Report(cam, "đỉnh đầu người sút", new Vector3(b.center.x, b.max.y, b.center.z));
                    Debug.Log($"[CameraFramingProbe] người sút đứng tại {b.center}, cao {b.size.y:F2}m");
                }
            }

            Report(cam, "quả bóng", new Vector3(0f, 0.11f, 0f));
            Report(cam, "vạch vôi khung thành", new Vector3(0f, 0f, 11f));
            Report(cam, "xà ngang", new Vector3(0f, 2.44f, 11f));
            Report(cam, "cột trái", new Vector3(-3.66f, 1.22f, 11f));
            Report(cam, "cột phải", new Vector3(3.66f, 1.22f, 11f));
            Report(cam, "hàng khán đài cuối", new Vector3(0f, 6.01f, 26.5f));
        }

        static void Report(UnityEngine.Camera cam, string what, Vector3 world)
        {
            Vector3 v = cam.WorldToViewportPoint(world);
            string warn = (v.z <= 0f || v.x < 0f || v.x > 1f || v.y < 0f || v.y > 1f) ? "  ⚠ NGOÀI KHUNG" : "";
            Debug.Log($"[CameraFramingProbe] {what,-24} ngang {v.x:F3}  dọc {v.y:F3}  xa {v.z:F1}m{warn}");
        }
    }
}
