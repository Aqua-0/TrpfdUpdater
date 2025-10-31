using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using TrpfdManager.Core;

namespace TrpfdManager;
public sealed class MainVm : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private string _gameRoot = "";
    private string _romfsRoot = "";
    private string _stripPrefix = "";
    private bool _watch;
    private bool _dryRun;
    private bool _autoBackup;
    private string _status = "idle";
    private string _resolvedTrpfdPath = "";
    private string _lastBackupPath = "";
    private WatchHandle? _watcher;

    public string GameRoot { get => _gameRoot; set { _gameRoot = value; OnChanged(); } }
    public string RomfsRoot { get => _romfsRoot; set { _romfsRoot = value; OnChanged(); } }
    public string StripPrefix { get => _stripPrefix; set { _stripPrefix = value; OnChanged(); } }
    public bool Watch { get => _watch; set { _watch = value; OnChanged(); } }
    public bool DryRun { get => _dryRun; set { _dryRun = value; OnChanged(); } }
    public bool AutoBackup { get => _autoBackup; set { _autoBackup = value; OnChanged(); } }
    public string Status { get => _status; set { _status = value; OnChanged(); } }
    public string ResolvedTrpfdPath { get => _resolvedTrpfdPath; set { _resolvedTrpfdPath = value; OnChanged(); } }
    public string LastBackupPath { get => _lastBackupPath; set { _lastBackupPath = value; OnChanged(); } }

    public ObservableCollection<string> Log { get; } = new();

    public void LogAdd(string msg)
    {
        foreach (var line in msg.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            Log.Add($"[{DateTime.Now:HH:mm:ss}] {line}");
    }

    public void StartWatcher(TrpfdService service)
    {
        _watcher?.Dispose();
        _watcher = service.StartWatcher();
        LogAdd("[watch] started");
    }

    public void StopWatcher()
    {
        _watcher?.Dispose();
        _watcher = null;
        LogAdd("[watch] stopped");
    }

    private void OnChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}