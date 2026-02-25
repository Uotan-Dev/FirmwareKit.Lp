namespace FirmwareKit.Lp;

/// <summary>
/// Defines methods for reading Android Logical Partition (LP) metadata.
/// </summary>
public interface ILpMetadataReader
{
    /// <summary>
    /// Reads LP metadata from a file path.
    /// </summary>
    LpMetadata ReadFromImageFile(string path);

    /// <summary>
    /// Reads LP metadata from a file path asynchronously.
    /// </summary>
    Task<LpMetadata> ReadFromImageFileAsync(string path);

    /// <summary>
    /// Reads LP metadata from a stream.
    /// </summary>
    LpMetadata ReadFromImageStream(Stream stream);

    /// <summary>
    /// Reads LP metadata from a stream asynchronously.
    /// </summary>
    Task<LpMetadata> ReadFromImageStreamAsync(Stream stream);

    /// <summary>
    /// Reads LP metadata for a specific slot.
    /// </summary>
    LpMetadata ReadMetadata(Stream stream, uint slotNumber);

    /// <summary>
    /// Reads LP metadata for a specific slot asynchronously.
    /// </summary>
    Task<LpMetadata> ReadMetadataAsync(Stream stream, uint slotNumber);
}

/// <summary>
/// Defines methods for writing and serializing Android Logical Partition (LP) metadata.
/// </summary>
public interface ILpMetadataWriter
{
    /// <summary>
    /// Writes the provided metadata to an image file.
    /// </summary>
    bool WriteToImageFile(string path, LpMetadata metadata);

    /// <summary>
    /// Writes the provided metadata to an image file asynchronously.
    /// </summary>
    Task WriteToImageFileAsync(string path, LpMetadata metadata);

    /// <summary>
    /// Writes the provided metadata to a stream.
    /// </summary>
    void WriteToImageStream(Stream stream, LpMetadata metadata);

    /// <summary>
    /// Writes the provided metadata to a stream asynchronously.
    /// </summary>
    Task WriteToImageStreamAsync(Stream stream, LpMetadata metadata);

    /// <summary>
    /// Serializes metadata into a binary byte array.
    /// </summary>
    byte[] SerializeMetadata(LpMetadata metadata);
}
