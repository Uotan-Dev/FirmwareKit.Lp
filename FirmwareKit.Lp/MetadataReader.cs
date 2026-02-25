using System.Runtime.InteropServices;

namespace FirmwareKit.Lp;

/// <summary>
/// Provides methods to read Android Logical Partition (LP) metadata from image files or streams.
/// </summary>
public class MetadataReader : ILpMetadataReader
{
    private static readonly MetadataReader _default = new();

    /// <summary>
    /// Gets a default instance of the metadata reader.
    /// </summary>
    public static MetadataReader Default => _default;
    /// <summary>
    /// Reads LP metadata from a file at the specified path.
    /// </summary>
    /// <param name="path">The absolute path to the image file.</param>
    /// <returns>The parsed <see cref="LpMetadata"/>.</returns>
    public LpMetadata ReadFromImageFile(string path)
    {
        using var stream = File.OpenRead(path);
        return ReadFromImageStream(stream);
    }

    /// <summary>
    /// Reads LP metadata from a file at the specified path asynchronously.
    /// </summary>
    /// <param name="path">The absolute path to the image file.</param>
    /// <returns>A task that represents the asynchronous read operation, wrapping the parsed <see cref="LpMetadata"/>.</returns>
    public async Task<LpMetadata> ReadFromImageFileAsync(string path)
    {
#if NETSTANDARD2_0
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
#else
        using var stream = File.OpenRead(path);
#endif
        return await ReadFromImageStreamAsync(stream).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads LP metadata from a stream.
    /// </summary>
    /// <param name="stream">The source stream to read from.</param>
    /// <returns>The parsed <see cref="LpMetadata"/>.</returns>
    /// <exception cref="InvalidDataException">Thrown if valid geometry cannot be found.</exception>
    public LpMetadata ReadFromImageStream(Stream stream)
    {
        long[] tryOffsets = [ MetadataFormat.LP_PARTITION_RESERVED_BYTES,
                              MetadataFormat.LP_PARTITION_RESERVED_BYTES + MetadataFormat.LP_METADATA_GEOMETRY_SIZE,
                              0 ];

        foreach (var offset in tryOffsets)
        {
            try
            {
                LpLogger.Info($"Attempting to read geometry at offset {offset}...");
                var buffer = new byte[MetadataFormat.LP_METADATA_GEOMETRY_SIZE];
                stream.Seek(offset, SeekOrigin.Begin);
                if (stream.Read(buffer, 0, buffer.Length) == buffer.Length)
                {
                    ParseGeometry(buffer, out var geometry);
                    var metadataOffset = offset;
                    if (offset == MetadataFormat.LP_PARTITION_RESERVED_BYTES + MetadataFormat.LP_METADATA_GEOMETRY_SIZE)
                    {
                        metadataOffset -= MetadataFormat.LP_METADATA_GEOMETRY_SIZE;
                    }

                    stream.Seek(metadataOffset + (MetadataFormat.LP_METADATA_GEOMETRY_SIZE * 2), SeekOrigin.Begin);
                    var metadata = ParseMetadata(geometry, stream);
                    LpLogger.Info($"Successfully parsed metadata: Partitions={metadata.Partitions.Count}, Groups={metadata.Groups.Count}");
                    return metadata;
                }
            }
            catch (Exception ex)
            {
                LpLogger.Warning($"Failed to parse at offset {offset}: {ex.Message}");
                continue;
            }
        }

        throw new InvalidDataException("Could not find a valid LpMetadataGeometry. The image might not be a super image or is corrupted.");
    }

    /// <summary>
    /// Reads LP metadata from a stream asynchronously.
    /// </summary>
    /// <param name="stream">The source stream to read from.</param>
    /// <returns>A task that represents the asynchronous read operation, wrapping the parsed <see cref="LpMetadata"/>.</returns>
    /// <exception cref="InvalidDataException">Thrown if valid geometry cannot be found.</exception>
    public async Task<LpMetadata> ReadFromImageStreamAsync(Stream stream)
    {
        long[] tryOffsets = [ MetadataFormat.LP_PARTITION_RESERVED_BYTES,
                              MetadataFormat.LP_PARTITION_RESERVED_BYTES + MetadataFormat.LP_METADATA_GEOMETRY_SIZE,
                              0 ];

        foreach (var offset in tryOffsets)
        {
            try
            {
                LpLogger.Info($"Attempting to read geometry at offset {offset} (Async)...");
                var buffer = new byte[MetadataFormat.LP_METADATA_GEOMETRY_SIZE];
                stream.Seek(offset, SeekOrigin.Begin);
                if (await stream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false) == buffer.Length)
                {
                    ParseGeometry(buffer, out var geometry);
                    var metadataOffset = offset;
                    if (offset == MetadataFormat.LP_PARTITION_RESERVED_BYTES + MetadataFormat.LP_METADATA_GEOMETRY_SIZE)
                    {
                        metadataOffset -= MetadataFormat.LP_METADATA_GEOMETRY_SIZE;
                    }

                    stream.Seek(metadataOffset + (MetadataFormat.LP_METADATA_GEOMETRY_SIZE * 2), SeekOrigin.Begin);
                    var metadata = await ParseMetadataAsync(geometry, stream).ConfigureAwait(false);
                    LpLogger.Info($"Successfully parsed metadata: Partitions={metadata.Partitions.Count}, Groups={metadata.Groups.Count}");
                    return metadata;
                }
            }
            catch (Exception ex)
            {
                LpLogger.Warning($"Failed to parse at offset {offset}: {ex.Message}");
                continue;
            }
        }

        throw new InvalidDataException("Could not find a valid LpMetadataGeometry. The image might not be a super image or is corrupted.");
    }

