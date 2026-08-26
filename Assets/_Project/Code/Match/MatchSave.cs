// Assets/_Project/Code/Match/MatchSave.cs
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Unity.Collections;
using UnityEngine;

namespace Eleven.Match {
  /// <summary>
  /// Hệ thống lưu / đọc tiến trình trận đấu dạng văn bản thuần, hỗ trợ ghi nguyên tử và kiểm tra checksum FNV-1a.
  /// </summary>
  public static class MatchSave {
    public const int CurrentSchemaVersion = 2;

    /// <summary>
    /// Đường dẫn file lưu mặc định trong persistentDataPath.
    /// Đây là hàm duy nhất được phép truy cập UnityEngine.Application.
    /// </summary>
    public static string DefaultPath() {
      return Path.Combine(Application.persistentDataPath, "match.sav");
    }

    /// <summary>
    /// Tính giá trị băm FNV-1a 32-bit cho chuỗi văn bản UTF-8 (không phụ thuộc nền tảng).
    /// </summary>
    public static uint ComputeFnv1a(string text) {
      if (text == null) return 0;
      uint hash = 2166136261u;
      byte[] bytes = Encoding.UTF8.GetBytes(text);
      for (int i = 0; i < bytes.Length; i++) {
        hash ^= bytes[i];
        hash *= 16777619u;
      }
      return hash;
    }

    /// <summary>
    /// Lưu trạng thái loạt sút ra file với cơ chế ghi nguyên tử (atomic write).
    /// </summary>
    public static bool TrySave(in ShootoutState state, string filePath, out string error) {
      error = null;

      if (string.IsNullOrWhiteSpace(filePath)) {
        error = "Đường dẫn file lưu không được để trống.";
        return false;
      }

      string directory = Path.GetDirectoryName(filePath);
      if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory)) {
        error = $"Thư mục không tồn tại: {directory}";
        return false;
      }

      string tempPath = filePath + ".tmp";

