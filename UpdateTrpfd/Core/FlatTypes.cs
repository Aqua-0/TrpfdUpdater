using System.Collections.Generic;
using FlatSharp.Attributes;

namespace TrpfdManager.Core;

// FlatSharp 7.x: properties virtual, vectors IList<T>.
[FlatBufferTable]
public class FileInfo
{
    [FlatBufferItem(0)] public virtual ulong PackIndex { get; set; } = 0;
    [FlatBufferItem(1)] public virtual uint UnusedTable { get; set; } = 0;
}

[FlatBufferTable]
public class PackInfo
{
    [FlatBufferItem(0)] public virtual ulong FileSize { get; set; } = 0;
    [FlatBufferItem(1)] public virtual ulong FileCount { get; set; } = 0;
}

// Single table. 0..3 base schema. 4..5 extra stash; ignored by the game.
[FlatBufferTable]
public class FileDescriptor
{
    [FlatBufferItem(0)] public virtual IList<ulong> FileHashes { get; set; } = new List<ulong>();
    [FlatBufferItem(1)] public virtual IList<string> PackNames { get; set; } = new List<string>();
    [FlatBufferItem(2)] public virtual IList<FileInfo> FileInfo { get; set; } = new List<FileInfo>();
    [FlatBufferItem(3)] public virtual IList<PackInfo> PackInfo { get; set; } = new List<PackInfo>();

    [FlatBufferItem(4)] public virtual IList<ulong> UnusedHashes { get; set; } = new List<ulong>();
    [FlatBufferItem(5)] public virtual IList<FileInfo> UnusedFileInfo { get; set; } = new List<FileInfo>();
}