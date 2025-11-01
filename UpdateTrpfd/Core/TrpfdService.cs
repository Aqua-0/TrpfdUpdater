using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace TrpfdManager.Core;

public sealed partial class TrpfdService
{
    protected readonly string _gameRoot;
    protected readonly string _romfsRoot;
    protected readonly string _stripPrefix;
    protected readonly bool _autoBackup;
    protected readonly Action<string> _log;
    protected string _trpfdPath = "";
    protected string _lastBackupPath = "";

    public TrpfdService(string gameRoot, string romfsRoot, string stripPrefix, bool autoBackup, Action<string>? log = null)
    {
        _gameRoot = string.IsNullOrWhiteSpace(gameRoot) ? "" : Path.GetFullPath(gameRoot);
        _romfsRoot = string.IsNullOrWhiteSpace(romfsRoot) ? "" : Path.GetFullPath(romfsRoot);
        _stripPrefix = stripPrefix ?? "";
        _autoBackup = autoBackup;
        _log = log ?? (_ => { });
    }

    public string ResolvedTrpfdPath => _trpfdPath;
    public string LastBackupPath => _lastBackupPath;

    public string FullSync(bool dryRun)
    {
        Validate();
        var fd = Load(ResolveTrpfdPath());
        var live = new HashSet<ulong>(EnumerateLooseHashes());
        var moves = new List<string>();

        foreach (var h in live) if (fd.FileHashes.Contains(h)) MoveToUnused(fd, h, moves);
        var restore = fd.UnusedHashes.Where(h => !live.Contains(h)).ToArray();
        foreach (var h in restore) MoveBack(fd, h, moves);

        if (!dryRun) { SortPrimary(fd); SaveWithOptionalBackup(fd, _trpfdPath); }
        return Report(_trpfdPath, live.Count, moves, dryRun);
    }

    public string FullSyncFromOverlay(string baseTrpfd, string targetTrpfd, IEnumerable<string> overlayFiles, bool dryRun)
    {
        if (!File.Exists(baseTrpfd)) throw new FileNotFoundException("base TRPFD not found", baseTrpfd);
        var fd = Load(baseTrpfd);

        var live = new HashSet<ulong>(overlayFiles.Select(ComputeOverlayKey).Select(GfFnv1a64.Hash));
        var moves = new List<string>();
        foreach (var h in live) if (fd.FileHashes.Contains(h)) MoveToUnused(fd, h, moves);

        if (!dryRun) { SortPrimary(fd); SaveWithOptionalBackup(fd, targetTrpfd); }
        return Report(targetTrpfd, live.Count, moves, dryRun);
    }

    private static string ComputeOverlayKey(string fullPath)
    {
        var d = new DirectoryInfo(Path.GetDirectoryName(fullPath)!);
        while (d is not null && !string.Equals(d.Name, "romfs", StringComparison.OrdinalIgnoreCase)) d = d.Parent;
        if (d is null) throw new DirectoryNotFoundException($"romfs root not found for {fullPath}");
        return Path.GetRelativePath(d.FullName, fullPath).Replace('\\', '/').TrimStart('/');
    }

    private string Report(string targetTrpfd, int liveCount, List<string> moves, bool dryRun)
    {
        var lines = new List<string>
        {
            $"{(dryRun ? "[dry-run]" : "[save]")} {targetTrpfd}",
            $"loose files: {liveCount}"
        };
        lines.AddRange(moves.Count == 0 ? new[] { "[no changes]" } : moves);
        return string.Join(Environment.NewLine, lines);
    }

    protected string ResolveTrpfdPath()
    {
        var p1 = Path.Combine(_gameRoot, "arc", "data.trpfd");
        var p2 = Path.Combine(_gameRoot, "romfs", "arc", "data.trpfd");
        if (File.Exists(p1)) _trpfdPath = p1;
        else if (File.Exists(p2)) _trpfdPath = p2;
        else throw new FileNotFoundException("missing data.trpfd under arc\\ or romfs\\arc\\", p1);
        return _trpfdPath;
    }

    protected static FileDescriptor Load(string trpfdPath)
    {
        var fd = FlatIo.Load<FileDescriptor>(trpfdPath);
        FdFixer.Fix(fd);
        return fd;
    }

    protected IEnumerable<ulong> EnumerateLooseHashes()
    {
        foreach (var f in Directory.EnumerateFiles(_romfsRoot, "*", SearchOption.AllDirectories))
        {
            if (f.EndsWith(".bak", StringComparison.OrdinalIgnoreCase)) continue;
            var key = PathRules.ToGameKey(f, _romfsRoot, _stripPrefix);
            yield return GfFnv1a64.Hash(key);
        }
    }

    protected static void SortPrimary(FileDescriptor fd)
    {
        var pairs = fd.FileHashes.Select((h, i) => (h, fd.FileInfo[i])).OrderBy(t => t.h).ToArray();
        fd.FileHashes = pairs.Select(p => p.h).ToList();
        fd.FileInfo = pairs.Select(p => p.Item2).ToList();
    }

    protected void SaveWithOptionalBackup(FileDescriptor fd, string targetPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        if (_autoBackup && File.Exists(targetPath))
        {
            var ts = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            var backup = targetPath + ".bak-" + ts;
            File.Copy(targetPath, backup, overwrite: false);
            _lastBackupPath = backup;
            _log("[backup] " + backup);
        }
        FlatIo.Save(fd, targetPath);
        _log("[save] " + targetPath);
    }

    protected void Validate()
    {
        if (!Directory.Exists(_gameRoot)) throw new DirectoryNotFoundException($"game root not found: {_gameRoot}");
        if (!Directory.Exists(_romfsRoot)) throw new DirectoryNotFoundException($"romfs root not found: {_romfsRoot}");
    }

    // helpers required by errors
    protected void MoveToUnused(FileDescriptor fd, ulong h, List<string>? moves)
    {
        var idx = fd.FileHashes.IndexOf(h); if (idx < 0) return;
        var fi = fd.FileInfo[idx];
        fd.FileHashes.RemoveAt(idx); fd.FileInfo.RemoveAt(idx);
        fd.UnusedHashes.Add(h); fd.UnusedFileInfo.Add(fi);
        moves?.Add($"removed 0x{h:X16}");
    }
    private static int FindInsertIndex(IList<ulong> sorted, ulong value)
    {
        // binary search without requiring List<T>
        int lo = 0, hi = sorted.Count - 1;
        while (lo <= hi)
        {
            int mid = lo + ((hi - lo) >> 1);
            ulong mv = sorted[mid];
            if (mv == value) return mid;
            if (mv < value) lo = mid + 1; else hi = mid - 1;
        }
        return lo; // insertion point
    }

    protected void MoveBack(FileDescriptor fd, ulong h, List<string>? moves)
    {
        var idx = fd.UnusedHashes.IndexOf(h); if (idx < 0) return;
        var fi = fd.UnusedFileInfo[idx];
        fd.UnusedHashes.RemoveAt(idx);
        fd.UnusedFileInfo.RemoveAt(idx);

        int pos = FindInsertIndex(fd.FileHashes, h);
        fd.FileHashes.Insert(pos, h);
        fd.FileInfo.Insert(pos, fi);
        moves?.Add($"restored 0x{h:X16}");
    }
}