using System.Text;

namespace FirmwareKit.Lp;

/// <summary>
/// Represents a logical partition in the metadata builder.
/// </summary>
public class Partition
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Partition"/> class.
    /// </summary>
    /// <param name="name">The partition name.</param>
    /// <param name="groupName">The group name this partition belongs to.</param>
    /// <param name="attributes">Partition attributes.</param>
    public Partition(string name, string groupName, uint attributes)
    {
        Name = name;
        GroupName = groupName;
        Attributes = attributes;
    }

    /// <summary>
    /// Gets or sets the partition name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the group name this partition belongs to.
    /// </summary>
    public string GroupName { get; set; }

    /// <summary>
    /// Gets or sets the partition attributes.
    /// </summary>
    public uint Attributes { get; set; }

    /// <summary>
    /// Gets or sets the partition size in bytes.
    /// </summary>
    public ulong Size { get; set; }

    /// <summary>
    /// Gets or sets the list of extents for this partition.
    /// </summary>
    public List<LpMetadataExtent> Extents { get; set; } = [];

    /// <summary>
    /// Adds an extent to the partition and updates the size if linear.
    /// </summary>
    /// <param name="extent">The extent to add.</param>
    public void AddExtent(LpMetadataExtent extent)
    {
        Extents.Add(extent);
        if (extent.TargetType == MetadataFormat.LP_TARGET_TYPE_LINEAR)
        {
            Size += extent.NumSectors * MetadataFormat.LP_SECTOR_SIZE;
        }
    }
}

/// <summary>
/// Represents a logical partition group.
/// </summary>
/// <param name="name">The name of the group.</param>
/// <param name="maximumSize">The maximum size of the group in bytes (0 for no limit).</param>
/// <param name="flags">Group flags.</param>
public class PartitionGroup(string name, ulong maximumSize, uint flags = 0)
{
    /// <summary>
    /// Gets or sets the name of the group.
    /// </summary>
    public string Name { get; set; } = name;

    /// <summary>
    /// Gets or sets the maximum size allowed for all partitions in this group.
    /// </summary>
    public ulong MaximumSize { get; set; } = maximumSize;

    /// <summary>
    /// Gets or sets the group flags.
    /// </summary>
    public uint Flags { get; set; } = flags;
}

/// <summary>
/// A high-level builder for creating or modifying Android Logical Partition (LP) metadata.
/// </summary>
public class MetadataBuilder
{
    private LpMetadataGeometry _geometry;
    private readonly List<Partition> _partitions = new List<Partition>();
    private List<PartitionGroup> _groups = new List<PartitionGroup>();
    private List<LpMetadataBlockDevice> _blockDevices = new List<LpMetadataBlockDevice>();

    /// <summary>
    /// Gets the list of partition groups.
    /// </summary>
    public IReadOnlyList<PartitionGroup> Groups => _groups;

    /// <summary>
    /// Gets the list of partitions.
    /// </summary>
    public IReadOnlyList<Partition> Partitions => _partitions;

    /// <summary>
    /// Initializes a new instance of the <see cref="MetadataBuilder"/> class with a default group.
    /// </summary>
    public MetadataBuilder()
    {
        _groups.Add(new PartitionGroup("default", 0));
    }

    /// <summary>
    /// Creates a new <see cref="MetadataBuilder"/> for a new device.
    /// </summary>
    /// <param name="deviceSize">Total size of the block device.</param>
    /// <param name="metadataMaxSize">Maximum size allowed for metadata.</param>
    /// <param name="metadataSlotCount">Number of metadata slots.</param>
    /// <returns>A new <see cref="MetadataBuilder"/> instance.</returns>
    public static MetadataBuilder New(ulong deviceSize, uint metadataMaxSize, uint metadataSlotCount)
    {
        var builder = new MetadataBuilder();
        builder.Init(deviceSize, metadataMaxSize, metadataSlotCount);
        return builder;
    }

