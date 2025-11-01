using System.IO;

namespace TrpfdManager.Core;
public sealed partial class TrpfdService
{
    public WatchHandle StartWatcher()
    {
        ResolveTrpfdPath();
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
            catch { }
        };
        FileSystemEventHandler onDelete = (_, e) =>
        {
            try { if (!Directory.Exists(e.FullPath)) ApplyChange(fd, e.FullPath, present: false); } catch { }
        };
        RenamedEventHandler onRename = (_, e) =>
        {
            try
            {
                if (!string.IsNullOrEmpty(e.OldFullPath)) ApplyChange(fd, e.OldFullPath, present: false);
                if (File.Exists(e.FullPath)) ApplyChange(fd, e.FullPath, present: true);
            }
            catch { }
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
        if (present && fd.FileHashes.Contains(h)) MoveToUnused(fd, h, null);
        else if (!present && fd.UnusedHashes.Contains(h)) MoveBack(fd, h, null);
        SortPrimary(fd);
        SaveWithOptionalBackup(fd, _trpfdPath);
    }
}

public sealed class WatchHandle : System.IDisposable
{
    private readonly FileSystemWatcher _fsw;
    private readonly System.Action _cleanup;
    internal WatchHandle(FileSystemWatcher fsw, System.Action cleanup) { _fsw = fsw; _cleanup = cleanup; }
    public void Dispose() { try { _cleanup(); } catch { } }
}