    /// <summary>
    /// Parses the <see cref="LpMetadataGeometry"/> from a raw buffer.
    /// </summary>
    /// <param name="buffer">The buffer containing geometry data.</param>
    /// <param name="geometry">The parsed geometry structure.</param>
    /// <exception cref="ArgumentException">Thrown if buffer is too small.</exception>
    /// <exception cref="InvalidDataException">Thrown if magic number or checksum mismatch occurs.</exception>
    public static void ParseGeometry(ReadOnlySpan<byte> buffer, out LpMetadataGeometry geometry)
    {
        geometry = default;
        if (buffer.Length < System.Runtime.CompilerServices.Unsafe.SizeOf<LpMetadataGeometry>())
        {
            throw new ArgumentException("Data length is insufficient to parse LpMetadataGeometry.");
        }

        geometry = LpMetadataGeometry.FromBytes(buffer);

        if (geometry.Magic != MetadataFormat.LP_METADATA_GEOMETRY_MAGIC)
        {
            throw new InvalidDataException($"Invalid LpMetadataGeometry magic: 0x{geometry.Magic:X8} (expected: 0x{MetadataFormat.LP_METADATA_GEOMETRY_MAGIC:X8})");
        }

        if (geometry.StructSize > (uint)buffer.Length)
        {
            throw new InvalidDataException($"LpMetadataGeometry struct size exceeds buffer: {geometry.StructSize} > {buffer.Length}");
        }

        ReadOnlySpan<byte> originalChecksum = geometry.Checksum.AsSpan();

        var tempBuffer = buffer.Slice(0, (int)geometry.StructSize).ToArray();
        for (var i = 0; i < 32; i++)
        {
            tempBuffer[8 + i] = 0;
        }

        var computed = CompatibilityExtensions.ComputeSha256(tempBuffer);
        for (var i = 0; i < 32; i++)
        {
            if (computed[i] != originalChecksum[i])
            {
                throw new InvalidDataException("LpMetadataGeometry checksum mismatch.");
            }
        }
    }

