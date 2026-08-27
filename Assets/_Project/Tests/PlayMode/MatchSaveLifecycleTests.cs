using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Eleven.Match;

namespace Eleven.Tests.PlayMode
{
    [TestFixture]
    public sealed class MatchSaveLifecycleTests
    {
        private string tempFilePath;
        private GameObject testGameObject;
        private MatchSaveLifecycle lifecycle;

        [SetUp]
        public void SetUp()
        {
            // Sử dụng file tạm trong thư mục cache để cách ly hoàn toàn, không đụng vào dữ liệu thật của người dùng.
            tempFilePath = Path.Combine(Application.temporaryCachePath, $"test_match_save_{Guid.NewGuid():N}.json");

            // Khởi tạo GameObject ở trạng thái inactive để gán cấu hình file tạm trước khi Awake được Unity kích hoạt.
            testGameObject = new GameObject("Test_MatchSaveLifecycle");
            testGameObject.SetActive(false);

            lifecycle = testGameObject.AddComponent<MatchSaveLifecycle>();
            lifecycle.FilePath = tempFilePath;
            lifecycle.AutoLoadOnAwake = false;

            testGameObject.SetActive(true);
        }

        [TearDown]
        public void TearDown()
        {
            // LogAssert.ignoreFailingMessages là cờ TĨNH, sống xuyên suốt cả lượt chạy test.
            // Bản nhận về bật nó lên trong FileHong_VeTranRong_KhongCrash mà không bao giờ tắt —
            // nghĩa là MỌI test chạy sau đó (BallDriverTests, DeviceAcceptanceTests…) sẽ âm thầm
            // bỏ qua mọi log lỗi. Một test đáng lẽ phải đỏ vì Unity log Error sẽ xanh, và không ai
            // biết. Trả nó về false ở đây, sau MỌI test, không riêng test bật nó.
            LogAssert.ignoreFailingMessages = false;

            if (testGameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(testGameObject);
            }

            XoaFileTam(tempFilePath);
        }

        /// <summary>
        /// Xoá cả file đích lẫn file `.tmp` mà cơ chế ghi nguyên tử của MatchSave dựng ra. Nếu một
        /// lần ghi hỏng giữa chừng, `.tmp` sẽ nằm lại trong temporaryCachePath và không ai dọn.
        /// </summary>
        static void XoaFileTam(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(path + ".tmp")) File.Delete(path + ".tmp");
        }

        [Test]
        public void PauseTrue_ThiLuu()
        {
            var state = default(ShootoutState);
            state.homeKicksFirst = true;
            lifecycle.SetState(state);

            lifecycle.OnApplicationPause(true);

            Assert.That(lifecycle.SaveCount, Is.EqualTo(1), "Khi ứng dụng bị tạm dừng (pause = true), hệ thống bắt buộc phải lưu tiến trình xuống đĩa để tránh mất dữ liệu người dùng.");
            Assert.That(File.Exists(tempFilePath), Is.True, "File lưu thực tế phải tồn tại trên ổ đĩa sau khi sự kiện pause = true diễn ra.");
        }

        [Test]
        public void PauseFalse_KhongLuu()
        {
            var state = default(ShootoutState);
            state.homeKicksFirst = true;
            lifecycle.SetState(state);

            lifecycle.OnApplicationPause(false);

            Assert.That(lifecycle.SaveCount, Is.EqualTo(0), "Khi ứng dụng được mở lại (pause = false), không được phép ghi đĩa gây lãng phí tài nguyên I/O.");
            Assert.That(File.Exists(tempFilePath), Is.False, "Không được tự ý tạo file lưu khi người chơi chỉ vừa quay lại màn hình game.");
        }

        [Test]
        public void FocusMat_ThiLuu()
        {
            var state = default(ShootoutState);
            state.homeKicksFirst = true;
            lifecycle.SetState(state);

            lifecycle.OnApplicationFocus(false);

            Assert.That(lifecycle.SaveCount, Is.EqualTo(1), "Khi ứng dụng mất tiêu điểm (focus = false), phải lưu ngay lập tức vì hệ điều hành có thể hủy tiến trình bất cứ lúc nào.");
            Assert.That(File.Exists(tempFilePath), Is.True, "File lưu phải tồn tại trên đĩa sau khi sự kiện focus = false được kích hoạt.");
        }

        [Test]
        public void PauseRoiFocusMat_ChiLuuMotLan()
        {
            var state = default(ShootoutState);
            state.homeKicksFirst = true;
            lifecycle.SetState(state);

            // Giả lập hệ điều hành bắn liên tiếp hai sự kiện khi ứng dụng chuyển nền
            lifecycle.OnApplicationPause(true);
            lifecycle.OnApplicationFocus(false);

            Assert.That(lifecycle.SaveCount, Is.EqualTo(1), "Cơ chế chống ghi trùng (dirty flag) phải chặn đợt lưu thứ hai khi dữ liệu chưa có bất kỳ thay đổi nào mới.");
        }

