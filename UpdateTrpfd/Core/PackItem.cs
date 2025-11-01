using System.ComponentModel;

namespace TrpfdManager.Core;

public sealed class PackItem : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public string Name { get; }
    private bool _isEnabled;
    public bool IsEnabled { get => _isEnabled; set { _isEnabled = value; PropertyChanged?.Invoke(this, new(nameof(IsEnabled))); } }

    // mark packs that need a romfs/ move
    public bool NeedsLayoutFix { get; set; }

    public PackItem(string name, bool enabled) { Name = name; _isEnabled = enabled; }
}