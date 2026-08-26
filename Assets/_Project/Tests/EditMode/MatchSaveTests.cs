// Assets/_Project/Tests/EditMode/MatchSaveTests.cs
using System;
using System.Globalization;
using System.IO;
using System.Text;
using NUnit.Framework;
using Eleven.Match;

namespace Eleven.Match.Tests {
  [TestFixture]
  public class MatchSaveTests {
    private string _tempDirectory;

    [SetUp]
    public void SetUp() {
      _tempDirectory = Path.Combine(Path.GetTempPath(), "MatchSaveTests_" + Guid.NewGuid().ToString("N"));
      Directory.CreateDirectory(_tempDirectory);
    }

    [TearDown]
    public void TearDown() {
      try {
        if (Directory.Exists(_tempDirectory)) {
          Directory.Delete(_tempDirectory, true);
        }
      } catch {
        // Bỏ qua lỗi dọn dẹp thư mục tạm của hệ điều hành
      }
    }

    private string GetFilePath(string fileName = "match.sav") {
      return Path.Combine(_tempDirectory, fileName);
    }

    [Test]
    public void LuuRoiDoc_ChoRaTrangThaiGiongHet() {
      string filePath = GetFilePath();

      ShootoutState originalState = default;
      originalState.homeKicksFirst = false;
      originalState.home.Add(KickResult.Scored);
      originalState.home.Add(KickResult.Missed);
      originalState.home.Add(KickResult.Scored);
      originalState.away.Add(KickResult.Missed);
      originalState.away.Add(KickResult.Scored);

      bool saveSuccess = MatchSave.TrySave(originalState, filePath, out string saveError);
      Assert.IsTrue(saveSuccess, $"Lưu file thất bại với lỗi: {saveError}");

      bool loadSuccess = MatchSave.TryLoad(filePath, out ShootoutState loadedState, out string loadError);
      Assert.IsTrue(loadSuccess, $"Đọc file thất bại với lỗi: {loadError}");

      Assert.AreEqual(originalState.homeKicksFirst, loadedState.homeKicksFirst, "Thuộc tính homeKicksFirst không khớp sau khi đọc lại.");
      Assert.AreEqual(originalState.home.Length, loadedState.home.Length, "Số lượt sút đội nhà không khớp.");
      Assert.AreEqual(originalState.away.Length, loadedState.away.Length, "Số lượt sút đội khách không khớp.");

      for (int i = 0; i < originalState.home.Length; i++) {
        Assert.AreEqual(originalState.home[i], loadedState.home[i], $"Kết quả lượt sút thứ {i} của đội nhà không khớp.");
      }

      for (int i = 0; i < originalState.away.Length; i++) {
        Assert.AreEqual(originalState.away[i], loadedState.away[i], $"Kết quả lượt sút thứ {i} của đội khách không khớp.");
      }
    }

    [Test]
    public void DocFileSchemaV1_MacDinhHomeKicksFirstLaTrue() {
      string filePath = GetFilePath("v1_match.sav");

      // Tạo thủ công định dạng file schema phiên bản 1 (không có trường homeKicksFirst)
      string body = "version=1\n" +
                    "timestamp=638400000000000000\n" +
                    "home=1,2,1\n" +
                    "away=2,1,0\n";
      uint checksum = MatchSave.ComputeFnv1a(body);
      string fullContent = body + "checksum=" + checksum.ToString(CultureInfo.InvariantCulture) + "\n";

      File.WriteAllText(filePath, fullContent, Encoding.UTF8);

      bool loadSuccess = MatchSave.TryLoad(filePath, out ShootoutState loadedState, out string loadError);
      Assert.IsTrue(loadSuccess, $"Đọc file schema v1 thất bại với lỗi: {loadError}");

      Assert.IsTrue(loadedState.homeKicksFirst, "File phiên bản 1 khi đọc phải mặc định homeKicksFirst = true.");
      Assert.AreEqual(3, loadedState.home.Length, "Số lượt sút đội nhà ở file v1 phải là 3.");
      Assert.AreEqual(KickResult.Scored, loadedState.home[0], "Lượt 0 đội nhà v1 phải là Scored.");
      Assert.AreEqual(KickResult.Missed, loadedState.home[1], "Lượt 1 đội nhà v1 phải là Missed.");
      Assert.AreEqual(KickResult.Scored, loadedState.home[2], "Lượt 2 đội nhà v1 phải là Scored.");

      Assert.AreEqual(3, loadedState.away.Length, "Số lượt sút đội khách ở file v1 phải là 3.");
      Assert.AreEqual(KickResult.Missed, loadedState.away[0], "Lượt 0 đội khách v1 phải là Missed.");
      Assert.AreEqual(KickResult.Scored, loadedState.away[1], "Lượt 1 đội khách v1 phải là Scored.");
      Assert.AreEqual(KickResult.Pending, loadedState.away[2], "Lượt 2 đội khách v1 phải là Pending.");
    }

