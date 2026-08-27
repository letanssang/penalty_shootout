#!/usr/bin/env bash
# Eleven Metres — build một lệnh.
#   ./tools/build.sh ios        # build Xcode project (Release, Metal, IL2CPP)
#   ./tools/build.sh android    # build APK (Release, Vulkan, IL2CPP, ARM64)
# Exit code khác 0 khi build hỏng — không bao giờ im lặng.
set -euo pipefail

TARGET="${1:-}"
if [[ "$TARGET" != "ios" && "$TARGET" != "android" ]]; then
    echo "Cách dùng: $0 ios|android [-o outputPath]" >&2
    exit 2
fi
shift || true

OUTPUT=""
while [[ $# -gt 0 ]]; do
    case "$1" in
        -o|--output) OUTPUT="$2"; shift 2 ;;
        *) echo "Tham số lạ: $1" >&2; exit 2 ;;
    esac
done

PROJECT_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$PROJECT_ROOT"

# Tìm Unity: ưu tiên UNITY_PATH, sau đó ĐÚNG bản ghi trong ProjectVersion.txt.
#
# TUYỆT ĐỐI KHÔNG lấy "bản mới nhất đã cài" (`sort -V | tail -1`) như bản đầu tiên của script
# này làm. Sự cố 2026-08-27: máy có thêm 6000.5.9f1, script chọn nó, và Unity lặng lẽ NÂNG CẤP
# cả dự án — URP 17.3→17.5, collections 2.6.8→6.5.0, mathematics 1.3.3→1.4.0, ugui 2.0→2.5,
# gỡ com.unity.modules.vr, thêm modules.physicscore2d, ghi đè manifest.json + packages-lock.json
# và sinh ProjectSettings/PhysicsCoreProjectSettings2D.asset. Không có một dòng cảnh báo nào;
# chỉ lộ ra vì có người đọc `git status`. Một lệnh build không bao giờ được phép đổi phiên bản
# engine của dự án.
UNITY_BIN="${UNITY_PATH:-}"
if [[ -z "$UNITY_BIN" ]]; then
    PROJECT_VERSION="$(sed -n 's/^m_EditorVersion: *//p' ProjectSettings/ProjectVersion.txt 2>/dev/null | head -1 | tr -d '\r')"
    if [[ -z "$PROJECT_VERSION" ]]; then
        echo "LỖI: Không đọc được m_EditorVersion từ ProjectSettings/ProjectVersion.txt." >&2
        exit 3
    fi
    UNITY_BIN="/Applications/Unity/Hub/Editor/$PROJECT_VERSION/Unity.app/Contents/MacOS/Unity"
    if [[ ! -x "$UNITY_BIN" ]]; then
        echo "LỖI: Dự án cần Unity $PROJECT_VERSION nhưng không thấy ở $UNITY_BIN." >&2
        echo "      Cài đúng bản đó qua Unity Hub. Đã cài sẵn những bản này:" >&2
        ls -1 /Applications/Unity/Hub/Editor/ 2>/dev/null | sed 's/^/        /' >&2 || true
        echo "      (Nếu cố ý build bằng bản khác thì đặt UNITY_PATH — và biết rằng nó sẽ" >&2
        echo "       nâng cấp package của dự án.)" >&2
        exit 3
    fi
    echo "Unity: $UNITY_BIN (khớp ProjectVersion.txt = $PROJECT_VERSION)"
else
    echo "Unity: $UNITY_BIN (từ UNITY_PATH — không kiểm tra khớp ProjectVersion.txt)"
fi
if [[ ! -x "$UNITY_BIN" ]]; then
    echo "LỖI: UNITY_PATH trỏ tới thứ không chạy được: $UNITY_BIN" >&2
    exit 3
fi

LOG_FILE="build_${TARGET}_$(date +%Y%m%d_%H%M%S).log"
ARGS=(-batchmode -nographics -quit
      -projectPath "$PROJECT_ROOT"
      -executeMethod Eleven.Editor.BuildPipeline.BuildScript.BuildFromCli
      -buildTarget "$TARGET"
      -logFile "$LOG_FILE")
[[ -n "$OUTPUT" ]] && ARGS+=(-outputPath "$OUTPUT")

echo "Bắt đầu build $TARGET (log: $LOG_FILE)..."
# 'set -e' sẽ thoát ngay khi Unity trả mã khác 0, nên nhánh in log bên dưới không bao giờ chạy.
# '|| EXIT_CODE=$?' giữ lại quyền kiểm soát để còn trích được lỗi ra màn hình.
EXIT_CODE=0
"$UNITY_BIN" "${ARGS[@]}" || EXIT_CODE=$?

if [[ $EXIT_CODE -ne 0 ]]; then
    echo "BUILD THẤT BẠI (exit code $EXIT_CODE). Xem log:" >&2
    grep -E "(error|Error|Exception)" "$LOG_FILE" | head -40 >&2 || true
    exit "$EXIT_CODE"
fi

echo "BUILD OK → xem chi tiết trong $LOG_FILE"
exit 0
