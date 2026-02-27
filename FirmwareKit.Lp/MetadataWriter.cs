using System.Runtime.InteropServices;

namespace FirmwareKit.Lp;

/// <summary>
/// Provides methods to serialize and export Android Logical Partition (LP) metadata to binary formats.
/// </summary>
public class MetadataWriter : ILpMetadataWriter
{
    private static readonly MetadataWriter _default = new();

    /// <summary>
    /// Gets a default instance of the metadata writer.
    /// </summary>
    public static MetadataWriter Default => _default;

    private static void WriteStruct<T>(Span<byte> destination, ref T value) where T : unmanaged
    {
#if NETCOREAPP3_0_OR_GREATER || NET5_0_OR_GREATER
        MemoryMarshal.Write(destination, in value);
#else
        MemoryMarshal.Write(destination, ref value);
#endif
    }

    /// <summary>
    /// Serializes an <see cref="LpMetadataGeometry"/> into a padded byte array.
    /// </summary>
    /// <param name="geometry">The geometry structure to serialize.</param>
    /// <returns>A 4KB byte array containing the geometry plus checksum.</returns>
    public byte[] SerializeGeometry(LpMetadataGeometry geometry)
    {
        geometry.Magic = MetadataFormat.LP_METADATA_GEOMETRY_MAGIC;
        geometry.StructSize = (uint)System.Runtime.CompilerServices.Unsafe.SizeOf<LpMetadataGeometry>();

        for (var i = 0; i < 32; i++)
        {
            geometry.Checksum[i] = 0;
        }

        var padded = new byte[MetadataFormat.LP_METADATA_GEOMETRY_SIZE];
        WriteStruct(padded, ref geometry);

        Span<byte> hash = stackalloc byte[32];
        CompatibilityExtensions.TryComputeSha256(padded.AsSpan(0, (int)geometry.StructSize), hash);
        hash.CopyTo(padded.AsSpan(8, 32));

        return padded;
    }

    /// <summary>
    /// Serializes <see cref="LpMetadata"/> (including header and all tables) into a binary byte array.
    /// </summary>
    /// <param name="metadata">The metadata object containing all partitions, extents, and groups.</param>
    /// <returns>A byte array representing the binary metadata.</returns>
    public byte[] SerializeMetadata(LpMetadata metadata)
    {
        var header = metadata.Header;

        uint partitionTableSize = (uint)(metadata.Partitions.Count * System.Runtime.CompilerServices.Unsafe.SizeOf<LpMetadataPartition>());
        uint extentTableSize = (uint)(metadata.Extents.Count * System.Runtime.CompilerServices.Unsafe.SizeOf<LpMetadataExtent>());
        uint groupTableSize = (uint)(metadata.Groups.Count * System.Runtime.CompilerServices.Unsafe.SizeOf<LpMetadataPartitionGroup>());
        uint blockDeviceTableSize = (uint)(metadata.BlockDevices.Count * System.Runtime.CompilerServices.Unsafe.SizeOf<LpMetadataBlockDevice>());

        header.Partitions.Offset = 0;
        header.Partitions.NumEntries = (uint)metadata.Partitions.Count;
        header.Partitions.EntrySize = (uint)System.Runtime.CompilerServices.Unsafe.SizeOf<LpMetadataPartition>();

        header.Extents.Offset = partitionTableSize;
        header.Extents.NumEntries = (uint)metadata.Extents.Count;
        header.Extents.EntrySize = (uint)System.Runtime.CompilerServices.Unsafe.SizeOf<LpMetadataExtent>();

        header.Groups.Offset = header.Extents.Offset + extentTableSize;
        header.Groups.NumEntries = (uint)metadata.Groups.Count;
        header.Groups.EntrySize = (uint)System.Runtime.CompilerServices.Unsafe.SizeOf<LpMetadataPartitionGroup>();

        header.BlockDevices.Offset = header.Groups.Offset + groupTableSize;
        header.BlockDevices.NumEntries = (uint)metadata.BlockDevices.Count;
        header.BlockDevices.EntrySize = (uint)System.Runtime.CompilerServices.Unsafe.SizeOf<LpMetadataBlockDevice>();

        header.TablesSize = header.BlockDevices.Offset + blockDeviceTableSize;

        var tables = new byte[header.TablesSize];
        WriteTable(tables.AsSpan((int)header.Partitions.Offset), metadata.Partitions);
        WriteTable(tables.AsSpan((int)header.Extents.Offset), metadata.Extents);
        WriteTable(tables.AsSpan((int)header.Groups.Offset), metadata.Groups);
        WriteTable(tables.AsSpan((int)header.BlockDevices.Offset), metadata.BlockDevices);

        Span<byte> tableHash = stackalloc byte[32];
        CompatibilityExtensions.TryComputeSha256(tables, tableHash);
        for (var i = 0; i < 32; i++)
        {
            header.TablesChecksum[i] = tableHash[i];
        }

        header.Magic = MetadataFormat.LP_METADATA_HEADER_MAGIC;
        header.HeaderSize = (uint)System.Runtime.CompilerServices.Unsafe.SizeOf<LpMetadataHeader>();

        for (var i = 0; i < 32; i++)
        {
            header.HeaderChecksum[i] = 0;
        }

        var headerBytes = new byte[header.HeaderSize];
        WriteStruct(headerBytes, ref header);

        Span<byte> headerHash = stackalloc byte[32];
        CompatibilityExtensions.TryComputeSha256(headerBytes, headerHash);
        headerHash.CopyTo(headerBytes.AsSpan(12, 32));

        var result = new byte[headerBytes.Length + tables.Length];
        Buffer.BlockCopy(headerBytes, 0, result, 0, headerBytes.Length);
        Buffer.BlockCopy(tables, 0, result, headerBytes.Length, tables.Length);
        return result;
    }

