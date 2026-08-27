using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Eleven.Tests.EditMode
{
    /// <summary>
    /// Bộ test tự động kiểm tra tính toàn vẹn và phân tầng kiến trúc của các Assembly Definition (.asmdef).
    /// Thay thế quy trình thủ công (so sánh kích thước bản build hoặc kiểm tra lỗi thủ công sau khi đóng gói),
    /// bộ test này chứng minh ở mức tĩnh rằng:
    /// 1. Mã nguồn chỉ dành cho Unity Editor không bao giờ bị rò rỉ vào bản build runtime của người chơi.
    /// 2. Không tồn tại chu trình phụ thuộc vòng (circular dependency).
    /// 3. Chiều phụ thuộc giữa các tầng kiến trúc (Core -> Gameplay -> Presentation/UI) tuân thủ tuyệt đối thiết kế.
    /// </summary>
    [TestFixture]
    public class AssemblyHygieneTests
    {
        [Serializable]
        private class AsmdefData
        {
            public string name;
            public string[] references;
            public string[] includePlatforms;
        }

        private static readonly string[] RuntimeAssemblyNames = new[]
        {
            "Eleven.Ball",
            "Eleven.Keeper",
            "Eleven.Shooter",
            "Eleven.Match",
            "Eleven.Presentation",
            "Eleven.UI"
        };

        private const string EditorBallAsmdefName = "Eleven.Editor.Ball";

        [Test]
        public void AsmdefEditorOnly_DungCoIncludePlatformsLaEditor()
        {
            AsmdefData asmdef = LayAsmdef(ChiMucAsmdef(), EditorBallAsmdefName, out string path);

            if (asmdef.includePlatforms == null || asmdef.includePlatforms.Length != 1 || asmdef.includePlatforms[0] != "Editor")
            {
                string currentPlatforms = asmdef.includePlatforms == null
                    ? "null (trống)"
                    : $"[{string.Join(", ", asmdef.includePlatforms)}]";

                Assert.Fail(
                    $"Assembly Editor '{EditorBallAsmdefName}' tại '{path}' có includePlatforms = {currentPlatforms}. " +
                    $"Giá trị bắt buộc phải là duy nhất [\"Editor\"]. " +
                    $"HẬU QUẢ: Toàn bộ cửa sổ, công cụ và mã Editor sẽ bị biên dịch vào bản build Standalone của người chơi, " +
                    $"làm phình to dung lượng build và gây crash/lỗi biên dịch runtime.");
            }
        }

        [Test]
        public void KhongAssemblyRuntimeNao_ThamChieuToiAssemblyChiCoOEditor()
        {
            var chiMuc = ChiMucAsmdef();
            var viPham = new List<string>();

            foreach (string runtimeName in RuntimeAssemblyNames)
            {
                AsmdefData runtimeAsmdef = LayAsmdef(chiMuc, runtimeName, out string path);
                string[] references = runtimeAsmdef.references ?? Array.Empty<string>();

                foreach (string refName in references)
                {
                    if (refName == EditorBallAsmdefName
                        || refName.StartsWith("UnityEditor", StringComparison.OrdinalIgnoreCase))
                    {
                        viPham.Add($"{runtimeName} → {refName}   ({path})");
                    }
                }
            }

            // Gom hết rồi mới báo, không dừng ở vi phạm đầu tiên: sửa một cái rồi chạy lại để
            // lòi ra cái tiếp theo là cách tốn thời gian nhất.
            if (viPham.Count > 0)
            {
                Assert.Fail(
                    $"{viPham.Count} assembly runtime đang tham chiếu code chỉ có ở Editor:\n  " +
                    string.Join("\n  ", viPham) + "\n" +
                    "HẬU QUẢ: code Editor bị kéo vào bản build của người chơi — build hỏng với " +
                    "CS0246 (không tìm thấy namespace 'UnityEditor'), hoặc tệ hơn là build thành " +
                    "công và mang theo cả công cụ Editor.");
            }
        }

        [Test]
        public void KhongFileRuntimeNao_DungUnityEditorNgoaiVungUNITY_EDITOR()
        {
            string codeDirectory = Path.Combine(Application.dataPath, "_Project", "Code");

            if (!Directory.Exists(codeDirectory))
            {
                Assert.Fail(
                    $"Không tìm thấy thư mục mã nguồn runtime tại '{codeDirectory}'. " +
                    $"HẬU QUẢ: Cấu trúc thư mục dự án đã bị thay đổi ngoài ý muốn, không thể xác thực mã nguồn runtime.");
            }

            string[] csFiles = Directory.GetFiles(codeDirectory, "*.cs", SearchOption.AllDirectories);

            // Canh chính bộ quét, không canh code bị quét. Nếu thư mục đổi tên hoặc bị dời đi,
            // vòng lặp dưới chạy qua 0 file và test XANH mà không kiểm gì — đúng kiểu hỏng tệ
            // nhất, vì nó im lặng. 10 là ngưỡng thấp có chủ ý: nó bắt "0 file", không phải bắt
            // "ít file hơn hôm qua".
            Assert.Greater(csFiles.Length, 10,
                $"Chỉ quét được {csFiles.Length} file .cs dưới '{codeDirectory}'. " +
                "HẬU QUẢ: test này đang XANH GIẢ — nó không đọc code runtime nữa.");

            var violations = new List<string>();

            foreach (string filePath in csFiles)
            {
                string[] lines = File.ReadAllLines(filePath);
                var editorConditionStack = new Stack<bool>();

                for (int i = 0; i < lines.Length; i++)
                {
                    string rawLine = lines[i];
                    string line = rawLine.Trim();

                    if (line.StartsWith("#if", StringComparison.Ordinal))
                    {
                        bool isEditorCondition = line.Contains("UNITY_EDITOR") && !line.Contains("!UNITY_EDITOR");
                        bool parentIsEditor = editorConditionStack.Count > 0 && editorConditionStack.Peek();
                        editorConditionStack.Push(parentIsEditor || isEditorCondition);
                    }
                    else if (line.StartsWith("#elif", StringComparison.Ordinal))
                    {
                        if (editorConditionStack.Count > 0)
                        {
                            editorConditionStack.Pop();
                        }
                        bool isEditorCondition = line.Contains("UNITY_EDITOR") && !line.Contains("!UNITY_EDITOR");
                        bool parentIsEditor = editorConditionStack.Count > 0 && editorConditionStack.Peek();
                        editorConditionStack.Push(parentIsEditor || isEditorCondition);
                    }
                    else if (line.StartsWith("#else", StringComparison.Ordinal))
                    {
                        if (editorConditionStack.Count > 0)
                        {
                            editorConditionStack.Pop();
                            bool parentIsEditor = editorConditionStack.Count > 0 && editorConditionStack.Peek();
                            editorConditionStack.Push(parentIsEditor);
                        }
                    }
                    else if (line.StartsWith("#endif", StringComparison.Ordinal))
                    {
                        if (editorConditionStack.Count > 0)
                        {
                            editorConditionStack.Pop();
                        }
                    }
                    else
                    {
                        // Bản giao ban đầu chỉ bắt đúng dạng `using UnityEditor...;`. Ba dạng
                        // khác lọt lưới, và cả ba đều làm hỏng build y hệt:
                        //   using UE = UnityEditor;              (đặt bí danh)
                        //   using UnityEditor;   // ghi chú      (không kết thúc bằng ';')
                        //   UnityEditor.AssetDatabase.Refresh(); (viết đủ tên, không cần using)
                        // Nên bắt theo TOKEN "UnityEditor" ở bất kỳ đâu ngoài chuỗi/ghi chú.
                        if (CoNhacToiUnityEditor(line))
                        {
                            bool isInsideEditorBlock = editorConditionStack.Count > 0 && editorConditionStack.Peek();
                            if (!isInsideEditorBlock)
                            {
                                string relativePath = filePath.Replace(Application.dataPath, "Assets");
                                violations.Add($"{relativePath} (Dòng {i + 1}): \"{line}\"");
                            }
                        }
                    }
                }
            }

            if (violations.Count > 0)
            {
                string violationReport = string.Join("\n", violations);
                Assert.Fail(
                    $"Phát hiện {violations.Count} vị trí sử dụng 'using UnityEditor' trong thư mục runtime mà KHÔNG được bao bọc bởi '#if UNITY_EDITOR':\n{violationReport}\n" +
                    $"HẬU QUẢ: Trình biên dịch của Unity khi build target Player (iOS/Android/Standalone) sẽ báo lỗi CS0246 và làm gãy toàn bộ quy trình CI/CD build game.");
            }
        }

        [Test]
        public void BoQuetUnityEditor_ThatSuBatDuoc_KIEM_CHINH_BO_QUET_TRUOC()
        {
            // Test ở trên chỉ nói "không tìm thấy vi phạm nào". Câu đó đúng cả khi bộ quét hỏng
            // hoàn toàn. Nên phải kiểm chính bộ quét bằng những dòng đã biết trước đáp án —
            // nửa đầu PHẢI bắt, nửa sau PHẢI tha.
            //
            // Đây là chốt kiểm soát thật: chỉ cần ai sửa CoNhacToiUnityEditor cho "gọn hơn" mà
            // làm hỏng nó, ô nghiệm thu T11 sẽ lặng lẽ mất hiệu lực nếu không có test này.
            string[] phaiBat =
            {
                "using UnityEditor;",
                "using UnityEditor.SceneManagement;",
                "using UE = UnityEditor;",                 // đặt bí danh
                "using UnityEditor;   // dọn sau",         // có ghi chú đuôi
                "UnityEditor.AssetDatabase.Refresh();",    // viết đủ tên, không cần using
                "        var x = UnityEditor.EditorUtility.DisplayDialog(\"a\", \"b\", \"c\");",
            };

            string[] phaiTha =
            {
                "using UnityEngine;",
                "// using UnityEditor;  — đã bỏ, giữ lại cho biết lịch sử",
                "Debug.Log(\"cần UnityEditor mới chạy được\");",   // chỉ nằm trong chuỗi
                "class MyUnityEditorHelper { }",                    // dính liền, không phải namespace
                "var s = \"UnityEditor\";",
                "int UnityEditorCount = 0;",
            };

            foreach (string dong in phaiBat)
                Assert.IsTrue(CoNhacToiUnityEditor(dong),
                    $"Bộ quét BỎ SÓT dòng lẽ ra phải bắt: {dong}");

            foreach (string dong in phaiTha)
                Assert.IsFalse(CoNhacToiUnityEditor(dong),
                    $"Bộ quét BẮT OAN dòng vô hại: {dong}");
        }

        [Test]
        public void ChieuPhuThuocGiuaCacAssembly_KhongCoVongLap()
        {
            string projectDirectory = Path.Combine(Application.dataPath, "_Project");
            if (!Directory.Exists(projectDirectory))
            {
                Assert.Fail(
                    $"Không tìm thấy thư mục '{projectDirectory}'. " +
                    $"HẬU QUẢ: Không thể quét đồ thị phụ thuộc giữa các assembly definition.");
            }

            string[] asmdefFiles = Directory.GetFiles(projectDirectory, "*.asmdef", SearchOption.AllDirectories);
            var graph = new Dictionary<string, List<string>>(StringComparer.Ordinal);

            foreach (string file in asmdefFiles)
            {
                string json = File.ReadAllText(file);
                AsmdefData data = JsonUtility.FromJson<AsmdefData>(json);
                if (data != null && !string.IsNullOrEmpty(data.name) && data.name.StartsWith("Eleven.", StringComparison.Ordinal))
                {
                    string[] refs = data.references ?? Array.Empty<string>();
                    var filteredRefs = refs.Where(r => r.StartsWith("Eleven.", StringComparison.Ordinal)).ToList();
                    graph[data.name] = filteredRefs;
                }
            }

            // Đảm bảo tất cả các assembly mong đợi đều có mặt trong đồ thị
            foreach (string runtimeName in RuntimeAssemblyNames)
            {
                if (!graph.ContainsKey(runtimeName))
                {
                    Assert.Fail(
                        $"Assembly '{runtimeName}' không tồn tại trong đồ thị phụ thuộc. " +
                        $"HẬU QUẢ: Thiếu assembly định nghĩa runtime, cấu trúc dự án bị phân mảnh.");
                }
            }

            // DFS kiểm tra chu trình (0: White, 1: Gray, 2: Black)
            var colors = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (string node in graph.Keys)
            {
                colors[node] = 0;
            }

            var currentPath = new List<string>();

            foreach (string node in graph.Keys)
            {
                if (colors[node] == 0)
                {
                    if (CheckCycleDfs(node, graph, colors, currentPath, out List<string> cycle))
                    {
                        string cycleTrace = string.Join(" -> ", cycle);
                        Assert.Fail(
                            $"Phát hiện chu trình phụ thuộc vòng giữa các assembly:\n  {cycleTrace}\n" +
                            $"HẬU QUẢ: Unity và .NET không thể xác định thứ tự biên dịch, dẫn tới lỗi circular dependency compilation " +
                            $"và phá vỡ tính module hóa.");
                    }
                }
            }
        }

        [Test]
        public void TangDuoi_KhongBietGiTangTren()
        {
            // 1. Eleven.Ball là tầng đáy: Không được tham chiếu bất cứ Eleven.* nào
            var chiMuc = ChiMucAsmdef();

            AsmdefData ball = LayAsmdef(chiMuc, "Eleven.Ball", out string ballPath);
            string[] ballRefs = ball.references ?? Array.Empty<string>();
            var illegalBallRefs = ballRefs.Where(r => r.StartsWith("Eleven.", StringComparison.Ordinal)).ToList();
            if (illegalBallRefs.Count > 0)
            {
                Assert.Fail(
                    $"Assembly tầng đáy 'Eleven.Ball' tại '{ballPath}' đang tham chiếu tới: [{string.Join(", ", illegalBallRefs)}]. " +
                    $"HẬU QUẢ: Eleven.Ball là tầng dữ liệu/vật lý cơ sở, việc phụ thuộc vào tầng trên làm phá vỡ kiến trúc module độc lập " +
                    $"và ngăn cản khả năng tái sử dụng mô phỏng bóng độc lập.");
            }

            // 2. Eleven.Keeper và Eleven.Shooter: Không tham chiếu lẫn nhau và không tham chiếu Eleven.Match
            AsmdefData keeper = LayAsmdef(chiMuc, "Eleven.Keeper", out string keeperPath);
            string[] keeperRefs = keeper.references ?? Array.Empty<string>();
            if (keeperRefs.Contains("Eleven.Shooter") || keeperRefs.Contains("Eleven.Match"))
            {
                Assert.Fail(
                    $"Assembly 'Eleven.Keeper' tại '{keeperPath}' tham chiếu trái phép tới Eleven.Shooter hoặc Eleven.Match. " +
                    $"HẬU QUẢ: Phá vỡ tính đóng gói của tác tử thủ môn; thủ môn không được biết về đối thủ trực tiếp hay tầng điều phối trận đấu.");
            }

            AsmdefData shooter = LayAsmdef(chiMuc, "Eleven.Shooter", out string shooterPath);
            string[] shooterRefs = shooter.references ?? Array.Empty<string>();
            if (shooterRefs.Contains("Eleven.Keeper") || shooterRefs.Contains("Eleven.Match"))
            {
                Assert.Fail(
                    $"Assembly 'Eleven.Shooter' tại '{shooterPath}' tham chiếu trái phép tới Eleven.Keeper hoặc Eleven.Match. " +
                    $"HẬU QUẢ: Phá vỡ tính đóng gói của tác tử cầu thủ sút; cầu thủ sút không được biết về thủ môn hay tầng điều phối trận đấu.");
            }

            // 3. Eleven.Match: Không tham chiếu Presentation hoặc UI
            AsmdefData match = LayAsmdef(chiMuc, "Eleven.Match", out string matchPath);
            string[] matchRefs = match.references ?? Array.Empty<string>();
            if (matchRefs.Contains("Eleven.Presentation") || matchRefs.Contains("Eleven.UI"))
            {
                Assert.Fail(
                    $"Assembly 'Eleven.Match' tại '{matchPath}' tham chiếu trái phép tới Eleven.Presentation hoặc Eleven.UI. " +
                    $"HẬU QUẢ: Eleven.Match là tầng điều phối luật thi đấu thuần túy, việc phụ thuộc vào Presentation/UI sẽ biến nó thành " +
                    $"God-module dính chặt với giao diện, không thể chạy headless server hoặc auto-simulation test.");
            }
        }

        /// <summary>
        /// Dòng này có nhắc tới namespace <c>UnityEditor</c> theo cách thật sự biên dịch không?
        /// Bỏ qua phần ghi chú <c>//</c> và mọi thứ nằm trong chuỗi ký tự — nếu không thì chính
        /// các thông điệp Assert trong file này (có chứa chữ "UnityEditor") sẽ bị tính là vi phạm.
        /// Cũng đòi ranh giới từ, để <c>MyUnityEditorHelper</c> không bị bắt oan.
        /// </summary>
        private static bool CoNhacToiUnityEditor(string line)
        {
            int ghiChu = line.IndexOf("//", StringComparison.Ordinal);
            if (ghiChu >= 0) line = line.Substring(0, ghiChu);

            // Bỏ nội dung mọi chuỗi "..." (đủ dùng ở đây: code runtime không có chuỗi lồng nháy).
            var sb = new System.Text.StringBuilder(line.Length);
            bool trongChuoi = false;
            for (int i = 0; i < line.Length; i++)
            {
                char ch = line[i];
                if (ch == '"' && (i == 0 || line[i - 1] != '\\')) { trongChuoi = !trongChuoi; continue; }
                if (!trongChuoi) sb.Append(ch);
            }
            string sach = sb.ToString();

            int idx = sach.IndexOf("UnityEditor", StringComparison.Ordinal);
            while (idx >= 0)
            {
                char truoc = idx > 0 ? sach[idx - 1] : ' ';
                int sauIdx = idx + "UnityEditor".Length;
                char sau = sauIdx < sach.Length ? sach[sauIdx] : ' ';
                bool ranhGioiTrai = !char.IsLetterOrDigit(truoc) && truoc != '_';
                bool ranhGioiPhai = !char.IsLetterOrDigit(sau) && sau != '_';
                if (ranhGioiTrai && ranhGioiPhai) return true;
                idx = sach.IndexOf("UnityEditor", idx + 1, StringComparison.Ordinal);
            }
            return false;
        }

        /// <summary>
        /// Quét MỘT LẦN toàn bộ Assets, trả về bảng tên assembly → đường dẫn file .asmdef.
        ///
        /// Bản giao ban đầu gọi <c>Directory.GetFiles(..., AllDirectories)</c> lại từ đầu cho
        /// từng assembly — 10+ lần quét đệ quy cả cây Assets cho một lần chạy test. Gom lại một
        /// lần vừa nhanh hơn vừa cho chỗ để bắt TRÙNG TÊN: hai file .asmdef cùng tên thì Unity
        /// không biên dịch được, mà bản cũ lại lặng lẽ lấy <c>files[0]</c> và chạy tiếp.
        /// </summary>
        private static Dictionary<string, string> ChiMucAsmdef()
        {
            string[] files = Directory.GetFiles(Application.dataPath, "*.asmdef", SearchOption.AllDirectories);

            Assert.Greater(files.Length, 0,
                $"Không tìm thấy file .asmdef nào dưới '{Application.dataPath}'. " +
                "HẬU QUẢ: bộ test này sẽ XANH GIẢ — nó không kiểm được gì cả mà vẫn báo đạt.");

            var chiMuc = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string file in files)
            {
                AsmdefData data = JsonUtility.FromJson<AsmdefData>(File.ReadAllText(file));
                if (data == null || string.IsNullOrEmpty(data.name))
                    continue;

                if (chiMuc.TryGetValue(data.name, out string daCo))
                {
                    Assert.Fail(
                        $"Hai file .asmdef cùng khai báo tên '{data.name}':\n  {daCo}\n  {file}\n" +
                        "HẬU QUẢ: Unity từ chối biên dịch khi có tên assembly trùng nhau, và mọi " +
                        "test dưới đây sẽ kiểm nhầm file.");
                }
                chiMuc[data.name] = file;
            }
            return chiMuc;
        }

        private static AsmdefData LayAsmdef(Dictionary<string, string> chiMuc, string asmdefName,
                                            out string matchedFilePath)
        {
            if (!chiMuc.TryGetValue(asmdefName, out matchedFilePath))
            {
                Assert.Fail(
                    $"Không tìm thấy assembly definition tên '{asmdefName}' trong Assets. " +
                    "HẬU QUẢ: thiếu cấu hình kiến trúc; test không xác thực được gì và KHÔNG " +
                    "được lặng lẽ bỏ qua.");
            }

            AsmdefData parsedData = JsonUtility.FromJson<AsmdefData>(File.ReadAllText(matchedFilePath));
            if (parsedData == null)
            {
                Assert.Fail(
                    $"Không parse được JSON từ '{matchedFilePath}'. " +
                    "HẬU QUẢ: file .asmdef hỏng cú pháp, Unity sẽ không biên dịch được assembly.");
            }
            return parsedData;
        }

        private static bool CheckCycleDfs(
            string node,
            Dictionary<string, List<string>> graph,
            Dictionary<string, int> colors,
            List<string> path,
            out List<string> cycle)
        {
            colors[node] = 1; // Đang duyệt (Gray)
            path.Add(node);

            if (graph.TryGetValue(node, out List<string> neighbors))
            {
                foreach (string neighbor in neighbors)
                {
                    if (!graph.ContainsKey(neighbor))
                    {
                        continue;
                    }

                    if (colors[neighbor] == 1) // Phát hiện vòng lặp
                    {
                        int cycleStartIndex = path.IndexOf(neighbor);
                        cycle = new List<string>(path.GetRange(cycleStartIndex, path.Count - cycleStartIndex))
                        {
                            neighbor
                        };
                        return true;
                    }

                    if (colors[neighbor] == 0) // Chưa duyệt (White)
                    {
                        if (CheckCycleDfs(neighbor, graph, colors, path, out cycle))
                        {
                            return true;
                        }
                    }
                }
            }

            path.RemoveAt(path.Count - 1);
            colors[node] = 2; // Đã duyệt xong (Black)
            cycle = null;
            return false;
        }
    }
}
