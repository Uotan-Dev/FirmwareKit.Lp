using System.Runtime.InteropServices;
using System.Text;

namespace FirmwareKit.Lp;

/// <summary>
/// A 32-byte fixed-size buffer for binary data (e.g. SHA256 hashes).
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct Buffer32
{
    private fixed byte _element[32];

    /// <summary>
    /// Gets or sets the byte at the specified index.
    /// </summary>
    /// <param name="index">The 0-based index.</param>
    /// <returns>The byte at the index.</returns>
    public byte this[int index]
    {
        readonly get => _element[index];
        set => _element[index] = value;
    }

    /// <summary>
    /// Returns a read-only span over the buffer.
    /// </summary>
    /// <returns>A span of 32 bytes.</returns>
    public readonly ReadOnlySpan<byte> AsSpan()
    {
        fixed (byte* p = _element) return new ReadOnlySpan<byte>(p, 32);
    }
}

/// <summary>
/// A 36-byte fixed-size buffer for binary storage (e.g. partition names).
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct Buffer36
{
    private fixed byte _element[36];

    /// <summary>
    /// Gets or sets the byte at the specified index.
    /// </summary>
    /// <param name="index">The 0-based index.</param>
    /// <returns>The byte at the index.</returns>
    public byte this[int index]
    {
        readonly get => _element[index];
        set => _element[index] = value;
    }

    /// <summary>
    /// Returns a read-only span over the buffer.
    /// </summary>
    /// <returns>A span of 36 bytes.</returns>
    public readonly ReadOnlySpan<byte> AsSpan()
    {
        fixed (byte* p = _element) return new ReadOnlySpan<byte>(p, 36);
    }

    /// <summary>
    /// Sets the buffer from a string using UTF-8 encoding.
    /// </summary>
    /// <param name="name">The name to set.</param>
    public void SetName(string name)
    {
        Span<byte> nameSpan = stackalloc byte[36];
        nameSpan.Clear();
        var written = Encoding.UTF8.GetBytes(name, nameSpan.Slice(0, 35));
        fixed (byte* p = _element)
        {
            var dest = new Span<byte>(p, 36);
            nameSpan.CopyTo(dest);
        }
    }

    /// <summary>
    /// Gets the name from the buffer.
    /// </summary>
    /// <returns>Decoded UTF-8 string.</returns>
    public readonly string GetName()
    {
        fixed (byte* p = _element)
        {
            var span = new ReadOnlySpan<byte>(p, 36);
            var len = 0;
            while (len < span.Length && span[len] != 0) len++;
            return Encoding.UTF8.GetString(span.Slice(0, len));
        }
    }
}

/// <summary>
/// A 124-byte fixed-size buffer for reserved fields.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct Buffer124
{
    private fixed byte _element[124];

    /// <summary>
    /// Gets or sets the byte at the specified index.
    /// </summary>
    /// <param name="index">The 0-based index.</param>
    /// <returns>The byte at the index.</returns>
    public byte this[int index]
    {
        readonly get => _element[index];
        set => _element[index] = value;
    }

    /// <summary>
    /// Returns a read-only span over the buffer.
    /// </summary>
    /// <returns>A span of 124 bytes.</returns>
    public readonly ReadOnlySpan<byte> AsSpan()
    {
        fixed (byte* p = _element) return new ReadOnlySpan<byte>(p, 124);
    }
}

/// <summary>
/// Contains constants and definitions for the Android Logical Partition (LP) binary format.
/// </summary>
public static class MetadataFormat
{
    /// <summary>
    /// Magic number for LpMetadataGeometry.
    /// </summary>
    public const uint LP_METADATA_GEOMETRY_MAGIC = 0x616c4467;

    /// <summary>
    /// Space reserved for geometry information.
    /// </summary>
    public const int LP_METADATA_GEOMETRY_SIZE = 4096;

    /// <summary>
    /// Magic number for LpMetadataHeader.
    /// </summary>
    public const uint LP_METADATA_HEADER_MAGIC = 0x414C5030;

    /// <summary>
    /// Major metadata version.
    /// </summary>
    public const ushort LP_METADATA_MAJOR_VERSION = 10;

    /// <summary>
    /// Minimum minor metadata version supported.
    /// </summary>
    public const ushort LP_METADATA_MINOR_VERSION_MIN = 0;

    /// <summary>
    /// Maximum minor metadata version supported.
    /// </summary>
    public const ushort LP_METADATA_MINOR_VERSION_MAX = 2;

    /// <summary>
    /// Size of a standard disk sector in bytes.
    /// </summary>
    public const int LP_SECTOR_SIZE = 512;