      try {
        long timestampTicks = DateTime.UtcNow.Ticks;

        // Xây dựng phần thân nội dung dữ liệu
        StringBuilder sb = new StringBuilder();
        sb.Append("version=").Append(CurrentSchemaVersion.ToString(CultureInfo.InvariantCulture)).Append('\n');
        sb.Append("timestamp=").Append(timestampTicks.ToString(CultureInfo.InvariantCulture)).Append('\n');
        sb.Append("homeKicksFirst=").Append(state.homeKicksFirst ? "true" : "false").Append('\n');
        sb.Append("home=").Append(SerializeKicks(state.home)).Append('\n');
        sb.Append("away=").Append(SerializeKicks(state.away)).Append('\n');

        string body = sb.ToString();
        uint checksum = ComputeFnv1a(body);
        string fullContent = body + "checksum=" + checksum.ToString(CultureInfo.InvariantCulture) + "\n";

        // Ghi ra file tạm và đẩy toàn bộ dữ liệu xuống đĩa cứng
        using (FileStream stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
        using (StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(false))) {
          writer.Write(fullContent);
          writer.Flush();
          stream.Flush(true);
        }

        // Đổi tên / thay thế nguyên tử sang file đích
        if (File.Exists(filePath)) {
          File.Replace(tempPath, filePath, null);
        } else {
          File.Move(tempPath, filePath);
        }

        return true;
      } catch (Exception ex) {
        // Dọn dẹp file tạm nếu còn sót lại do lỗi
        if (File.Exists(tempPath)) {
          try {
            File.Delete(tempPath);
          } catch {
            // Bỏ qua lỗi xoá file tạm để trả về lỗi chính
          }
        }
        error = $"Lỗi khi ghi file lưu trận đấu: {ex.Message}";
        return false;
      }
    }

    /// <summary>
    /// Đọc trạng thái loạt sút từ file. Tuyệt đối không ném ngoại lệ, trả về false và error khi có lỗi.
    /// </summary>
    public static bool TryLoad(string filePath, out ShootoutState state, out string error) {
      state = default;
      error = null;

      if (string.IsNullOrWhiteSpace(filePath)) {
        error = "Đường dẫn file không được để trống.";
        return false;
      }

      try {
        if (!File.Exists(filePath)) {
          error = $"File không tồn tại: {filePath}";
          return false;
        }

        string rawContent;
        using (FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
        using (StreamReader reader = new StreamReader(stream, Encoding.UTF8)) {
          rawContent = reader.ReadToEnd();
        }

        if (string.IsNullOrWhiteSpace(rawContent)) {
          error = "File rỗng hoặc chỉ chứa khoảng trắng.";
          return false;
        }

        // Chuẩn hoá ký tự ngắt dòng sang \n
        string normalized = rawContent.Replace("\r\n", "\n").Replace('\r', '\n');

        int checksumIndex = normalized.LastIndexOf("checksum=", StringComparison.Ordinal);
        if (checksumIndex < 0) {
          error = "Không tìm thấy trường checksum trong file.";
          return false;
        }

        string body = normalized.Substring(0, checksumIndex);
        string checksumLine = normalized.Substring(checksumIndex);
        int newlineAfterChecksum = checksumLine.IndexOf('\n');
        if (newlineAfterChecksum >= 0) {
          checksumLine = checksumLine.Substring(0, newlineAfterChecksum);
        }

        string[] checksumParts = checksumLine.Split('=');
        if (checksumParts.Length != 2 || !uint.TryParse(checksumParts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out uint expectedChecksum)) {
          error = "Định dạng checksum không hợp lệ hoặc bị hỏng.";
          return false;
        }

        uint actualChecksum = ComputeFnv1a(body);
        if (actualChecksum != expectedChecksum) {
          error = $"Checksum không khớp (kỳ vọng: {expectedChecksum}, thực tế: {actualChecksum}). File đã bị chỉnh sửa hoặc bị hỏng.";
          return false;
        }

        // Phân tích các cặp khoá-giá trị trong phần thân
        string[] lines = body.Split('\n');
        Dictionary<string, string> fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < lines.Length; i++) {
          string trimmedLine = lines[i].Trim();
          if (string.IsNullOrEmpty(trimmedLine)) continue;

          int eqIdx = trimmedLine.IndexOf('=');
          if (eqIdx >= 0) {
            string key = trimmedLine.Substring(0, eqIdx).Trim();
            string val = trimmedLine.Substring(eqIdx + 1).Trim();
            fields[key] = val;
          }
        }

        if (!fields.TryGetValue("version", out string versionStr) ||
            !int.TryParse(versionStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int version)) {
          error = "Thiếu trường 'version' hoặc định dạng version không hợp lệ.";
          return false;
        }

        if (version > CurrentSchemaVersion) {
          error = $"Phiên bản file ({version}) lớn hơn phiên bản hệ thống hỗ trợ ({CurrentSchemaVersion}).";
          return false;
        }

        if (version < 1) {
          error = $"Phiên bản file không hợp lệ: {version}.";
          return false;
        }

        // Đọc trường homeKicksFirst: v1 mặc định true, v2+ bắt buộc có trong file
        bool homeKicksFirst = true;
        if (version >= 2) {
          if (!fields.TryGetValue("homeKicksFirst", out string hkfStr) || !bool.TryParse(hkfStr, out homeKicksFirst)) {
            error = "Thiếu hoặc sai định dạng trường 'homeKicksFirst' trong schema phiên bản 2.";
            return false;
          }
        }

        if (!fields.TryGetValue("home", out string homeStr)) {
          error = "Thiếu trường 'home' trong dữ liệu file.";
          return false;
        }

        if (!fields.TryGetValue("away", out string awayStr)) {
          error = "Thiếu trường 'away' trong dữ liệu file.";
          return false;
        }

        if (!TryParseKicks(homeStr, out List<KickResult> homeList, out error)) {
          return false;
        }

        if (!TryParseKicks(awayStr, out List<KickResult> awayList, out error)) {
          return false;
        }

        ShootoutState result = default;
        result.homeKicksFirst = homeKicksFirst;

        for (int i = 0; i < homeList.Count; i++) {
          result.home.Add(homeList[i]);
        }

        for (int i = 0; i < awayList.Count; i++) {
          result.away.Add(awayList[i]);
        }

        state = result;
        return true;
      } catch (Exception ex) {
        state = default;
        error = $"Lỗi không xác định khi nạp dữ liệu file: {ex.Message}";
        return false;
      }
    }

    private static string SerializeKicks(in FixedList64Bytes<KickResult> kicks) {
      if (kicks.Length == 0) return string.Empty;
      StringBuilder sb = new StringBuilder();
      for (int i = 0; i < kicks.Length; i++) {
        if (i > 0) sb.Append(',');
        sb.Append(((byte)kicks[i]).ToString(CultureInfo.InvariantCulture));
      }
      return sb.ToString();
    }

    private static bool TryParseKicks(string kickData, out List<KickResult> kicks, out string error) {
      kicks = new List<KickResult>();
      error = null;

      if (string.IsNullOrWhiteSpace(kickData)) {
        return true;
      }

      string[] tokens = kickData.Split(',');
      for (int i = 0; i < tokens.Length; i++) {
        string token = tokens[i].Trim();
        if (string.IsNullOrEmpty(token)) {
          error = "Dữ liệu lượt sút chứa phần tử rỗng giữa các dấu phẩy.";
          return false;
        }

        if (!byte.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out byte val)) {
          error = $"Giá trị lượt sút '{token}' không phải số nguyên byte hợp lệ.";
          return false;
        }

        if (val > (byte)KickResult.Missed) {
          error = $"Giá trị lượt sút {val} không hợp lệ (phải từ 0 đến 2).";
          return false;
        }

        kicks.Add((KickResult)val);
      }

      // Lay suc chua tu chinh kieu du lieu, khong viet cung: neu sau nay doi sang
      // FixedList128Bytes thi gioi han tu dong noi ra theo.
      int sucChua = default(FixedList64Bytes<KickResult>).Capacity;
      if (kicks.Count > sucChua) {
        error = $"Số lượt sút ({kicks.Count}) vượt quá sức chứa tối đa của danh sách ({sucChua} phần tử).";
        return false;
      }

      return true;
    }
  }
}