    /// <summary>
    /// Parses primary metadata from a stream based on the provided geometry.
    /// </summary>
    /// <param name="geometry">The source geometry metadata.</param>
    /// <param name="stream">The source stream where metadata resides.</param>
    /// <returns>The parsed <see cref="LpMetadata"/>.</returns>
    /// <exception cref="InvalidDataException">Thrown if header or tables fail validation.</exception>
    public static LpMetadata ParseMetadata(LpMetadataGeometry geometry, Stream stream)
    {
        var headerBuffer = new byte[System.Runtime.CompilerServices.Unsafe.SizeOf<LpMetadataHeader>()];
        if (stream.Read(headerBuffer, 0, headerBuffer.Length) != headerBuffer.Length)
        {
            throw new InvalidDataException("Could not read LpMetadataHeader.");
        }

        var header = LpMetadataHeader.FromBytes(headerBuffer);
        if (header.Magic != MetadataFormat.LP_METADATA_HEADER_MAGIC)
        {
            throw new InvalidDataException("Invalid LpMetadataHeader magic.");
        }

        ReadOnlySpan<byte> originalHeaderChecksum = header.HeaderChecksum.AsSpan();

        var headerCopy = (byte[])headerBuffer.Clone();
        for (var i = 0; i < 32; i++)
        {
            headerCopy[12 + i] = 0;
        }

        var computedHeader = CompatibilityExtensions.ComputeSha256(headerCopy, 0, (int)header.HeaderSize);
        for (var i = 0; i < 32; i++)
        {
            if (computedHeader[i] != originalHeaderChecksum[i])
            {
                throw new InvalidDataException("LpMetadataHeader checksum mismatch.");
            }
        }

        var tablesBuffer = new byte[header.TablesSize];
        if (stream.Read(tablesBuffer, 0, tablesBuffer.Length) != tablesBuffer.Length)
        {
            throw new InvalidDataException("Could not read metadata tables.");
        }

        ReadOnlySpan<byte> originalTablesChecksum = header.TablesChecksum.AsSpan();

        var computedTables = CompatibilityExtensions.ComputeSha256(tablesBuffer);
        for (var i = 0; i < 32; i++)
        {
            if (computedTables[i] != originalTablesChecksum[i])
            {
                throw new InvalidDataException("Metadata tables checksum mismatch.");
            }
        }

        var metadata = new LpMetadata
        {
            Geometry = geometry,
            Header = header
        };

        ParseTable(tablesBuffer, header.Partitions, metadata.Partitions);
        ParseTable(tablesBuffer, header.Extents, metadata.Extents);
        ParseTable(tablesBuffer, header.Groups, metadata.Groups);
        ParseTable(tablesBuffer, header.BlockDevices, metadata.BlockDevices);

        return metadata;
    }