    /// <summary>
    /// Bytes reserved at the beginning of the partition for the partition table.
    /// </summary>
    public const int LP_PARTITION_RESERVED_BYTES = 4096;

    /// <summary>
    /// Default name for the super partition.
    /// </summary>
    public const string LP_METADATA_DEFAULT_PARTITION_NAME = "super";

    /// <summary>
    /// No attributes for a partition.
    /// </summary>
    public const uint LP_PARTITION_ATTR_NONE = 0x0;

    /// <summary>
    /// Partition is read-only.
    /// </summary>
    public const uint LP_PARTITION_ATTR_READONLY = 1 << 0;

    /// <summary>
    /// Partition name is suffixed by the slot (e.g. _a or _b).
    /// </summary>
    public const uint LP_PARTITION_ATTR_SLOT_SUFFIXED = 1 << 1;

    /// <summary>
    /// Partition has been updated.
    /// </summary>
    public const uint LP_PARTITION_ATTR_UPDATED = 1 << 2;

    /// <summary>
    /// Partition is disabled.
    /// </summary>
    public const uint LP_PARTITION_ATTR_DISABLED = 1 << 3;

    /// <summary>
    /// Linear target type for an extent.
    /// </summary>
    public const uint LP_TARGET_TYPE_LINEAR = 0;

    /// <summary>
    /// Zero-fill target type for an extent.
    /// </summary>
    public const uint LP_TARGET_TYPE_ZERO = 1;

    /// <summary>
    /// Group name is suffixed by the slot.
    /// </summary>
    public const uint LP_GROUP_SLOT_SUFFIXED = 1 << 0;

    /// <summary>
    /// Block device name is suffixed by the slot.
    /// </summary>
    public const uint LP_BLOCK_DEVICE_SLOT_SUFFIXED = 1 << 0;

    /// <summary>
    /// Flag for Virtual A/B device.
    /// </summary>
    public const uint LP_HEADER_FLAG_VIRTUAL_AB_DEVICE = 0x1;
}

/// <summary>
/// Describes the global layout of the metadata area on disk.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct LpMetadataGeometry
{
    /// <summary>
    /// Magic number.
    /// </summary>
    public uint Magic;

    /// <summary>
    /// Size of this structure.
    /// </summary>
    public uint StructSize;

    /// <summary>
    /// SHA256 checksum of the geometry block.
    /// </summary>
    public Buffer32 Checksum;

    /// <summary>
    /// Maximum size allocated for metadata blocks.
    /// </summary>
    public uint MetadataMaxSize;

    /// <summary>
    /// Number of metadata slots available (usually 2).
    /// </summary>
    public uint MetadataSlotCount;

    /// <summary>
    /// Logical block size of the underlying device.
    /// </summary>
    public uint LogicalBlockSize;

    /// <summary>
    /// Read geometry from binary data.
    /// </summary>
    /// <param name="data">Input buffer.</param>
    /// <returns>Geometry structure.</returns>
    public static LpMetadataGeometry FromBytes(ReadOnlySpan<byte> data)
    {
        return data.Length < System.Runtime.CompilerServices.Unsafe.SizeOf<LpMetadataGeometry>()
            ? throw new ArgumentException("Data too small for LpMetadataGeometry")
            : MemoryMarshal.Read<LpMetadataGeometry>(data);
    }
}

/// <summary>
/// Describes the location and size of a table within the metadata area.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct LpMetadataTableDescriptor
{
    /// <summary>
    /// Offset relative to the table data start.
    /// </summary>
    public uint Offset;

    /// <summary>
    /// Number of entries in the table.
    /// </summary>
    public uint NumEntries;

    /// <summary>
    /// Size of each entry in bytes.
    /// </summary>
    public uint EntrySize;
}

