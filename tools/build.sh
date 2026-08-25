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

# Tìm Unity: ưu tiên UNITY_PATH, sau đó bản 6000.3 mới nhất trong Unity Hub.
UNITY_BIN="${UNITY_PATH:-}"
if [[ -z "$UNITY_BIN" ]]; then
    UNITY_BIN="$(ls -d /Applications/Unity/Hub/Editor/*/Unity.app/Contents/MacOS/Unity 2>/dev/null | sort -V | tail -1 || true)"
fi
if [[ -z "$UNITY_BIN" || ! -x "$UNITY_BIN" ]]; then
    echo "LỖI: Không tìm thấy Unity. Cài qua Unity Hub (bản 6000.3) hoặc đặt UNITY_PATH." >&2
    exit 3
fi
echo "Unity: $UNITY_BIN"

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
