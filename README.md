# FirmwareKit.Lp

[![NuGet Version](https://img.shields.io/nuget/v/FirmwareKit.Lp.svg)](https://www.nuget.org/packages/FirmwareKit.Lp/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

A high-performance .NET library for parsing, editing, and exporting Android **super image LP (Logical Partition) metadata**. Designed specifically for firmware developers, automation scripts, and system tools.

## Installation

Install via NuGet:

```bash
dotnet add package FirmwareKit.Lp
```

## Quick Start

### 1. Reading Metadata

Using `Stream` extension methods is the most concise way:

```csharp
using FirmwareKit.Lp;

using var fs = File.OpenRead("super.img");
// Read using the default parser
var metadata = await fs.ReadLpMetadataAsync();

Console.WriteLine($"Version: {metadata.Header.MajorVersion}.{metadata.Header.MinorVersion}");
foreach (var p in metadata.Partitions)
{
    Console.WriteLine($"Partition: {p.NameString}");
}
```

Alternatively, use the parser instance directly:

```csharp
var metadata = MetadataReader.Default.ReadFromImageFile("super.img");
```

### 2. Modifying and Editing

Use `MetadataBuilder` to easily modify existing images:

```csharp
// Create a builder from existing metadata
var builder = MetadataBuilder.FromMetadata(metadata);

// Adjust partition size (e.g., resize system_a to 2GB)
var partition = builder.FindPartition("system_a");
if (partition != null)
{
    builder.ResizePartition(partition, 2uL * 1024 * 1024 * 1024);
}

// Automatically rearrange partitions to fill gaps
builder.CompactPartitions();

// Build new metadata
var updatedMetadata = builder.Build();
```

### 3. Persisting to File

```csharp
// Write the modified metadata back to a super image
await MetadataWriter.Default.WriteToImageFileAsync("super_fixed.img", updatedMetadata);
```

## Developer Guide

### Dependency Injection (DI) Integration

For complex applications, you can register the interfaces:

```csharp
// In Startup.cs or Program.cs
services.AddSingleton<ILpMetadataReader, MetadataReader>();
services.AddSingleton<ILpMetadataWriter, MetadataWriter>();
```

### Cross-Platform Compatibility

- **.NET Standard 2.0**: Compatible with Unity and older .NET Framework 4.6.1+.
- **Modern .NET**: Supports **Native AOT** trimming in .NET 8+.

## License

This project is licensed under the [MIT License](LICENSE).