    /// <summary>
    /// Parses primary metadata from a stream asynchronously.
    /// </summary>
    /// <param name="geometry">The source geometry metadata.</param>
    /// <param name="stream">The source stream where metadata resides.</param>
    /// <returns>A task that represents the asynchronous parse operation, wrapping the parsed <see cref="LpMetadata"/>.</returns>
    /// <exception cref="InvalidDataException">Thrown if header or tables fail validation.</exception>
    public static async Task<LpMetadata> ParseMetadataAsync(LpMetadataGeometry geometry, Stream stream)
    {
        var headerBuffer = new byte[System.Runtime.CompilerServices.Unsafe.SizeOf<LpMetadataHeader>()];
        if (await stream.ReadAsync(headerBuffer, 0, headerBuffer.Length).ConfigureAwait(false) != headerBuffer.Length)
        {
            throw new InvalidDataException("Could not read LpMetadataHeader.");
        }

        var header = LpMetadataHeader.FromBytes(headerBuffer);
        if (header.Magic != MetadataFormat.LP_METADATA_HEADER_MAGIC)
        {
            throw new InvalidDataException("Invalid LpMetadataHeader magic.");
        }

        ReadOnlySpan<byte> originalHeaderChecksum = header.HeaderChecksum.AsSpan();

        var headerCopy = (byte[])headerBuffer.Clone();
        for (var i = 0; i < 32; i++)
        {
            headerCopy[12 + i] = 0;
        }

        var computedHeader = CompatibilityExtensions.ComputeSha256(headerCopy, 0, (int)header.HeaderSize);
        for (var i = 0; i < 32; i++)
        {
            if (computedHeader[i] != originalHeaderChecksum[i])
            {
                throw new InvalidDataException("LpMetadataHeader checksum mismatch.");
            }
        }

        var tablesBuffer = new byte[header.TablesSize];
        if (await stream.ReadAsync(tablesBuffer, 0, tablesBuffer.Length).ConfigureAwait(false) != tablesBuffer.Length)
        {
            throw new InvalidDataException("Could not read metadata tables.");
        }

        ReadOnlySpan<byte> originalTablesChecksum = header.TablesChecksum.AsSpan();

        var computedTables = CompatibilityExtensions.ComputeSha256(tablesBuffer);
        for (var i = 0; i < 32; i++)
        {
            if (computedTables[i] != originalTablesChecksum[i])
            {
                throw new InvalidDataException("Metadata tables checksum mismatch.");
            }
        }

        var metadata = new LpMetadata
        {
            Geometry = geometry,
            Header = header
        };

        ParseTable(tablesBuffer, header.Partitions, metadata.Partitions);
        ParseTable(tablesBuffer, header.Extents, metadata.Extents);
        ParseTable(tablesBuffer, header.Groups, metadata.Groups);
        ParseTable(tablesBuffer, header.BlockDevices, metadata.BlockDevices);

        return metadata;
    }

    /// <summary>
    /// Reads LP metadata from a stream at the specified slot.
    /// </summary>
    /// <param name="stream">The source stream.</param>
    /// <param name="slotNumber">The metadata slot index (usually 0).</param>
    /// <returns>The parsed <see cref="LpMetadata"/>.</returns>
    public LpMetadata ReadMetadata(Stream stream, uint slotNumber)
    {
        ReadLogicalPartitionGeometry(stream, out var geometry);
        return ReadPrimaryMetadata(stream, geometry, slotNumber);
    }

    /// <summary>
    /// Reads LP metadata from a stream at the specified slot asynchronously.
    /// </summary>
    /// <param name="stream">The source stream.</param>
    /// <param name="slotNumber">The metadata slot index (usually 0).</param>
    /// <returns>A task that represents the asynchronous read operation, wrapping the parsed <see cref="LpMetadata"/>.</returns>
    public async Task<LpMetadata> ReadMetadataAsync(Stream stream, uint slotNumber)
    {
        // For geometry reading, keep it synchronous since it's very small (or implement async if desired)
        // Here we'll wrap it asynchronously for the metadata part which is larger
        ReadLogicalPartitionGeometry(stream, out var geometry);
        return await ReadPrimaryMetadataAsync(stream, geometry, slotNumber).ConfigureAwait(false);
    }

    /// <summary>
    /// Attempts to read the LP geometry from a stream, checking both primary and backup locations.
    /// </summary>
    /// <param name="stream">The source stream.</param>
    /// <param name="geometry">The output geometry structure.</param>
    public static void ReadLogicalPartitionGeometry(Stream stream, out LpMetadataGeometry geometry)
    {
        try
        {
            ReadPrimaryGeometry(stream, out geometry);
        }
        catch
        {
            ReadBackupGeometry(stream, out geometry);
        }
    }

    /// <summary>
    /// Reads the primary LP geometry from the predefined offset.
    /// </summary>
    /// <param name="stream">The source stream.</param>
    /// <param name="geometry">The output geometry structure.</param>
    public static void ReadPrimaryGeometry(Stream stream, out LpMetadataGeometry geometry) => ReadGeometry(stream, MetadataFormat.LP_PARTITION_RESERVED_BYTES, out geometry);