    [Test]
    public void TuChoiFile_CoSoPhienBanLonHonHienTai() {
      string filePath = GetFilePath("v999_match.sav");

      string body = "version=999\n" +
                    "timestamp=638400000000000000\n" +
                    "homeKicksFirst=true\n" +
                    "home=1\n" +
                    "away=1\n";
      uint checksum = MatchSave.ComputeFnv1a(body);
      string fullContent = body + "checksum=" + checksum.ToString(CultureInfo.InvariantCulture) + "\n";

      File.WriteAllText(filePath, fullContent, Encoding.UTF8);

      bool loadSuccess = MatchSave.TryLoad(filePath, out ShootoutState loadedState, out string loadError);
      Assert.IsFalse(loadSuccess, "Hệ thống phải từ chối nạp file có phiên bản 999 lớn hơn phiên bản hiện tại.");
      Assert.IsNotNull(loadError, "Phải trả về thông báo lỗi mô tả phiên bản không hợp lệ.");
      Assert.That(loadError, Does.Contain("999").Or.Contain("phiên bản"), "Thông báo lỗi phải đề cập đến số phiên bản.");
    }

    [Test]
    public void GhiDeLenFileDaTonTai_VanThanhCongVaDungDuLieu() {
      string filePath = GetFilePath();

      // Lưu lần 1
      ShootoutState state1 = default;
      state1.homeKicksFirst = true;
      state1.home.Add(KickResult.Scored);

      bool save1 = MatchSave.TrySave(state1, filePath, out string err1);
      Assert.IsTrue(save1, $"Lưu lần 1 thất bại: {err1}");

      // Lưu đè lần 2 với dữ liệu mới
      ShootoutState state2 = default;
      state2.homeKicksFirst = false;
      state2.home.Add(KickResult.Missed);
      state2.home.Add(KickResult.Scored);
      state2.away.Add(KickResult.Scored);

      bool save2 = MatchSave.TrySave(state2, filePath, out string err2);
      Assert.IsTrue(save2, $"Lưu đè lần 2 thất bại: {err2}");

      // Đọc lại và kiểm tra đúng trạng thái lần 2
      bool loadSuccess = MatchSave.TryLoad(filePath, out ShootoutState loadedState, out string loadError);
      Assert.IsTrue(loadSuccess, $"Đọc dữ liệu sau ghi đè thất bại: {loadError}");
      Assert.IsFalse(loadedState.homeKicksFirst, "homeKicksFirst phải là false theo dữ liệu lần 2.");
      Assert.AreEqual(2, loadedState.home.Length, "Số lượt sút đội nhà phải là 2 theo dữ liệu lần 2.");
      Assert.AreEqual(1, loadedState.away.Length, "Số lượt sút đội khách phải là 1 theo dữ liệu lần 2.");
      Assert.AreEqual(KickResult.Missed, loadedState.home[0], "Lượt sút đầu đội nhà phải là Missed theo dữ liệu lần 2.");
    }

    [Test]
    public void SauKhiLuuXong_KhongConFileTmpSotLai() {
      string filePath = GetFilePath();

      ShootoutState state = default;
      state.homeKicksFirst = true;
      state.home.Add(KickResult.Scored);

      bool saveSuccess = MatchSave.TrySave(state, filePath, out string saveError);
      Assert.IsTrue(saveSuccess, $"Lưu file thất bại: {saveError}");

      string[] tmpFiles = Directory.GetFiles(_tempDirectory, "*.tmp");
      Assert.AreEqual(0, tmpFiles.Length, "Không được để lại bất kỳ file .tmp nào trong thư mục sau khi lưu thành công.");
    }

    [Test]
    public void FileBiCatCut_TryLoadTraFalseVaKhongNem() {
      string filePath = GetFilePath();

      ShootoutState state = default;
      state.homeKicksFirst = true;
      state.home.Add(KickResult.Scored);
      state.away.Add(KickResult.Missed);

      bool saveSuccess = MatchSave.TrySave(state, filePath, out string saveError);
      Assert.IsTrue(saveSuccess, $"Lưu file hợp lệ thất bại: {saveError}");

      // Cắt cụt file còn một nửa
      string fullText = File.ReadAllText(filePath, Encoding.UTF8);
      string truncatedText = fullText.Substring(0, fullText.Length / 2);
      File.WriteAllText(filePath, truncatedText, Encoding.UTF8);

      bool loadSuccess = false;
      Assert.DoesNotThrow(() => {
        loadSuccess = MatchSave.TryLoad(filePath, out _, out string loadError);
        Assert.IsNotNull(loadError, "Phải có thông báo lỗi khi file bị cắt cụt.");
      }, "TryLoad không bao giờ được ném Exception khi file bị cắt cụt.");

      Assert.IsFalse(loadSuccess, "TryLoad phải trả về false khi nạp file bị cắt cụt.");
    }

    [Test]
    public void FileToanRacNhiPhan_TryLoadTraFalseVaKhongNem() {
      string filePath = GetFilePath("junk.sav");

      byte[] junkBytes = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF, 0x00, 0xFF, 0x12, 0x34, 0x56, 0x78, 0xAA, 0xBB };
      File.WriteAllBytes(filePath, junkBytes);