    /// <summary>
    /// Creates a <see cref="MetadataBuilder"/> from existing <see cref="LpMetadata"/>.
    /// </summary>
    /// <param name="metadata">The source metadata.</param>
    /// <returns>A <see cref="MetadataBuilder"/> initialized with existing metadata.</returns>
    public static MetadataBuilder FromMetadata(LpMetadata metadata)
    {
        var builder = new MetadataBuilder
        {
            _geometry = metadata.Geometry,
            _blockDevices = metadata.BlockDevices.ToList(),
            _groups = metadata.Groups.Select(g => new PartitionGroup(g.GetName(), g.MaximumSize, g.Flags)).ToList()
        };

        foreach (var p in metadata.Partitions)
        {
            var partition = new Partition(p.GetName(), builder._groups[(int)p.GroupIndex].Name, p.Attributes);
            for (var i = 0; i < p.NumExtents; i++)
            {
                partition.AddExtent(metadata.Extents[(int)(p.FirstExtentIndex + i)]);
            }
            builder._partitions.Add(partition);
        }
        return builder;
    }

    private void Init(ulong deviceSize, uint metadataMaxSize, uint metadataSlotCount)
    {
        _geometry = new LpMetadataGeometry
        {
            Magic = MetadataFormat.LP_METADATA_GEOMETRY_MAGIC,
            StructSize = (uint)System.Runtime.CompilerServices.Unsafe.SizeOf<LpMetadataGeometry>(),
            MetadataMaxSize = metadataMaxSize,
            MetadataSlotCount = metadataSlotCount,
            LogicalBlockSize = 4096
        };

        var super = new LpMetadataBlockDevice
        {
            Alignment = 4096,
            AlignmentOffset = 0,
            Size = deviceSize,
            FirstLogicalSector = Utility.AlignTo(
                MetadataFormat.LP_PARTITION_RESERVED_BYTES + ((MetadataFormat.LP_METADATA_GEOMETRY_SIZE + ((ulong)metadataMaxSize * metadataSlotCount)) * 2),
                4096
            ) / MetadataFormat.LP_SECTOR_SIZE
        };
        super.PartitionName.SetName(MetadataFormat.LP_METADATA_DEFAULT_PARTITION_NAME);

        _blockDevices.Add(super);
    }

    /// <summary>
    /// Adds a new partition to the metadata builder.
    /// </summary>
    /// <param name="name">The name of the partition.</param>
    /// <param name="groupName">The name of the partition group it belongs to.</param>
    /// <param name="attributes">Attributes for the partition.</param>
    /// <exception cref="ArgumentException">Thrown if partition already exists or group doesn't exist.</exception>
    public void AddPartition(string name, string groupName, uint attributes)
    {
        if (_partitions.Any(p => p.Name == name))
        {
            throw new ArgumentException($"Partition '{name}' already exists.");
        }

        if (!_groups.Any(g => g.Name == groupName))
        {
            throw new ArgumentException($"Partition group '{groupName}' does not exist.");
        }

        _partitions.Add(new Partition(name, groupName, attributes));
    }

    /// <summary>
    /// Removes a partition by name.
    /// </summary>
    /// <param name="name">The name of the partition.</param>
    public void RemovePartition(string name)
    {
        var partition = FindPartition(name);
        if (partition != null)
        {
            _partitions.Remove(partition);
        }
    }

    /// <summary>
    /// Changes the order of the partitions in the metadata.
    /// </summary>
    /// <param name="orderedNames">The desired sequence of partition names.</param>
    public void ReorderPartitions(IEnumerable<string> orderedNames)
    {
        var newOrder = new List<Partition>();
        foreach (var name in orderedNames)
        {
            var p = _partitions.Find(x => x.Name == name);
            if (p != null)
            {
                newOrder.Add(p);
            }
        }
        _partitions.Clear();
        _partitions.AddRange(newOrder);
    }