        [Test]
        public void SetStateGiuaChung_ThiLuuLanNua()
        {
            var state1 = default(ShootoutState);
            state1.homeKicksFirst = true;
            lifecycle.SetState(state1);
            lifecycle.OnApplicationPause(true);

            // Giả lập trạng thái trận đấu tiếp tục diễn ra lượt sút mới
            var state2 = ShootoutRules.ApplyKick(state1, KickResult.Scored);
            lifecycle.SetState(state2);
            lifecycle.OnApplicationPause(true);

            Assert.That(lifecycle.SaveCount, Is.EqualTo(2), "Khi trạng thái trận đấu được cập nhật mới giữa các lần tạm dừng, hệ thống phải thực hiện lưu lại đầy đủ.");
        }

        [Test]
        public void KhongCoGiThayDoi_KhongLuu()
        {
            // Không gọi SetState, giữ nguyên đối tượng khởi tạo ban đầu
            lifecycle.OnApplicationPause(true);
            lifecycle.OnApplicationFocus(false);
            lifecycle.OnApplicationQuit();

            Assert.That(lifecycle.SaveCount, Is.EqualTo(0), "Nếu chưa có trạng thái trận đấu nào được nạp hoặc thay đổi, không được phép thực hiện ghi đĩa tạo rác.");
            Assert.That(File.Exists(tempFilePath), Is.False, "Không được sinh ra file lưu rỗng khi dữ liệu chưa từng được cập nhật.");
        }

