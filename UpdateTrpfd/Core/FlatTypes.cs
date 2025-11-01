using System.Collections.Generic;
using FlatSharp.Attributes;

namespace TrpfdManager.Core;

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

internal static class FdFixer
{
    public static void Fix(FileDescriptor fd)
    {
        fd.FileHashes ??= new List<ulong>();
        fd.FileInfo ??= new List<FileInfo>();
        fd.PackNames ??= new List<string>();
        fd.PackInfo ??= new List<PackInfo>();
        fd.UnusedHashes ??= new List<ulong>();
        fd.UnusedFileInfo ??= new List<FileInfo>();

        while (fd.FileInfo.Count < fd.FileHashes.Count) fd.FileInfo.Add(new FileInfo());
        while (fd.FileInfo.Count > fd.FileHashes.Count) fd.FileInfo.RemoveAt(fd.FileInfo.Count - 1);

        while (fd.UnusedFileInfo.Count < fd.UnusedHashes.Count) fd.UnusedFileInfo.Add(new FileInfo());
        while (fd.UnusedFileInfo.Count > fd.UnusedHashes.Count) fd.UnusedFileInfo.RemoveAt(fd.UnusedFileInfo.Count - 1);
    }
}