    /// <summary>
    /// Adds a new partition group to the builder.
    /// </summary>
    /// <param name="name">The name of the group.</param>
    /// <param name="maxSize">Maximum combined size of partitions in this group.</param>
    /// <exception cref="ArgumentException">Thrown if group already exists.</exception>
    public void AddGroup(string name, ulong maxSize)
    {
        if (_groups.Any(g => g.Name == name))
        {
            throw new ArgumentException($"Partition group '{name}' already exists.");
        }

        _groups.Add(new PartitionGroup(name, maxSize));
    }

    /// <summary>
    /// Removes a partition group by name.
    /// </summary>
    /// <param name="name">The name of the group.</param>
    /// <exception cref="InvalidOperationException">Thrown if group is default or in use by partitions.</exception>
    public void RemoveGroup(string name)
    {
        if (name == "default")
        {
            throw new InvalidOperationException("Cannot delete the 'default' partition group.");
        }

        var group = _groups.Find(g => g.Name == name);
        if (group != null)
        {
            if (_partitions.Any(p => p.GroupName == name))
            {
                throw new InvalidOperationException($"Partition group '{name}' is in use and cannot be deleted.");
            }
            _groups.Remove(group);
        }
    }

    /// <summary>
    /// Resizes an existing partition group.
    /// </summary>
    /// <param name="name">The name of the group.</param>
    /// <param name="newMaxSize">The new maximum size limit.</param>
    /// <exception cref="ArgumentException">Thrown if group doesn't exist.</exception>
    /// <exception cref="InvalidOperationException">Thrown if new size is smaller than current usage.</exception>
    public void ResizeGroup(string name, ulong newMaxSize)
    {
        var group = _groups.Find(g => g.Name == name) ?? throw new ArgumentException($"Partition group '{name}' does not exist.");
        if (newMaxSize > 0)
        {
            var currentUsage = _partitions.Where(p => p.GroupName == name).Sum(p => (long)p.Size);
            if ((ulong)currentUsage > newMaxSize)
            {
                throw new InvalidOperationException($"New capacity limit ({newMaxSize / (1024 * 1024.0):F2} MiB) is smaller than current space used by partitions in this group ({(ulong)currentUsage / (1024 * 1024.0):F2} MiB).");
            }
        }
        group.MaximumSize = newMaxSize;
    }

    /// <summary>
    /// Finds a partition by its name.
    /// </summary>
    /// <param name="name">The partition name.</param>
    /// <returns>The <see cref="Partition"/> if found; otherwise, null.</returns>
    public Partition? FindPartition(string name) => _partitions.FirstOrDefault(p => p.Name == name);

    /// <summary>
    /// Finds a partition group by its name.
    /// </summary>
    /// <param name="name">The group name.</param>
    /// <returns>The <see cref="PartitionGroup"/> if found; otherwise, null.</returns>
    public PartitionGroup? FindGroup(string name) => _groups.FirstOrDefault(g => g.Name == name);

    /// <summary>
    /// Updates the size of the underlying block device.
    /// </summary>
    /// <param name="newSize">New device size in bytes.</param>
    /// <exception cref="InvalidOperationException">Thrown if new size is smaller than current partition layout.</exception>
    public void ResizeBlockDevice(ulong newSize)
    {
        if (_blockDevices.Count == 0)
        {
            return;
        }

        var maxSectorUsed = _blockDevices[0].FirstLogicalSector;
        foreach (var p in _partitions)
        {
            foreach (var extent in p.Extents)
            {
                if (extent.TargetType == MetadataFormat.LP_TARGET_TYPE_LINEAR)
                {
                    maxSectorUsed = Math.Max(maxSectorUsed, extent.TargetData + extent.NumSectors);
                }
            }
        }

        var minRequiredSize = maxSectorUsed * MetadataFormat.LP_SECTOR_SIZE;
        if (newSize < minRequiredSize)
        {
            throw new InvalidOperationException($"Could not resize image: New capacity ({newSize / (1024 * 1024.0):F2} MiB) is smaller than the minimum required space used by partitions ({minRequiredSize / (1024 * 1024.0):F2} MiB).");
        }

        var device = _blockDevices[0];
        device.Size = newSize;
        _blockDevices[0] = device;
    }