        [Test]
        public void LuuXongDocLai_DungTrangThai()
        {
            // Dựng trạng thái trận đấu chuẩn mực thông qua luật ShootoutRules
            var originalState = default(ShootoutState);
            originalState.homeKicksFirst = true;
            originalState = ShootoutRules.ApplyKick(originalState, KickResult.Scored);
            originalState = ShootoutRules.ApplyKick(originalState, KickResult.Missed);
            originalState = ShootoutRules.ApplyKick(originalState, KickResult.Scored);

            lifecycle.SetState(originalState);
            var saved = lifecycle.SaveNow();
            Assert.That(saved, Is.True, "Lệnh SaveNow phải thực hiện ghi đĩa thành công.");

            // Tạo component thứ hai để đọc lại file vừa lưu nhằm kiểm chứng tính toàn vẹn dữ liệu
            var secondGameObject = new GameObject("Test_SecondLifecycle");
            secondGameObject.SetActive(false);

            var secondLifecycle = secondGameObject.AddComponent<MatchSaveLifecycle>();
            secondLifecycle.FilePath = tempFilePath;
            secondLifecycle.AutoLoadOnAwake = true;

            secondGameObject.SetActive(true);

            try
            {
                var loadedState = secondLifecycle.State;
                Assert.That(loadedState.homeKicksFirst, Is.EqualTo(originalState.homeKicksFirst), "Thuộc tính đội sút trước (homeKicksFirst) nạp lại không trùng khớp với dữ liệu gốc.");
                Assert.That(loadedState.home.Length, Is.EqualTo(originalState.home.Length), "Số lượt sút của đội nhà khi nạp lại không trùng khớp với dữ liệu gốc.");
                Assert.That(loadedState.away.Length, Is.EqualTo(originalState.away.Length), "Số lượt sút của đội khách khi nạp lại không trùng khớp với dữ liệu gốc.");

                for (int i = 0; i < originalState.home.Length; i++)
                {
                    Assert.That(loadedState.home[i], Is.EqualTo(originalState.home[i]), $"Kết quả sút của đội nhà tại lượt {i + 1} bị sai lệch sau khi nạp.");
                }

                for (int i = 0; i < originalState.away.Length; i++)
                {
                    Assert.That(loadedState.away[i], Is.EqualTo(originalState.away[i]), $"Kết quả sút của đội khách tại lượt {i + 1} bị sai lệch sau khi nạp.");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(secondGameObject);
            }
        }

        [Test]
        public void FileHong_VeTranRong_KhongCrash()
        {
            // Cố ý ghi nội dung sai cấu trúc vào file lưu
            File.WriteAllText(tempFilePath, "kh0ng ph@i file luu hop le !@#$%^&*");

            // Khẳng định cảnh báo ĐÚNG LÀ có, thay vì bịt tai bằng ignoreFailingMessages. Khác biệt
            // quan trọng: Expect vừa làm cảnh báo không đánh đỏ test, vừa BẮT BUỘC nó phải xuất
            // hiện. Nếu ai đó bỏ dòng LogWarning đi, người chơi sẽ mất sạch tiến trình mà không có
            // một dòng log nào giải thích — và test này phải là thứ chặn lại.
            LogAssert.Expect(LogType.Warning, new Regex(@"\[MatchSaveLifecycle\].*Không thể nạp file lưu"));

            var corruptTestGameObject = new GameObject("Test_CorruptLifecycle");
            corruptTestGameObject.SetActive(false);

            var corruptLifecycle = corruptTestGameObject.AddComponent<MatchSaveLifecycle>();
            corruptLifecycle.FilePath = tempFilePath;
            corruptLifecycle.AutoLoadOnAwake = true;

            Assert.DoesNotThrow(() => corruptTestGameObject.SetActive(true), "Khởi tạo nạp file lưu hỏng không được phép ném Exception gây crash ứng dụng.");

            try
            {
                var state = corruptLifecycle.State;
                Assert.That(state.home.Length, Is.EqualTo(0), "Khi file lưu bị hỏng, danh sách lượt sút của đội nhà phải rỗng để bắt đầu lại trận mới an toàn.");
                Assert.That(state.away.Length, Is.EqualTo(0), "Khi file lưu bị hỏng, danh sách lượt sút của đội khách phải rỗng để bắt đầu lại trận mới an toàn.");
                Assert.That(state.homeKicksFirst, Is.True, "Khi file lưu bị hỏng, lượt sút đầu tiên phải được đặt lại mặc định cho đội nhà.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(corruptTestGameObject);
            }
        }

        [Test]
        public void Thoat_ThiLuu()
        {
            // OnApplicationQuit chỉ được gọi ở nhánh "không có gì thay đổi" trong bản nhận về, nên
            // chưa có test nào chứng minh nó THẬT SỰ lưu. Trên Standalone/Editor đây lại là tín
            // hiệu duy nhất, nên để hở là mất trắng tiến trình khi thoát từ máy tính.
            var state = default(ShootoutState);
            state.homeKicksFirst = true;
            lifecycle.SetState(state);

            lifecycle.OnApplicationQuit();

            Assert.That(lifecycle.SaveCount, Is.EqualTo(1), "Thoát app phải lưu — trên Standalone đây là tín hiệu duy nhất nhận được.");
            Assert.That(File.Exists(tempFilePath), Is.True, "File lưu phải có mặt trên đĩa sau khi thoát app.");
        }

        [Test]
        public void GhiThatBai_GiuNguyenDirty_DeConCoHoiThuLai()
        {
            // Component cố ý CHỈ xoá cờ dirty khi ghi thành công. Đó là quyết định đúng nhưng chưa
            // ai canh: nếu sau này có người "dọn code" thành xoá dirty vô điều kiện, một lần ghi
            // hỏng (đĩa đầy, mất quyền) sẽ nuốt luôn cơ hội lưu ở lần chuyển nền kế tiếp, và người
            // chơi mất tiến trình mà không có triệu chứng gì.
            string duongDanHong = Path.Combine(Application.temporaryCachePath,
                                               "khong_ton_tai_" + Guid.NewGuid().ToString("N"),
                                               "match.sav");
            lifecycle.FilePath = duongDanHong;

            var state = default(ShootoutState);
            state.homeKicksFirst = true;
            lifecycle.SetState(state);

            bool? ketQuaBaoVe = null;
            lifecycle.OnSaved += ok => ketQuaBaoVe = ok;

            LogAssert.Expect(LogType.Warning, new Regex(@"\[MatchSaveLifecycle\].*Ghi dữ liệu thất bại"));
            lifecycle.OnApplicationPause(true);

            Assert.That(lifecycle.SaveCount, Is.EqualTo(1), "Lần ghi hỏng vẫn phải tính là một lần THỬ ghi.");
            Assert.That(ketQuaBaoVe, Is.EqualTo(false), "OnSaved phải báo false khi ghi hỏng, không được im lặng.");

            // Vẫn còn dirty → lần chuyển nền sau vẫn thử ghi lần nữa.
            lifecycle.FilePath = tempFilePath;
            lifecycle.OnApplicationPause(true);

            Assert.That(lifecycle.SaveCount, Is.EqualTo(2), "Ghi hỏng phải GIỮ cờ dirty để lần chuyển nền sau còn thử lại; SaveCount đứng yên nghĩa là cờ đã bị xoá oan.");
            Assert.That(File.Exists(tempFilePath), Is.True, "Lần thử lại phải ghi được thật sự xuống đĩa.");
        }

        [Test]
        public void OnSaved_BanDungKetQua()
        {
            var callbackTriggeredCount = 0;
            var saveSuccessResult = false;

            lifecycle.OnSaved += success =>
            {
                callbackTriggeredCount++;
                saveSuccessResult = success;
            };

            var state = default(ShootoutState);
            state.homeKicksFirst = true;
            lifecycle.SetState(state);

            lifecycle.OnApplicationPause(true);

            Assert.That(callbackTriggeredCount, Is.EqualTo(1), "Sự kiện OnSaved phải được bắn đúng 1 lần duy nhất sau khi tiến trình lưu kết thúc.");
            Assert.That(saveSuccessResult, Is.True, "Sự kiện OnSaved phải truyền kết quả true khi dữ liệu được ghi đĩa thành công.");
        }
    }
}
