// File: TrpfdManager/Core/TrpfdService.cs
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace TrpfdManager.Core;

public sealed class TrpfdService
{
    private readonly string _gameRoot;
    private readonly string _romfsRoot;
    private readonly string _stripPrefix;
    private readonly bool _autoBackup;
    private readonly Action<string> _log;
    private string _trpfdPath = "";
    private string _lastBackupPath = "";

    public TrpfdService(string gameRoot, string romfsRoot, string stripPrefix, bool autoBackup, Action<string>? log = null)
    {
        _gameRoot = (gameRoot ?? "").Trim();
        _romfsRoot = (romfsRoot ?? "").Trim();
        _stripPrefix = stripPrefix ?? "";
        _autoBackup = autoBackup;
        _log = log ?? (_ => { });
    }

    public string ResolvedTrpfdPath => _trpfdPath;
    public string LastBackupPath => _lastBackupPath;

    // debug helpers for Diagnostics
    internal string DebugRomfsRoot => _romfsRoot;
    internal void DebugEchoConfig(List<string> sink)
    {
        sink.Add("[cfg] GameRoot=" + _gameRoot);
        sink.Add("[cfg] RomfsRoot=" + _romfsRoot);
        sink.Add("[cfg] StripPrefix=" + _stripPrefix);
        sink.Add("[cfg] AutoBackup=" + _autoBackup);
    }
    internal string DebugResolveTrpfd() { Validate(); return _trpfdPath; }

    public string FullSync(bool dryRun)
    {
        Validate();
        var fd = Load(_trpfdPath);

        var live = new HashSet<ulong>(EnumerateLooseHashes());
        var moves = new List<string>();

        foreach (var h in live)
            if (fd.FileHashes.Contains(h))
                MoveToUnused(fd, h, moves);

        var restore = fd.UnusedHashes.Where(h => !live.Contains(h)).ToArray();
        foreach (var h in restore)
            MoveBack(fd, h, moves);

        if (!dryRun)
        {
            SortPrimary(fd);
            SaveWithOptionalBackup(fd);
        }

        var report = new[]
        {
            $"{(dryRun ? "[dry-run]" : "[save]")} {_trpfdPath}",
            $"auto-backup: {(_autoBackup ? "on" : "off")}",
            $"loose files: {live.Count}",
            string.Join(Environment.NewLine, moves.DefaultIfEmpty("[no changes]"))
        }.Where(s => !string.IsNullOrEmpty(s));

        return string.Join(Environment.NewLine, report);
    }

