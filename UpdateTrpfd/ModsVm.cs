using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TrpfdManager;

public sealed class ModsVm : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private string _modsRoot = "";
    private string _targetRomfs = "";
    private ModPackVm? _selected;

    public string ModsRoot { get => _modsRoot; set { _modsRoot = value ?? ""; OnChanged(); } }
    public string TargetRomfs { get => _targetRomfs; set { _targetRomfs = value ?? ""; OnChanged(); } }
    public ObservableCollection<ModPackVm> Packs { get; } = new();
    public ModPackVm? Selected { get => _selected; set { _selected = value; OnChanged(); } }

    private void OnChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new(n));
}

public sealed class ModPackVm : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public required string Name { get; init; }
    public required string FullPath { get; init; } // folder or .zip
    public required bool IsZip { get; init; }
    public int RecognizedCount { get; set; }

    private bool _isEnabled = true;
    public bool IsEnabled { get => _isEnabled; set { _isEnabled = value; PropertyChanged?.Invoke(this, new(nameof(IsEnabled))); } }
}