/// <summary>
/// Header of a metadata block.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct LpMetadataHeader
{
    /// <summary>
    /// Magic number.
    /// </summary>
    public uint Magic;

    /// <summary>
    /// Major version number.
    /// </summary>
    public ushort MajorVersion;

    /// <summary>
    /// Minor version number.
    /// </summary>
    public ushort MinorVersion;

    /// <summary>
    /// Size of the header itself.
    /// </summary>
    public uint HeaderSize;

    /// <summary>
    /// SHA256 checksum of the header.
    /// </summary>
    public Buffer32 HeaderChecksum;

    /// <summary>
    /// Size of all metadata tables linked by this header.
    /// </summary>
    public uint TablesSize;

    /// <summary>
    /// SHA256 checksum of the tables area.
    /// </summary>
    public Buffer32 TablesChecksum;

    /// <summary>
    /// Descriptor for the partitions table.
    /// </summary>
    public LpMetadataTableDescriptor Partitions;

    /// <summary>
    /// Descriptor for the extents table.
    /// </summary>
    public LpMetadataTableDescriptor Extents;

    /// <summary>
    /// Descriptor for the groups table.
    /// </summary>
    public LpMetadataTableDescriptor Groups;

    /// <summary>
    /// Descriptor for the block devices table.
    /// </summary>
    public LpMetadataTableDescriptor BlockDevices;

    /// <summary>
    /// Metadata header flags.
    /// </summary>
    public uint Flags;

    /// <summary>
    /// Reserved space.
    /// </summary>
    public Buffer124 Reserved;

    /// <summary>
    /// Reads header from binary data.
    /// </summary>
    /// <param name="data">Input buffer.</param>
    /// <returns>Header structure.</returns>
    public static LpMetadataHeader FromBytes(ReadOnlySpan<byte> data)
    {
        return data.Length < System.Runtime.CompilerServices.Unsafe.SizeOf<LpMetadataHeader>()
            ? throw new ArgumentException("Data too small for LpMetadataHeader")
            : MemoryMarshal.Read<LpMetadataHeader>(data);
    }
}

/// <summary>
/// Describes a single logical partition.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct LpMetadataPartition
{
    /// <summary>
    /// Partition name (UTF-8).
    /// </summary>
    public Buffer36 Name;

    /// <summary>
    /// Attributes flags.
    /// </summary>
    public uint Attributes;

    /// <summary>
    /// Index of the first extent in the extents table.
    /// </summary>
    public uint FirstExtentIndex;

    /// <summary>
    /// Total number of extents for this partition.
    /// </summary>
    public uint NumExtents;

    /// <summary>
    /// Index of the group this partition belongs to.
    /// </summary>
    public uint GroupIndex;

    /// <summary>
    /// Gets the partition name as a string.
    /// </summary>
    public string NameString => GetName();

    /// <summary>
    /// Gets the partition name as a string.
    /// </summary>
    /// <returns>The decoded name.</returns>
    public string GetName() => Name.GetName();
}

/// <summary>
/// Describes a contiguous segment of blocks.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct LpMetadataExtent
{
    /// <summary>
    /// Total number of sectors for this extent.
    /// </summary>
    public ulong NumSectors;

    /// <summary>
    /// Type of target (0 = Linear, 1 = Zero).
    /// </summary>
    public uint TargetType;

    /// <summary>
    /// Starting sector on the block device.
    /// </summary>
    public ulong TargetData;

    /// <summary>
    /// Index of the source block device.
    /// </summary>
    public uint TargetSource;
}

/// <summary>
/// Describes a partition group that limits the total size of its partitions.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct LpMetadataPartitionGroup
{
    /// <summary>
    /// Group name (UTF-8).
    /// </summary>
    public Buffer36 Name;

    /// <summary>
    /// Group flags.
    /// </summary>
    public uint Flags;

    /// <summary>
    /// Maximum size of all partitions in this group in bytes.
    /// </summary>
    public ulong MaximumSize;

    /// <summary>
    /// Gets the group name as a string.
    /// </summary>
    public string NameString => GetName();

    /// <summary>
    /// Gets the group name as a string.
    /// </summary>
    /// <returns>Decoded name.</returns>
    public string GetName() => Name.GetName();
}

/// <summary>
/// Describes a block device used by the logical partition layout.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct LpMetadataBlockDevice
{
    /// <summary>
    /// First sector that can be used for logical partitions.
    /// </summary>
    public ulong FirstLogicalSector;

    /// <summary>
    /// Alignment requirement in bytes.
    /// </summary>
    public uint Alignment;

    /// <summary>
    /// Offset relative to the first sector to maintain alignment.
    /// </summary>
    public uint AlignmentOffset;

    /// <summary>
    /// Total size of the block device in bytes.
    /// </summary>
    public ulong Size;

    /// <summary>
    /// Name of the partition backing this block device.
    /// </summary>
    public Buffer36 PartitionName;

    /// <summary>
    /// Device flags.
    /// </summary>
    public uint Flags;

    /// <summary>
    /// Gets the partition name backing this block device.
    /// </summary>
    public string PartitionNameString => GetPartitionName();

    /// <summary>
    /// Gets the partition name backing this block device.
    /// </summary>
    /// <returns>Decoded name.</returns>
    public string GetPartitionName() => PartitionName.GetName();
}