      bool loadSuccess = false;
      Assert.DoesNotThrow(() => {
        loadSuccess = MatchSave.TryLoad(filePath, out ShootoutState loadedState, out string loadError);
        Assert.IsNotNull(loadError, "Phải có thông báo lỗi khi file toàn rác nhị phân.");
        Assert.AreEqual(default(ShootoutState).home.Length, loadedState.home.Length, "ShootoutState phải về default khi đọc thất bại.");
      }, "TryLoad không được ném Exception khi đọc file rác nhị phân.");

      Assert.IsFalse(loadSuccess, "TryLoad phải trả về false khi đọc file rác nhị phân.");
    }

    [Test]
    public void SuaMotKyTuTrongPhanThan_ChecksumPhatHienVaTuChoi() {
      string filePath = GetFilePath();

      ShootoutState state = default;
      state.homeKicksFirst = true;
      state.home.Add(KickResult.Scored);
      state.away.Add(KickResult.Missed);

      bool saveSuccess = MatchSave.TrySave(state, filePath, out string saveError);
      Assert.IsTrue(saveSuccess, $"Lưu file ban đầu thất bại: {saveError}");

      // Đổi 1 ký tự trong kết quả sút nhưng giữ nguyên checksum
      string text = File.ReadAllText(filePath, Encoding.UTF8);
      string tamperedText = text.Replace("home=1", "home=2");
      File.WriteAllText(filePath, tamperedText, Encoding.UTF8);

      bool loadSuccess = MatchSave.TryLoad(filePath, out _, out string loadError);
      Assert.IsFalse(loadSuccess, "Phải từ chối file khi nội dung thân file bị chỉnh sửa sai lệch với checksum.");
      Assert.That(loadError, Does.Contain("Checksum").IgnoreCase, "Thông báo lỗi phải đề cập đến việc Checksum không khớp.");
    }

    [Test]
    public void DuongDanThuMucKhongTonTai_TrySaveTraFalseVaKhongNem() {
      string nonExistentPath = Path.Combine(_tempDirectory, "ThuMucKhongTonTai", "match.sav");
      ShootoutState state = default;

      bool saveSuccess = true;
      Assert.DoesNotThrow(() => {
        saveSuccess = MatchSave.TrySave(state, nonExistentPath, out string saveError);
        Assert.IsNotNull(saveError, "Phải có thông báo lỗi khi đường dẫn thư mục không tồn tại.");
      }, "TrySave không được ném Exception khi thư mục đích không tồn tại.");

      Assert.IsFalse(saveSuccess, "TrySave phải trả về false khi thư mục cha không tồn tại.");
    }

    [Test]
    public void TrangThaiRong_LuuVaDocDung() {
      string filePath = GetFilePath("empty_match.sav");
      ShootoutState emptyState = default;

      bool saveSuccess = MatchSave.TrySave(emptyState, filePath, out string saveError);
      Assert.IsTrue(saveSuccess, $"Lưu trạng thái rỗng thất bại: {saveError}");

      bool loadSuccess = MatchSave.TryLoad(filePath, out ShootoutState loadedState, out string loadError);
      Assert.IsTrue(loadSuccess, $"Đọc trạng thái rỗng thất bại: {loadError}");

      Assert.AreEqual(0, loadedState.home.Length, "Số lượt sút đội nhà phải bằng 0.");
      Assert.AreEqual(0, loadedState.away.Length, "Số lượt sút đội khách phải bằng 0.");
      Assert.AreEqual(emptyState.homeKicksFirst, loadedState.homeKicksFirst, "homeKicksFirst của trạng thái rỗng phải khớp.");
    }

    [Test]
    public void TrangThaiDai_DotSuddenDeath20Luot_LuuVaDocDung() {
      string filePath = GetFilePath("long_match.sav");

      ShootoutState longState = default;
      longState.homeKicksFirst = true;

      for (int i = 0; i < 20; i++) {
        longState.home.Add(i % 2 == 0 ? KickResult.Scored : KickResult.Missed);
        longState.away.Add(i % 3 == 0 ? KickResult.Scored : KickResult.Missed);
      }

      bool saveSuccess = MatchSave.TrySave(longState, filePath, out string saveError);
      Assert.IsTrue(saveSuccess, $"Lưu trận đấu 20 lượt mỗi bên thất bại: {saveError}");

      bool loadSuccess = MatchSave.TryLoad(filePath, out ShootoutState loadedState, out string loadError);
      Assert.IsTrue(loadSuccess, $"Đọc trận đấu 20 lượt mỗi bên thất bại: {loadError}");

      Assert.AreEqual(20, loadedState.home.Length, "Đội nhà phải có đủ 20 lượt sút.");
      Assert.AreEqual(20, loadedState.away.Length, "Đội khách phải có đủ 20 lượt sút.");

      for (int i = 0; i < 20; i++) {
        Assert.AreEqual(longState.home[i], loadedState.home[i], $"Lượt sút thứ {i} của đội nhà không khớp.");
        Assert.AreEqual(longState.away[i], loadedState.away[i], $"Lượt sút thứ {i} của đội khách không khớp.");
      }
    }
  }
}
