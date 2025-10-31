using System.Collections.Generic;

namespace TrpfdManager.Core;

/// <summary>Normalize nullable/misaligned vectors found in some stock TRPFDs.</summary>
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

        // Synchronize paired vector lengths.
        while (fd.FileInfo.Count < fd.FileHashes.Count) fd.FileInfo.Add(new FileInfo());
        while (fd.FileInfo.Count > fd.FileHashes.Count) fd.FileInfo.RemoveAt(fd.FileInfo.Count - 1);

        while (fd.UnusedFileInfo.Count < fd.UnusedHashes.Count) fd.UnusedFileInfo.Add(new FileInfo());
        while (fd.UnusedFileInfo.Count > fd.UnusedHashes.Count) fd.UnusedFileInfo.RemoveAt(fd.UnusedFileInfo.Count - 1);
    }
}
