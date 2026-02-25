namespace FirmwareKit.Lp;

/// <summary>
/// Provides extension methods for <see cref="Stream"/> to interact with LP metadata.
/// </summary>
public static class StreamExtensions
{
    /// <summary>
    /// Reads LP metadata from the stream using the default reader.
    /// </summary>
    public static LpMetadata ReadLpMetadata(this Stream stream)
    {
        return MetadataReader.Default.ReadFromImageStream(stream);
    }

    /// <summary>
    /// Reads LP metadata from the stream asynchronously using the default reader.
    /// </summary>
    public static Task<LpMetadata> ReadLpMetadataAsync(this Stream stream)
    {
        return MetadataReader.Default.ReadFromImageStreamAsync(stream);
    }

    /// <summary>
    /// Reads LP metadata from the stream for a specific slot.
    /// </summary>
    public static LpMetadata ReadLpMetadata(this Stream stream, uint slot)
    {
        return MetadataReader.Default.ReadMetadata(stream, slot);
    }

    /// <summary>
    /// Reads LP metadata from the stream for a specific slot asynchronously.
    /// </summary>
    public static Task<LpMetadata> ReadLpMetadataAsync(this Stream stream, uint slot)
    {
        return MetadataReader.Default.ReadMetadataAsync(stream, slot);
    }

    /// <summary>
    /// Writes LP metadata to the stream using the default writer.
    /// </summary>
    public static void WriteLpMetadata(this Stream stream, LpMetadata metadata)
    {
        MetadataWriter.Default.WriteToImageStream(stream, metadata);
    }

    /// <summary>
    /// Writes LP metadata to the stream asynchronously using the default writer.
    /// </summary>
    public static Task WriteLpMetadataAsync(this Stream stream, LpMetadata metadata)
    {
        return MetadataWriter.Default.WriteToImageStreamAsync(stream, metadata);
    }
}
