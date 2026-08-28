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

            Object.DestroyImmediate(tex);
            rt.Release();
            Object.DestroyImmediate(rt);

            Debug.Log($"[CameraFramingProbe] Đã chụp {outPath} — máy tại {pos}, nhìn {pose.lookAt}, fov {pose.fov}.");
        }
    }
}
