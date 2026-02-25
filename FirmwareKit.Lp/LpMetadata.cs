namespace FirmwareKit.Lp;

/// <summary>
/// Represents the complete set of Android Logical Partition (LP) metadata, 
/// including geometry, header, partitions, extents, groups, and block device details.
/// </summary>
public class LpMetadata
{
    /// <summary>
    /// The geometry information describing metadata slots and sizes.
    /// </summary>
    public LpMetadataGeometry Geometry { get; set; }

    /// <summary>
    /// The metadata header containing versioning and table descriptors.
    /// </summary>
    public LpMetadataHeader Header { get; set; }

    /// <summary>
    /// The list of logical partitions.
    /// </summary>
    public List<LpMetadataPartition> Partitions { get; set; } = [];

    /// <summary>
    /// The list of extents (data segments) belonging to the partitions.
    /// </summary>
    public List<LpMetadataExtent> Extents { get; set; } = [];

    /// <summary>
    /// The list of partition groups used for size limiting.
    /// </summary>
    public List<LpMetadataPartitionGroup> Groups { get; set; } = [];

    /// <summary>
    /// The list of block devices described in this metadata.
    /// </summary>
    public List<LpMetadataBlockDevice> BlockDevices { get; set; } = [];
}
