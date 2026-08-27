using System;
using System.IO;
using Unity.Mathematics;
using Eleven.Ball;
using Eleven.Shooter;
using Eleven.Match;

namespace Eleven.Presentation
{
    /// <summary>
    /// Cấu trúc nhị phân nén siêu gọn lưu trữ toàn bộ dữ liệu của một lượt sút luân lưu.
    /// Kích thước dữ liệu &lt; 256 bytes (thực tế ~64 bytes), lưu trữ seed và input để phát lại tất định.
    /// </summary>
    public struct ReplayKickData
    {
        public const uint MagicHeader = 0x52504C59; // "RPLY" trong ASCII
        public const byte CurrentVersion = 1;
        public const int MaxAllowedPayloadBytes = 256;

        public uint seed;
        public ShotIntent intent;
        public ShotOutcome expectedOutcome;
        public float3 expectedCrossing;
        public int expectedCell;

        /// <summary>
        /// Tính mã băm FNV-1a 32-bit trên toàn bộ dữ liệu nội dung để phát hiện sai lệch hoặc sửa đổi thủ công.
        /// </summary>
        public uint ComputeChecksum()
        {
            uint hash = 2166136261u; // FNV offset basis
            const uint prime = 16777619u;

            void HashInt(int val)
            {
                unchecked
                {
                    hash ^= (byte)(val & 0xFF); hash *= prime;
                    hash ^= (byte)((val >> 8) & 0xFF); hash *= prime;
                    hash ^= (byte)((val >> 16) & 0xFF); hash *= prime;
                    hash ^= (byte)((val >> 24) & 0xFF); hash *= prime;
                }
            }

            void HashFloat(float val)
            {
                int intVal = BitConverter.SingleToInt32Bits(val);
                HashInt(intVal);
            }

            HashInt((int)seed);
            HashFloat(intent.aimPoint.x);
            HashFloat(intent.aimPoint.y);
            HashFloat(intent.aimPoint.z);
            HashFloat(intent.spin.x);
            HashFloat(intent.spin.y);
            HashFloat(intent.spin.z);
            HashFloat(intent.speed);
            HashInt((int)intent.type);
            HashFloat(intent.quality);
            HashInt(intent.unstable ? 1 : 0);
            HashFloat(intent.scatterRadius);
            HashInt((int)expectedOutcome);
            HashFloat(expectedCrossing.x);
            HashFloat(expectedCrossing.y);
            HashFloat(expectedCrossing.z);
            HashInt(expectedCell);

            return hash;
        }

        /// <summary>
        /// Đóng gói dữ liệu lượt sút thành mảng byte nhị phân.
        /// </summary>
        public byte[] ToBytes()
        {
            using (var ms = new MemoryStream(MaxAllowedPayloadBytes))
            using (var writer = new BinaryWriter(ms))
            {
                writer.Write(MagicHeader);
                writer.Write(CurrentVersion);
                writer.Write(seed);

                // ShotIntent
                writer.Write(intent.aimPoint.x);
                writer.Write(intent.aimPoint.y);
                writer.Write(intent.aimPoint.z);
                writer.Write(intent.spin.x);
                writer.Write(intent.spin.y);
                writer.Write(intent.spin.z);
                writer.Write(intent.speed);
                writer.Write((byte)intent.type);
                writer.Write(intent.quality);
                writer.Write(intent.unstable);
                writer.Write(intent.scatterRadius);

                // Expected result
                writer.Write((byte)expectedOutcome);
                writer.Write(expectedCrossing.x);
                writer.Write(expectedCrossing.y);
                writer.Write(expectedCrossing.z);
                writer.Write(expectedCell);

                // Checksum
                writer.Write(ComputeChecksum());

                return ms.ToArray();
            }
        }

        /// <summary>
        /// Giải mã mảng byte nhị phân thành cấu trúc ReplayKickData, tự động kiểm tra toàn vẹn.
        /// </summary>
        public static bool TryFromBytes(byte[] bytes, out ReplayKickData data, out string error)
        {
            data = default;
            error = null;

            if (bytes == null || bytes.Length == 0)
            {
                error = "Dữ liệu replay rỗng hoặc null.";
                return false;
            }

            if (bytes.Length > MaxAllowedPayloadBytes)
            {
                error = $"Kích thước payload ({bytes.Length} bytes) vượt quá giới hạn ngân sách {MaxAllowedPayloadBytes} bytes.";
                return false;
            }

            try
            {
                using (var ms = new MemoryStream(bytes))
                using (var reader = new BinaryReader(ms))
                {
                    uint magic = reader.ReadUInt32();
                    if (magic != MagicHeader)
                    {
                        error = $"Magic header không hợp lệ: 0x{magic:X8}, mong đợi 0x{MagicHeader:X8}.";
                        return false;
                    }

                    byte version = reader.ReadByte();
                    if (version > CurrentVersion)
                    {
                        error = $"Phiên bản replay {version} mới hơn phiên bản hỗ trợ ({CurrentVersion}).";
                        return false;
                    }

                    data.seed = reader.ReadUInt32();

                    // ShotIntent
                    data.intent.aimPoint = new float3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                    data.intent.spin = new float3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                    data.intent.speed = reader.ReadSingle();
                    data.intent.type = (ShotType)reader.ReadByte();
                    data.intent.quality = reader.ReadSingle();
                    data.intent.unstable = reader.ReadBoolean();
                    data.intent.scatterRadius = reader.ReadSingle();

                    // Expected results
                    data.expectedOutcome = (ShotOutcome)reader.ReadByte();
                    data.expectedCrossing = new float3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                    data.expectedCell = reader.ReadInt32();

                    uint recordedChecksum = reader.ReadUInt32();
                    uint computedChecksum = data.ComputeChecksum();

                    if (recordedChecksum != computedChecksum)
                    {
                        error = $"Lỗi kiểm tra toàn vẹn (Checksum mismatch): ghi nhận 0x{recordedChecksum:X8}, tính toán 0x{computedChecksum:X8}.";
                        return false;
                    }

                    return true;
                }
            }
            catch (Exception ex)
            {
                error = $"Lỗi phân tích cú pháp dữ liệu replay: {ex.Message}";
                return false;
            }
        }
    }
}
