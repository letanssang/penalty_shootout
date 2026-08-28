using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Eleven.Editor.Tools
{
    /// <summary>
    /// Chụp một dải khung hình của một clip ra PNG, để soi động tác bằng mắt mà không cần
    /// mở Unity. Dùng cho việc xác định khung chạm bóng: đọc số thì chỉ ra "chân nhanh nhất
    /// ở khung 52", nhìn ảnh mới biết khung đó chân đã tới bóng hay còn cách nửa gang.
    ///
    /// Chạy KHÔNG có -nographics (cần GPU để render):
    ///   Unity -batchmode -quit -projectPath . \
    ///     -executeMethod Eleven.Editor.Tools.ClipFrameRenderer.Run -clip PenaltyKick -frames 12
    /// </summary>
    public static class ClipFrameRenderer
    {
        public static void Run()
        {
            var args = System.Environment.GetCommandLineArgs();
            var clipName = ArgValue(args, "-clip") ?? "PenaltyKick";
            var count = int.TryParse(ArgValue(args, "-frames"), out var n) ? n : 12;
            var outDir = ArgValue(args, "-out") ?? "Temp/ClipFrames";
            var from = float.TryParse(ArgValue(args, "-from"), out var f) ? f : 0f;
            var to = float.TryParse(ArgValue(args, "-to"), out var t) ? t : 1f;

            var clipPath = AssetDatabase.FindAssets("t:Model", new[] { MixamoModelImport.Root.TrimEnd('/') })
                .Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == clipName);
            if (clipPath == null) { Debug.LogError($"[ClipFrameRenderer] Không thấy clip {clipName}"); return; }

            var clip = AssetDatabase.LoadAllAssetsAtPath(clipPath)
                .OfType<AnimationClip>().FirstOrDefault(c => (c.hideFlags & HideFlags.HideInHierarchy) == 0);
            var rig = AssetDatabase.LoadAssetAtPath<GameObject>(MixamoModelImport.AvatarSourcePath);
            if (clip == null || rig == null) { Debug.LogError("[ClipFrameRenderer] Thiếu clip hoặc nhân vật."); return; }

            var actor = (GameObject)PrefabUtility.InstantiatePrefab(rig);
            var camGo = new GameObject("ProbeCam");
            var cam = camGo.AddComponent<Camera>();
            // Mặt đất + quả bóng làm MỐC QUY CHIẾU. Thiếu chúng thì ảnh chụp chỉ là một hình
            // người lơ lửng trên nền trơn: không nói được chân đã chạm bóng chưa, thậm chí
            // không biết người đang đứng hay đang ngã.
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.transform.localScale = new Vector3(2f, 1f, 2f);
            ground.GetComponent<Renderer>().sharedMaterial.color = new Color(0.22f, 0.42f, 0.24f);

            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.transform.localScale = Vector3.one * 0.22f;   // bóng số 5, đường kính 22 cm
            ball.transform.position = new Vector3(0f, 0.11f, 0f);
            // CÒN THIẾU: bóng đang đặt ở gốc toạ độ, nhưng clip có root motion đưa người sút
            // đi tới 2.5 m nên chỗ chân thật sự chạm bóng KHÔNG phải gốc. Muốn dùng ảnh này để
            // chốt khung chạm thì T35 phải đặt bóng theo vị trí chân trụ ở cuối đà, không phải
            // ở (0,0,0). Hiện tại ảnh chỉ đọc được tư thế và tương quan với mặt đất.

            var lightGo = new GameObject("ProbeLight");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.4f;
            lightGo.transform.rotation = Quaternion.Euler(45f, 200f, 0f);

            // Camera BÁM HÔNG chứ không đứng yên. Clip như PenaltyKick dịch 2.5 m; camera cố
            // định thì nhân vật đi ra khỏi khung ngay sau một phần ba clip và bảy ảnh cuối
            // chụp được nền trống.
            var hips = actor.GetComponent<Animator>()?.GetBoneTransform(HumanBodyBones.Hips);
            var camOffset = new Vector3(-3.0f, -0.35f, 0.9f);   // ngang tầm chân, nhìn từ bên trái
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.16f, 0.18f, 0.22f);

            const int W = 480, H = 480;
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);

            Directory.CreateDirectory(outDir);
            try
            {
                for (int i = 0; i < count; i++)
                {
                    var norm = count == 1 ? from : Mathf.Lerp(from, to, i / (float)(count - 1));
                    clip.SampleAnimation(actor, norm * clip.length);

                    var focus = hips != null ? hips.position : new Vector3(0f, 0.9f, 0f);
                    cam.transform.position = focus + camOffset;
                    cam.transform.LookAt(focus - new Vector3(0f, 0.45f, 0f));   // ngắm vào chân, không vào hông

                    cam.Render();
                    RenderTexture.active = rt;
                    tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
                    tex.Apply();
                    RenderTexture.active = null;
                    File.WriteAllBytes($"{outDir}/{clipName}_{i:D2}_n{norm:F3}.png", tex.EncodeToPNG());
                }
                Debug.Log($"[ClipFrameRenderer] {count} khung → {outDir}");
            }
            finally
            {
                cam.targetTexture = null;
                Object.DestroyImmediate(tex);
                rt.Release();
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(camGo);
                Object.DestroyImmediate(lightGo);
                Object.DestroyImmediate(actor);
                Object.DestroyImmediate(ground);
                Object.DestroyImmediate(ball);
            }
        }

        static string ArgValue(string[] args, string key)
        {
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == key) return args[i + 1];
            return null;
        }
    }
}