    private void WriteTable<T>(Span<byte> destination, List<T> list) where T : unmanaged
    {
        if (list.Count == 0) return;

        var entrySize = System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
        for (var i = 0; i < list.Count; i++)
        {
            var entry = list[i];
            WriteStruct(destination.Slice(i * entrySize, entrySize), ref entry);
        }
    }

    /// <summary>
    /// Writes the provided metadata to an image file.
    /// </summary>
    /// <param name="path">The path to the image file.</param>
    /// <param name="metadata">The metadata to write.</param>
    /// <returns>True if successful; otherwise, false.</returns>
    public bool WriteToImageFile(string path, LpMetadata metadata)
    {
        using var stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite);
        if (metadata.BlockDevices.Count > 0)
        {
            stream.SetLength((long)metadata.BlockDevices[0].Size);
        }
        WriteToImageStream(stream, metadata);
        return true;
    }

    /// <summary>
    /// Writes the provided metadata to an image file asynchronously.
    /// </summary>
    /// <param name="path">The path to the image file.</param>
    /// <param name="metadata">The metadata to write.</param>
    /// <returns>A task that represents the asynchronous write operation.</returns>
    public async Task WriteToImageFileAsync(string path, LpMetadata metadata)
    {
#if NETSTANDARD2_0
        using var stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 4096, true);
#else
        using var stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite);
#endif
        if (metadata.BlockDevices.Count > 0)
        {
            stream.SetLength((long)metadata.BlockDevices[0].Size);
        }
        await WriteToImageStreamAsync(stream, metadata).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes the provided metadata to a stream.
    /// </summary>
    /// <param name="stream">The destination stream.</param>
    /// <param name="metadata">The metadata to write.</param>
    /// <exception cref="InvalidOperationException">Thrown if metadata size exceeds the geometry limits.</exception>
    public void WriteToImageStream(Stream stream, LpMetadata metadata)
    {
        var geometryBlob = SerializeGeometry(metadata.Geometry);
        var metadataBlob = SerializeMetadata(metadata);

        if (metadataBlob.Length > metadata.Geometry.MetadataMaxSize)
        {
            throw new InvalidOperationException("Serialized metadata size exceeds the MetadataMaxSize defined in geometry.");
        }

        stream.Seek(MetadataFormat.LP_PARTITION_RESERVED_BYTES, SeekOrigin.Begin);
        stream.Write(geometryBlob, 0, geometryBlob.Length);
        stream.Write(geometryBlob, 0, geometryBlob.Length);

        for (uint i = 0; i < metadata.Geometry.MetadataSlotCount; i++)
        {
            var primaryOffset = MetadataReader.GetPrimaryMetadataOffset(metadata.Geometry, i);
            stream.Seek(primaryOffset, SeekOrigin.Begin);
            stream.Write(metadataBlob, 0, metadataBlob.Length);

            if (metadata.BlockDevices.Count > 0)
            {
                var backupOffset = MetadataReader.GetBackupMetadataOffset(metadata.Geometry, metadata.BlockDevices[0].Size, i);
                stream.Seek(backupOffset, SeekOrigin.Begin);
                stream.Write(metadataBlob, 0, metadataBlob.Length);
            }
        }
    }

    /// <summary>
    /// Writes the provided metadata to a stream asynchronously.
    /// </summary>
    /// <param name="stream">The destination stream.</param>
    /// <param name="metadata">The metadata to write.</param>
    /// <returns>A task that represents the asynchronous write operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if metadata size exceeds the geometry limits.</exception>
    public async Task WriteToImageStreamAsync(Stream stream, LpMetadata metadata)
    {
        var geometryBlob = SerializeGeometry(metadata.Geometry);
        var metadataBlob = SerializeMetadata(metadata);

        if (metadataBlob.Length > metadata.Geometry.MetadataMaxSize)
        {
            throw new InvalidOperationException("Serialized metadata size exceeds the MetadataMaxSize defined in geometry.");
        }

        stream.Seek(MetadataFormat.LP_PARTITION_RESERVED_BYTES, SeekOrigin.Begin);
        await stream.WriteAsync(geometryBlob, 0, geometryBlob.Length).ConfigureAwait(false);
        await stream.WriteAsync(geometryBlob, 0, geometryBlob.Length).ConfigureAwait(false);

        for (uint i = 0; i < metadata.Geometry.MetadataSlotCount; i++)
        {
            var primaryOffset = MetadataReader.GetPrimaryMetadataOffset(metadata.Geometry, i);
            stream.Seek(primaryOffset, SeekOrigin.Begin);
            await stream.WriteAsync(metadataBlob, 0, metadataBlob.Length).ConfigureAwait(false);

            if (metadata.BlockDevices.Count > 0)
            {
                var backupOffset = MetadataReader.GetBackupMetadataOffset(metadata.Geometry, metadata.BlockDevices[0].Size, i);
                stream.Seek(backupOffset, SeekOrigin.Begin);
                await stream.WriteAsync(metadataBlob, 0, metadataBlob.Length).ConfigureAwait(false);
            }
        }
    }
}