    /// <summary>
    /// Reads the backup LP geometry from the predefined offset.
    /// </summary>
    /// <param name="stream">The source stream.</param>
    /// <param name="geometry">The output geometry structure.</param>
    public static void ReadBackupGeometry(Stream stream, out LpMetadataGeometry geometry) => ReadGeometry(stream, MetadataFormat.LP_PARTITION_RESERVED_BYTES + MetadataFormat.LP_METADATA_GEOMETRY_SIZE, out geometry);

    private static void ReadGeometry(Stream stream, long offset, out LpMetadataGeometry geometry)
    {
        var buffer = new byte[MetadataFormat.LP_METADATA_GEOMETRY_SIZE];
        stream.Seek(offset, SeekOrigin.Begin);
        if (stream.Read(buffer, 0, buffer.Length) != buffer.Length)
        {
            throw new InvalidDataException($"Could not read geometry at offset 0x{offset:X}.");
        }
        ParseGeometry(buffer, out geometry);
    }

    /// <summary>
    /// Reads primary metadata for a specific slot.
    /// </summary>
    /// <param name="stream">The source stream.</param>
    /// <param name="geometry">The previously read geometry.</param>
    /// <param name="slotNumber">The slot index.</param>
    /// <returns>The parsed <see cref="LpMetadata"/>.</returns>
    public static LpMetadata ReadPrimaryMetadata(Stream stream, LpMetadataGeometry geometry, uint slotNumber)
    {
        var offset = GetPrimaryMetadataOffset(geometry, slotNumber);
        stream.Seek(offset, SeekOrigin.Begin);
        return ParseMetadata(geometry, stream);
    }

    /// <summary>
    /// Reads primary metadata for a specific slot asynchronously.
    /// </summary>
    /// <param name="stream">The source stream.</param>
    /// <param name="geometry">The previously read geometry.</param>
    /// <param name="slotNumber">The slot index.</param>
    /// <returns>A task that represents the asynchronous read operation, wrapping the parsed <see cref="LpMetadata"/>.</returns>
    public static async Task<LpMetadata> ReadPrimaryMetadataAsync(Stream stream, LpMetadataGeometry geometry, uint slotNumber)
    {
        var offset = GetPrimaryMetadataOffset(geometry, slotNumber);
        stream.Seek(offset, SeekOrigin.Begin);
        return await ParseMetadataAsync(geometry, stream).ConfigureAwait(false);
    }

    /// <summary>
    /// Calculates the byte offset of the primary metadata for a given slot.
    /// </summary>
    /// <param name="geometry">The LP geometry.</param>
    /// <param name="slotNumber">The slot index.</param>
    /// <returns>The absolute byte offset.</returns>
    public static long GetPrimaryMetadataOffset(LpMetadataGeometry geometry, uint slotNumber) => MetadataFormat.LP_PARTITION_RESERVED_BYTES + (MetadataFormat.LP_METADATA_GEOMETRY_SIZE * 2) + ((long)slotNumber * geometry.MetadataMaxSize);

    /// <summary>
    /// Calculates the byte offset of the backup metadata for a given slot at the end of the device.
    /// </summary>
    /// <param name="geometry">The LP geometry.</param>
    /// <param name="deviceSize">Total size of the block device.</param>
    /// <param name="slotNumber">The slot index.</param>
    /// <returns>The absolute byte offset from the beginning of the device.</returns>
    public static long GetBackupMetadataOffset(LpMetadataGeometry geometry, ulong deviceSize, uint slotNumber)
    {
        var backupSize = (long)geometry.MetadataMaxSize * geometry.MetadataSlotCount;
        return (long)deviceSize - backupSize + ((long)slotNumber * geometry.MetadataMaxSize);
    }

    private static void ParseTable<T>(byte[] buffer, LpMetadataTableDescriptor desc, List<T> list) where T : unmanaged
    {
        if (desc.NumEntries == 0)
        {
            return;
        }

        for (uint i = 0; i < desc.NumEntries; i++)
        {
            var offset = (int)(desc.Offset + (i * desc.EntrySize));
            list.Add(MemoryMarshal.Read<T>(buffer.AsSpan(offset, (int)desc.EntrySize)));
        }
    }
}
