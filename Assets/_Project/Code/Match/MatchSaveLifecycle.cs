using System;
using UnityEngine;

namespace Eleven.Match
{
    /// <summary>
    /// Quản lý vòng đời lưu trữ tiến trình trận sút luân lưu.
    /// Tự động ghi nhận và đồng bộ xuống đĩa khi ứng dụng chuyển nền hoặc bị đóng đột ngột.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MatchSaveLifecycle : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Tự động nạp dữ liệu trận đấu khi GameObject được khởi tạo.")]
        private bool autoLoadOnAwake = true;

        private string customFilePath;
        private bool isDirty;

        /// <summary>
        /// Trạng thái trận đấu luân lưu hiện tại.
        /// </summary>
        public ShootoutState State { get; private set; }

        /// <summary>
        /// Đường dẫn file lưu trữ. Nếu để trống hoặc null, hệ thống sẽ dùng đường dẫn mặc định từ MatchSave.
        /// Không gọi MatchSave.DefaultPath() tại thời điểm khai báo biến để tránh truy cập Application.persistentDataPath ngoài luồng chính hoặc trước khởi tạo.
        /// </summary>
        public string FilePath
        {
            get => string.IsNullOrEmpty(customFilePath) ? MatchSave.DefaultPath() : customFilePath;
            set => customFilePath = value;
        }

        /// <summary>
        /// Cho phép bật/tắt hành vi tự nạp dữ liệu khi Awake (hỗ trợ can thiệp cấu hình trong kiểm thử).
        /// </summary>
        public bool AutoLoadOnAwake
        {
            get => autoLoadOnAwake;
            set => autoLoadOnAwake = value;
        }

        /// <summary>
        /// Số lần đã THỬ ghi đĩa — kể cả lần thất bại. Cố ý đếm lần thử chứ không đếm lần thành
        /// công, vì thứ cần canh là cơ chế chống ghi trùng: một lần chuyển nền chỉ được chạm đĩa
        /// một lần. Nếu chỉ đếm lần thành công thì một bản build ghi đĩa ba lần rồi hỏng hai lần
        /// vẫn cho SaveCount = 1 và test chống trùng vẫn xanh.
        /// </summary>
        public int SaveCount { get; private set; }

        /// <summary>
        /// Sự kiện phát ra sau mỗi lần thử ghi đĩa, thông báo kết quả thành công hay thất bại.
        /// </summary>
        public event Action<bool> OnSaved;

        private void Awake()
        {
            if (!autoLoadOnAwake)
            {
                State = CreateDefaultEmptyState();
                isDirty = false;
                return;
            }

            if (MatchSave.TryLoad(FilePath, out var loadedState, out var error))
            {
                State = loadedState;
                isDirty = false;
            }
            else
            {
                // Khi nạp thất bại (file rác, sai checksum, file chưa tồn tại), cần đưa State về trạng thái trận đấu rỗng hợp lệ
                // thay vì ném exception hoặc giữ dữ liệu nửa vời, đảm bảo gameplay không bị gián đoạn hay crash.
                State = CreateDefaultEmptyState();
                isDirty = false;
                Debug.LogWarning($"[MatchSaveLifecycle] Không thể nạp file lưu từ '{FilePath}': {error}. Trận đấu được khởi tạo lại ở trạng thái rỗng.");
            }
        }

        /// <summary>
        /// Cập nhật trạng thái trận đấu mới từ gameplay và đánh dấu trạng thái cần được lưu trữ.
        /// </summary>
        public void SetState(in ShootoutState s)
        {
            State = s;
            isDirty = true;
        }

        /// <summary>
        /// Ép buộc thực hiện ghi đĩa ngay lập tức bất kể dữ liệu có thay đổi hay không.
        /// </summary>
        public bool SaveNow()
        {
            return ExecuteSave();
        }

        // Bắt buộc xử lý cả 3 sự kiện sau đây để đảm bảo an toàn dữ liệu trên mọi nền tảng:
        // - OnApplicationPause(true): Tín hiệu chuẩn xác và sớm nhất trên Android khi người dùng rời app.
        // - OnApplicationFocus(false): Tín hiệu đáng tin cậy nhất trên iOS khi người dùng vuốt Control Center hoặc chuyển app.
        // - OnApplicationQuit(): Cần thiết trên Standalone/Editor, vì trên di động hệ điều hành thường diệt tiến trình ngầm mà KHÔNG bao giờ gọi hàm này.
        // Khai báo public để Unity engine tự gọi thông qua Message System, đồng thời cho phép test PlayMode kích hoạt trực tiếp mà không cần dùng reflection.
        public void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                SaveIfDirty();
            }
        }

        public void OnApplicationFocus(bool focus)
        {
            if (!focus)
            {
                SaveIfDirty();
            }
        }

        public void OnApplicationQuit()
        {
            SaveIfDirty();
        }

        private void SaveIfDirty()
        {
            // Tránh ghi đĩa nhiều lần liên tiếp khi các sự kiện Pause và Focus-lost cùng kích hoạt trong một lần chuyển nền.
            if (!isDirty)
            {
                return;
            }

            ExecuteSave();
        }

        private bool ExecuteSave()
        {
            // Đọc FilePath đúng MỘT lần: getter của nó có thể gọi MatchSave.DefaultPath(), mà hàm
            // đó chạm Application.persistentDataPath — API chỉ gọi được trên luồng chính và không
            // rẻ. Nhánh báo lỗi bên dưới dùng lại biến này chứ không đọc lại property.
            string duongDan = FilePath;

            SaveCount++;
            var success = MatchSave.TrySave(State, duongDan, out var error);

            if (success)
            {
                // Chỉ xoá cờ dirty khi file đã ghi thành công xuống đĩa. Nếu ghi lỗi, giữ dirty để có cơ hội thử lại lần sau.
                isDirty = false;
            }
            else
            {
                Debug.LogWarning($"[MatchSaveLifecycle] Ghi dữ liệu thất bại tại '{duongDan}': {error}. Tiến trình trận đấu có nguy cơ bị mất nếu ứng dụng bị đóng.");
            }

            OnSaved?.Invoke(success);
            return success;
        }

        private static ShootoutState CreateDefaultEmptyState()
        {
            var s = default(ShootoutState);
            s.homeKicksFirst = true;
            return s;
        }
    }
}
