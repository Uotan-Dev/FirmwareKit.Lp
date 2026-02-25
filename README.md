# FirmwareKit.Lp

[![NuGet Version](https://img.shields.io/nuget/v/FirmwareKit.Lp.svg)](https://www.nuget.org/packages/FirmwareKit.Lp/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

一个针对 Android **super 镜像 LP (Logical Partition) 元数据** 的高性能 .NET 解析、编辑和导出类库。专为固件开发人员、自动化脚本和系统工具设计。

## 安装

通过 NuGet 安装:

```bash
dotnet add package FirmwareKit.Lp
```

## 快速上手

### 1. 读取元数据

推荐使用 `Stream` 扩展方法，代码最简洁：

```csharp
using FirmwareKit.Lp;

using var fs = File.OpenRead("super.img");
// 使用默认解析器读取
var metadata = await fs.ReadLpMetadataAsync();

Console.WriteLine($"版本: {metadata.Header.MajorVersion}.{metadata.Header.MinorVersion}");
foreach (var p in metadata.Partitions)
{
    Console.WriteLine($"分区: {p.NameString}");
}
```

或者使用解析器实例：

```csharp
var metadata = MetadataReader.Default.ReadFromImageFile("super.img");
```

### 2. 修改与编辑

使用 `MetadataBuilder` 轻松修改现有镜像：

```csharp
// 从现有元数据创建构建器
var builder = MetadataBuilder.FromMetadata(metadata);

// 调整分区大小 (例如 system_a 调整为 2GB)
var partition = builder.FindPartition("system_a");
if (partition != null)
{
    builder.ResizePartition(partition, 2uL * 1024 * 1024 * 1024);
}

// 自动重新排列分区以填补空隙
builder.CompactPartitions();

// 构建新元数据
var updatedMetadata = builder.Build();
```

### 3. 持久化到文件

```csharp
// 将修改后的元数据写回 super 镜像
await MetadataWriter.Default.WriteToImageFileAsync("super_fixed.img", updatedMetadata);
```

## 开发者指南

### 依赖注入 (DI) 集成

对于复杂的应用程序，可以注册接口：

```csharp
// 在 Startup.cs 或 Program.cs 中
services.AddSingleton<ILpMetadataReader, MetadataReader>();
services.AddSingleton<ILpMetadataWriter, MetadataWriter>();
```

### 跨平台兼容性

- **.NET Standard 2.0**: 适用于 Unity、旧版 .NET Framework 4.6.1+。
- **Modern .NET**: 在 .NET 8+ 中支持 **Native AOT** 裁剪。

## 许可证

本项目基于 [MIT License](LICENSE) 开源。
