using System;
using System.Diagnostics;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace Eleven.Editor.BuildPipeline
{
    /// <summary>
    /// Build một lệnh, dùng cả từ CLI lẫn menu Editor.
    /// CLI: Unity -batchmode -executeMethod Eleven.Editor.BuildPipeline.BuildScript.BuildFromCli
    ///      -buildTarget ios|android -outputPath <đường_dẫn> -logFile -
    /// Ràng buộc Phase 0: Android chỉ Vulkan · iOS chỉ Metal · IL2CPP + ARM64 · Release.
    /// </summary>
    public static class BuildScript
    {
        const string DefaultAndroidOutput = "android_build/ElevenMetres.apk";
        const string DefaultIosOutput = "ios_build";

        [MenuItem("Eleven/Phase 0/Build Android (Release)")]
        public static void BuildAndroidMenu() => Build(BuildTarget.Android, DefaultAndroidOutput);

        [MenuItem("Eleven/Phase 0/Build iOS (Xcode project)")]
        public static void BuildIosMenu() => Build(BuildTarget.iOS, DefaultIosOutput);

        /// <summary>Điểm vào batchmode. Ném exception khi hỏng → Unity trả exit code khác 0.</summary>
        public static void BuildFromCli()
        {
            string target = GetArg("-buildTarget")?.ToLowerInvariant();
            string outputPath = GetArg("-outputPath");

            switch (target)
            {
                case "ios":
                    Build(BuildTarget.iOS, string.IsNullOrEmpty(outputPath) ? DefaultIosOutput : outputPath);
                    break;
                case "android":
                    Build(BuildTarget.Android, string.IsNullOrEmpty(outputPath) ? DefaultAndroidOutput : outputPath);
                    break;
                default:
                    throw new ArgumentException(
                        $"[BuildScript] -buildTarget phải là 'ios' hoặc 'android', nhận được: '{target}'");
            }
        }

        static void Build(BuildTarget target, string outputPath)
        {
            ConfigurePlayer(target);

            var scenes = EditorBuildSettings.scenes;
            if (scenes == null || scenes.Length == 0)
                throw new InvalidOperationException(
                    "[BuildScript] Chưa có scene nào trong Build Settings. Thêm ít nhất một scene trước khi build.");

            var options = new BuildPlayerOptions
            {
                scenes = Array.ConvertAll(scenes, s => s.path),
                target = target,
                locationPathName = outputPath,
                options = BuildOptions.None,
            };

            var stopwatch = Stopwatch.StartNew();
            // Tên namespace Eleven.Editor.BuildPipeline che khuất UnityEditor.BuildPipeline —
            // phải gọi đủ đường dẫn, nếu không trình biên dịch tìm BuildPlayer trong namespace này.
            BuildReport report = UnityEditor.BuildPipeline.BuildPlayer(options);
            stopwatch.Stop();

            if (report.summary.result != BuildResult.Succeeded)
                throw new Exception(
                    $"[BuildScript] Build THẤT BẠI: {report.summary.result}, {report.summary.totalErrors} lỗi. " +
                    "Exit code sẽ khác 0.");

            UnityEngine.Debug.Log(
                $"[BuildScript] Build OK: {target} → {outputPath} " +
                $"({report.summary.totalSize / (1024 * 1024)} MB, {stopwatch.Elapsed.TotalSeconds:F0}s, " +
                $"commit {GetCommitShortHash()})");
            // Không bắt lỗi im lặng: mọi nhánh thất bại đều ném → exit code != 0.
        }

        static void ConfigurePlayer(BuildTarget target)
        {
            // IL2CPP + Release cho cả hai nền tảng.
            // Dùng NamedBuildTarget: các overload nhận BuildTargetGroup đã bị đánh dấu obsolete ở Unity 6.
            var named = target == BuildTarget.Android
                ? NamedBuildTarget.Android
                : NamedBuildTarget.iOS;
            PlayerSettings.SetIl2CppCompilerConfiguration(named, Il2CppCompilerConfiguration.Release);
            PlayerSettings.stripEngineCode = true;

            if (target == BuildTarget.Android)
            {
                PlayerSettings.SetScriptingBackend(named, ScriptingImplementation.IL2CPP);
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64; // bỏ ARMv7

                // Vulkan duy nhất — GLES3 bị gỡ khỏi danh sách API.
                PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
                PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });

                EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;
                EditorUserBuildSettings.androidBuildType = AndroidBuildType.Release;
            }
            else if (target == BuildTarget.iOS)
            {
                PlayerSettings.SetScriptingBackend(named, ScriptingImplementation.IL2CPP);
                // Metal duy nhất cho iOS.
                PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.iOS, false);
                PlayerSettings.SetGraphicsAPIs(BuildTarget.iOS, new[] { GraphicsDeviceType.Metal });

                EditorUserBuildSettings.symlinkSources = true; // vòng lặp build Xcode nhanh hơn
                // iOSBuildConfigType đã bị gỡ ở Unity 6; tên mới là iOSXcodeBuildConfig + enum XcodeBuildConfig.
                EditorUserBuildSettings.iOSXcodeBuildConfig = XcodeBuildConfig.Release;
            }

            // Nhúng git commit hash: hiện trong HUD qua Application.version.
            string hash = GetCommitShortHash();
            if (!string.IsNullOrEmpty(hash))
                PlayerSettings.bundleVersion = $"0.1.0+{hash}";
        }

        /// <summary>Đọc commit hash từ git CLI; nếu thất bại trả chuỗi rỗng chứ không chặn build.</summary>
        public static string GetCommitShortHash()
        {
            try
            {
                string projectRoot = Application.dataPath.Replace("/Assets", "");
                var psi = new ProcessStartInfo("git", "rev-parse --short HEAD")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = projectRoot,
                };
                using var proc = Process.Start(psi);
                proc.WaitForExit(3000);
                return proc.ExitCode == 0 ? proc.StandardOutput.ReadToEnd().Trim() : "";
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"[BuildScript] Không đọc được git hash: {e.Message}");
                return "";
            }
        }

        static string GetArg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name)
                    return args[i + 1];
            return null;
        }
    }
}