    /// <summary>
    /// Resizes a specific partition to requested size.
    /// </summary>
    /// <param name="partition">The partition to resize.</param>
    /// <param name="requestedSize">New requested size in bytes.</param>
    /// <exception cref="InvalidOperationException">Thrown if resize fails due to space or group limits.</exception>
    public void ResizePartition(Partition partition, ulong requestedSize)
    {
        if (partition.Size == requestedSize)
        {
            return;
        }

        if (requestedSize > partition.Size)
        {
            var group = _groups.First(g => g.Name == partition.GroupName);
            if (group.MaximumSize > 0)
            {
                var currentGroupSize = _partitions
                    .Where(p => p.GroupName == partition.GroupName)
                    .Aggregate(0UL, (sum, p) => sum + p.Size);
                if (currentGroupSize - partition.Size + requestedSize > group.MaximumSize)
                {
                    throw new InvalidOperationException($"Exceeded capacity limit of partition group '{partition.GroupName}'.");
                }
            }
            if (!GrowPartition(partition, requestedSize))
            {
                throw new InvalidOperationException($"Insufficient disk space to grow partition '{partition.Name}'.");
            }
            return;
        }

        ShrinkPartition(partition, requestedSize);
    }

    private void ShrinkPartition(Partition partition, ulong requestedSize)
    {
        var sectorsToKeep = requestedSize / MetadataFormat.LP_SECTOR_SIZE;
        ulong currentSectors = 0;
        var newExtents = new List<LpMetadataExtent>();

        foreach (var extent in partition.Extents)
        {
            if (currentSectors + extent.NumSectors <= sectorsToKeep)
            {
                newExtents.Add(extent);
                currentSectors += extent.NumSectors;
            }
            else
            {
                var needed = sectorsToKeep - currentSectors;
                if (needed > 0)
                {
                    var partial = extent;
                    partial.NumSectors = needed;
                    newExtents.Add(partial);
                    currentSectors += needed;
                }
                break;
            }
        }
        partition.Extents = newExtents;
        partition.Size = currentSectors * MetadataFormat.LP_SECTOR_SIZE;
    }

    private bool GrowPartition(Partition partition, ulong requestedSize)
    {
        var sectorsNeeded = (requestedSize - partition.Size) / MetadataFormat.LP_SECTOR_SIZE;
        var freeRegions = GetFreeRegions();

        var device = _blockDevices[0];
        var alignmentSectors = device.Alignment / MetadataFormat.LP_SECTOR_SIZE;
        var alignmentOffsetSectors = device.AlignmentOffset / MetadataFormat.LP_SECTOR_SIZE;

        foreach (var region in freeRegions)
        {
            if (sectorsNeeded == 0)
            {
                break;
            }

            var startSector = region.StartSector;
            if (alignmentSectors > 0)
            {
                var remainder = (startSector - alignmentOffsetSectors) % alignmentSectors;
                if (remainder > 0)
                {
                    startSector += alignmentSectors - remainder;
                }
            }

            if (startSector >= region.StartSector + region.NumSectors)
            {
                continue;
            }

            var availableSectors = region.StartSector + region.NumSectors - startSector;
            var allocateSectors = Math.Min(availableSectors, sectorsNeeded);

            partition.AddExtent(new LpMetadataExtent
            {
                NumSectors = allocateSectors,
                TargetType = MetadataFormat.LP_TARGET_TYPE_LINEAR,
                TargetData = startSector,
                TargetSource = 0
            });
            sectorsNeeded -= allocateSectors;
        }

        return sectorsNeeded == 0;
    }

    private struct FreeRegion
    {
        public ulong StartSector;
        public ulong NumSectors;
    }