    public string ExportCsv()
    {
        Validate();
        var fd = Load(_trpfdPath);
        var dir = Path.GetDirectoryName(_trpfdPath);
        if (string.IsNullOrEmpty(dir)) dir = AppContext.BaseDirectory;
        var ts = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var csv = Path.Combine(dir, $"data.trpfd-entries-{ts}.csv");

        using var w = new StreamWriter(csv, false);
        w.WriteLine("HashHex,PackIndex,PackName,IsUnused");
        for (int i = 0; i < fd.FileHashes.Count; i++)
        {
            var h = fd.FileHashes[i];
            var fi = fd.FileInfo[i];
            var packIdx = (long)(fi?.PackIndex ?? 0UL);
            string packName = (packIdx >= 0 && packIdx < fd.PackNames.Count) ? (fd.PackNames[(int)packIdx] ?? "") : "";
            w.WriteLine($"0x{h:X16},{packIdx},\"{packName.Replace("\"", "\"\"")}\",0");
        }
        for (int i = 0; i < fd.UnusedHashes.Count; i++)
        {
            var h = fd.UnusedHashes[i];
            var fi = fd.UnusedFileInfo[i];
            var packIdx = (long)(fi?.PackIndex ?? 0UL);
            string packName = (packIdx >= 0 && packIdx < fd.PackNames.Count) ? (fd.PackNames[(int)packIdx] ?? "") : "";
            w.WriteLine($"0x{h:X16},{packIdx},\"{packName.Replace("\"", "\"\"")}\",1");
        }
        _log("[csv] wrote " + csv);
        return csv;
    }

    public IEnumerable<string> ListRecognizedLooseFiles(int limit = 500)
    {
        Validate();
        var fd = Load(_trpfdPath);

        int shown = 0, total = 0, overriding = 0, presentButMapped = 0, unknown = 0;

        yield return "[scan] romfs = " + _romfsRoot;

        foreach (var f in Directory.EnumerateFiles(_romfsRoot, "*", SearchOption.AllDirectories))
        {
            if (f.EndsWith(".bak", StringComparison.OrdinalIgnoreCase)) continue;

            total++;
            var rel = PathRules.ToGameKey(f, _romfsRoot, _stripPrefix);
            var h = GfFnv1a64.Hash(rel);

            if (fd.UnusedHashes.Contains(h))
            {
                overriding++;
                if (shown < limit) { shown++; yield return $"[override] {rel}  hash=0x{h:X16}"; }
            }
            else if (fd.FileHashes.Contains(h))
            {
                presentButMapped++;
                if (shown < limit) { shown++; yield return $"[present-but-mapped] {rel}  hash=0x{h:X16}"; }
            }
            else
            {
                unknown++;
                if (shown < limit) { shown++; yield return $"[unknown] {rel}  hash=0x{h:X16}"; }
            }
        }

        yield return $"[summary] files={total}  overriding={overriding}  present-but-mapped={presentButMapped}  unknown={unknown}";
        if (total > shown) yield return $"[note] showing first {shown} of {total}. Export CSV for full listing.";
    }

    public WatchHandle StartWatcher()
    {
        Validate();
        var fd = Load(_trpfdPath);

        var fsw = new FileSystemWatcher(_romfsRoot)
        {
            IncludeSubdirectories = true,
            EnableRaisingEvents = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.Size
        };

        FileSystemEventHandler onCreateOrChange = (_, e) =>
        {
            try
            {
                if (Directory.Exists(e.FullPath)) return;
                if (!File.Exists(e.FullPath)) return;
                ApplyChange(fd, e.FullPath, present: true);
            }
            catch (Exception ex) { _log("[watch-error] " + ex.ToString()); }
        };

        FileSystemEventHandler onDelete = (_, e) =>
        {
            try
            {
                if (Directory.Exists(e.FullPath)) return;
                ApplyChange(fd, e.FullPath, present: false);
            }
            catch (Exception ex) { _log("[watch-error] " + ex.ToString()); }
        };

        RenamedEventHandler onRename = (_, e) =>
        {
            try
            {
                if (!string.IsNullOrEmpty(e.OldFullPath)) ApplyChange(fd, e.OldFullPath, present: false);
                if (File.Exists(e.FullPath)) ApplyChange(fd, e.FullPath, present: true);
            }
            catch (Exception ex) { _log("[watch-error] " + ex.ToString()); }
        };

        fsw.Created += onCreateOrChange;
        fsw.Changed += onCreateOrChange;
        fsw.Deleted += onDelete;
        fsw.Renamed += onRename;

        return new WatchHandle(fsw, () =>
        {
            fsw.Created -= onCreateOrChange;
            fsw.Changed -= onCreateOrChange;
            fsw.Deleted -= onDelete;
            fsw.Renamed -= onRename;
            fsw.Dispose();
        });
    }

    private void ApplyChange(FileDescriptor fd, string fullPath, bool present)
    {
        var key = PathRules.ToGameKey(fullPath, _romfsRoot, _stripPrefix);
        var h = GfFnv1a64.Hash(key);

        if (present && fd.FileHashes.Contains(h))
        {
            MoveToUnused(fd, h, null);
            SortPrimary(fd);
            SaveWithOptionalBackup(fd);
        }
        else if (!present && fd.UnusedHashes.Contains(h))
        {
            MoveBack(fd, h, null);
            SortPrimary(fd);
            SaveWithOptionalBackup(fd);
        }
    }

    private void Validate()
    {
        if (string.IsNullOrWhiteSpace(_gameRoot)) throw new ArgumentException("Game Root is empty.");
        if (string.IsNullOrWhiteSpace(_romfsRoot)) throw new ArgumentException("Romfs Root is empty.");
        if (!Directory.Exists(_gameRoot)) throw new DirectoryNotFoundException($"Game Root not found: '{_gameRoot}'");
        if (!Directory.Exists(_romfsRoot)) throw new DirectoryNotFoundException($"Romfs Root not found: '{_romfsRoot}'");

        var p1 = Path.Combine(_gameRoot, "arc", "data.trpfd");
        var p2 = Path.Combine(_gameRoot, "romfs", "arc", "data.trpfd");

        if (File.Exists(p1)) _trpfdPath = p1;
        else if (File.Exists(p2)) _trpfdPath = p2;
        else throw new FileNotFoundException($"data.trpfd not found under '{_gameRoot}\\arc\\' or '{(_gameRoot + "\\romfs\\arc\\")}'.");
    }

    private static FileDescriptor Load(string trpfdPath)
    {
        var fd = FlatIo.Load<FileDescriptor>(trpfdPath);
        FdFixer.Fix(fd);
        return fd;
    }

    private void MoveToUnused(FileDescriptor fd, ulong hash, List<string>? moves)
    {
        var idx = fd.FileHashes.IndexOf(hash);
        if (idx < 0) return;
        var fi = fd.FileInfo[idx];

        fd.FileHashes.RemoveAt(idx);
        fd.FileInfo.RemoveAt(idx);

        fd.UnusedHashes.Add(hash);
        fd.UnusedFileInfo.Add(fi);

        _log($"[remove] 0x{hash:X16}");
        moves?.Add($"removed mapping 0x{hash:X16}");
    }

    private void MoveBack(FileDescriptor fd, ulong hash, List<string>? moves)
    {
        var idx = fd.UnusedHashes.IndexOf(hash);
        if (idx < 0) return;

        var fi = fd.UnusedFileInfo[idx];
        fd.UnusedHashes.RemoveAt(idx);
        fd.UnusedFileInfo.RemoveAt(idx);

        var pos = BinarySearchInsertPos(fd.FileHashes, hash);
        fd.FileHashes.Insert(pos, hash);
        fd.FileInfo.Insert(pos, fi);

        _log($"[restore] 0x{hash:X16}");
        moves?.Add($"restored mapping 0x{hash:X16}");
    }

    private static void SortPrimary(FileDescriptor fd)
    {
        var pairs = fd.FileHashes.Select((h, i) => (h, fd.FileInfo[i])).OrderBy(t => t.h).ToArray();
        fd.FileHashes = pairs.Select(p => p.h).ToList();
        fd.FileInfo = pairs.Select(p => p.Item2).ToList();
    }

    private IEnumerable<ulong> EnumerateLooseHashes()
    {
        foreach (var f in Directory.EnumerateFiles(_romfsRoot, "*", SearchOption.AllDirectories))
        {
            if (f.EndsWith(".bak", StringComparison.OrdinalIgnoreCase)) continue;
            var key = PathRules.ToGameKey(f, _romfsRoot, _stripPrefix);
            yield return GfFnv1a64.Hash(key);
        }
    }

    private void SaveWithOptionalBackup(FileDescriptor fd)
    {
        if (_autoBackup && File.Exists(_trpfdPath))
        {
            var ts = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            var backup = _trpfdPath + ".bak-" + ts;
            File.Copy(_trpfdPath, backup, overwrite: false);
            _lastBackupPath = backup;
            _log("[backup] " + backup);
        }
        FlatIo.Save(fd, _trpfdPath);
        _log("[save] " + _trpfdPath);
    }

    private static int BinarySearchInsertPos(IList<ulong> list, ulong value)
    {
        var i = BinarySearch(list, value);
        return i < 0 ? ~i : i;
    }
    private static int BinarySearch(IList<ulong> list, ulong value)
    {
        int lo = 0, hi = list.Count - 1;
        while (lo <= hi)
        {
            int mid = lo + ((hi - lo) >> 1);
            ulong mv = list[mid];
            if (mv == value) return mid;
            if (mv < value) lo = mid + 1;
            else hi = mid - 1;
        }
        return ~lo;
    }
}

public sealed class WatchHandle : IDisposable
{
    private readonly FileSystemWatcher _fsw;
    private readonly Action _cleanup;
    internal WatchHandle(FileSystemWatcher fsw, Action cleanup) { _fsw = fsw; _cleanup = cleanup; }
    public void Dispose() { try { _cleanup(); } catch { } }
}