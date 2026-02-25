using System.Buffers.Binary;

namespace FirmwareKit.Lp;

/// <summary>
/// Provides helper methods for metadata calculations and filesystem detection.
/// </summary>
public static class Utility
{
    /// <summary>
    /// Calculates the total bytes required for all metadata slots and geometry blocks.
    /// </summary>
    /// <param name="metadataMaxSize">The defined maximum metadata size.</param>
    /// <param name="maxSlots">Total number of slots.</param>
    /// <returns>Total size in bytes.</returns>
    public static uint GetTotalMetadataSize(uint metadataMaxSize, uint maxSlots)
    {
        return MetadataFormat.LP_PARTITION_RESERVED_BYTES +
               ((MetadataFormat.LP_METADATA_GEOMETRY_SIZE + (metadataMaxSize * maxSlots)) * 2);
    }

    /// <summary>
    /// Gets the standard Android slot suffix for a given slot index.
    /// </summary>
    /// <param name="slotNumber">The slot index (0 for _a, 1 for _b).</param>
    /// <returns>The slot suffix string.</returns>
    public static string GetSlotSuffix(uint slotNumber) => slotNumber == 0 ? "_a" : "_b";

    /// <summary>
    /// Aligns a value to the next multiple of the specified alignment.
    /// </summary>
    /// <param name="value">The value to align.</param>
    /// <param name="alignment">The alignment boundary.</param>
    /// <returns>The aligned value.</returns>
    public static ulong AlignTo(ulong value, uint alignment)
    {
        if (alignment == 0)
        {
            return value;
        }


        var remainder = value % alignment;
        return remainder == 0 ? value : value + (alignment - remainder);
    }

    /// <summary>
    /// Represents detected filesystem information.
    /// </summary>
    public struct FilesystemInfo
    {
        /// <summary>
        /// The type of filesystem found (e.g. EXT4, EROFS).
        /// </summary>
        public string Type;

        /// <summary>
        /// The size of the filesystem contents in bytes.
        /// </summary>
        public ulong Size;
    }

    /// <summary>
    /// Attempts to detect the filesystem type and size from a raw image stream.
    /// </summary>
    /// <param name="stream">The image stream.</param>
    /// <param name="partitionStartOffset">Absolute byte offset in the stream where the partition resides.</param>
    /// <returns>Detected filesystem details.</returns>
    public static FilesystemInfo DetectFilesystem(Stream stream, ulong partitionStartOffset)
    {
        try
        {
            var buffer = new byte[4096];
            stream.Seek((long)partitionStartOffset, SeekOrigin.Begin);
            if (stream.Read(buffer, 0, buffer.Length) < buffer.Length)
            {
                return new FilesystemInfo { Type = "Unknown", Size = 0 };
            }

            // SquashFS
            if (BitConverter.ToUInt32(buffer, 0) == 0x73717368) // 'hsqs'
            {
                return new FilesystemInfo
                {
                    Type = "SquashFS",
                    Size = BitConverter.ToUInt64(buffer, 40)
                };
            }

            // Superblock based filesystem
            var sb = buffer.AsSpan(1024);

            // EROFS
            if (BinaryPrimitives.ReadUInt32LittleEndian(sb.Slice(0, 4)) == 0xE0F5E1E2)
            {
                var log2_blksz = sb[12];
                var blocks = BinaryPrimitives.ReadUInt32LittleEndian(sb.Slice(44, 4));
                if (log2_blksz == 0)
                {
                    log2_blksz = 12;
                }

                return new FilesystemInfo
                {
                    Type = "EROFS",
                    Size = (ulong)blocks << log2_blksz
                };
            }

            // EXT2/3/4
            if (BinaryPrimitives.ReadUInt16LittleEndian(sb.Slice(0x38, 2)) == 0xEF53)
            {
                return new FilesystemInfo
                {
                    Type = "EXT4",
                    Size = (ulong)BinaryPrimitives.ReadUInt32LittleEndian(sb.Slice(0x4, 4)) * (1024u << (int)BinaryPrimitives.ReadUInt32LittleEndian(sb.Slice(0x18, 4)))
                };
            }

            // F2FS
            if (BinaryPrimitives.ReadUInt32LittleEndian(sb.Slice(0, 4)) == 0xF2F52010)
            {
                return new FilesystemInfo
                {
                    Type = "F2FS",
                    Size = (ulong)BinaryPrimitives.ReadUInt32LittleEndian(sb.Slice(0x48, 4)) * 4096
                };
            }

            // VFAT / FAT32
            if (buffer[510] == 0x55 && buffer[511] == 0xAA)
            {
                return new FilesystemInfo
                {
                    Type = "FAT/MBR",
                    Size = 0
                };
            }
        }
        catch (Exception ex)
        {
            LpLogger.Error($"Error detecting filesystem: {ex.Message}");
        }

        return new FilesystemInfo { Type = "Unknown", Size = 0 };
    }

    /// <summary>
    /// Detects the size of the filesystem if possible.
    /// </summary>
    /// <param name="stream">The source stream.</param>
    /// <param name="partitionStartOffset">Absolute byte offset.</param>
    /// <returns>Size in bytes or 0 if unknown.</returns>
    public static ulong DetectFilesystemSize(Stream stream, ulong partitionStartOffset) => DetectFilesystem(stream, partitionStartOffset).Size;
}