    private List<FreeRegion> GetFreeRegions()
    {
        var regions = new List<FreeRegion>();
        var firstSector = _blockDevices[0].FirstLogicalSector;

        var backupSize = (ulong)_geometry.MetadataMaxSize * _geometry.MetadataSlotCount;
        var lastSector = (_blockDevices[0].Size - backupSize) / MetadataFormat.LP_SECTOR_SIZE;

        var extents = _partitions.SelectMany(p => p.Extents)
            .Where(e => e.TargetType == MetadataFormat.LP_TARGET_TYPE_LINEAR)
            .OrderBy(e => e.TargetData)
            .ToList();

        var currentSector = firstSector;
        foreach (var extent in extents)
        {
            if (extent.TargetData > currentSector)
            {
                regions.Add(new FreeRegion { StartSector = currentSector, NumSectors = extent.TargetData - currentSector });
            }
            currentSector = Math.Max(currentSector, extent.TargetData + extent.NumSectors);
        }

        if (currentSector < lastSector)
        {
            regions.Add(new FreeRegion { StartSector = currentSector, NumSectors = lastSector - currentSector });
        }

        return regions;
    }

    /// <summary>
    /// Compacts the partition layout by removing gaps between partitions.
    /// </summary>
    public void CompactPartitions()
    {
        var currentSector = _blockDevices[0].FirstLogicalSector;
        var alignmentSectors = _blockDevices[0].Alignment / MetadataFormat.LP_SECTOR_SIZE;
        var alignmentOffsetSectors = _blockDevices[0].AlignmentOffset / MetadataFormat.LP_SECTOR_SIZE;

        foreach (var partition in _partitions)
        {
            if (partition.Size == 0)
            {
                continue;
            }

            if (alignmentSectors > 0)
            {
                var remainder = (currentSector - alignmentOffsetSectors) % alignmentSectors;
                if (remainder > 0)
                {
                    currentSector += alignmentSectors - remainder;
                }
            }

            var totalSectors = partition.Size / MetadataFormat.LP_SECTOR_SIZE;
            partition.Extents.Clear();
            partition.AddExtent(new LpMetadataExtent
            {
                NumSectors = totalSectors,
                TargetType = MetadataFormat.LP_TARGET_TYPE_LINEAR,
                TargetData = currentSector,
                TargetSource = 0
            });

            currentSector += totalSectors;
        }
    }

    /// <summary>
    /// Exports the current builder state to an immutable <see cref="LpMetadata"/> object.
    /// </summary>
    /// <returns>The generated metadata.</returns>
    public LpMetadata Build() => Export();

    /// <summary>
    /// Exports the current builder state to an immutable <see cref="LpMetadata"/> object.
    /// </summary>
    /// <returns>The generated metadata.</returns>
    public LpMetadata Export()
    {
        var metadata = new LpMetadata
        {
            Geometry = _geometry,
            Header = new LpMetadataHeader
            {
                Magic = MetadataFormat.LP_METADATA_HEADER_MAGIC,
                MajorVersion = MetadataFormat.LP_METADATA_MAJOR_VERSION,
                MinorVersion = MetadataFormat.LP_METADATA_MINOR_VERSION_MIN,
                HeaderSize = (uint)System.Runtime.CompilerServices.Unsafe.SizeOf<LpMetadataHeader>()
            },
            Groups = _groups.Select(g =>
                {
                    var group = new LpMetadataPartitionGroup { Flags = g.Flags, MaximumSize = g.MaximumSize };
                    group.Name.SetName(g.Name);
                    return group;
                }).ToList(),

            BlockDevices = _blockDevices
        };

        // Cache group indices to avoid O(N^2) behavior in Export
        var groupDict = _groups.Select((g, i) => (g.Name, i)).ToDictionary(x => x.Name, x => x.i);

        foreach (var p in _partitions)
        {
            var lpp = new LpMetadataPartition
            {
                Attributes = p.Attributes,
                FirstExtentIndex = (uint)metadata.Extents.Count,
                NumExtents = (uint)p.Extents.Count,
                GroupIndex = (uint)groupDict[p.GroupName]
            };
            lpp.Name.SetName(p.Name);
            metadata.Partitions.Add(lpp);
            metadata.Extents.AddRange(p.Extents);
        }

        return metadata;
    }
}
