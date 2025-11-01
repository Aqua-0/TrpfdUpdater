using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.IO;

namespace TrpfdManager.Core;

public sealed class OverlayVm : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private string _packsRoot = "";
    private string _baseTrpfd = "";
    private string _outputRoot = "";
    private bool _autoApply;
    private bool _neutralize;

    public string PacksRoot { get => _packsRoot; set { _packsRoot = value ?? ""; OnChanged(); } }
    public string BaseTrpfd { get => _baseTrpfd; set { _baseTrpfd = value ?? ""; OnChanged(); } }
    public string OutputRoot { get => _outputRoot; set { _outputRoot = value ?? ""; OnChanged(); } }
    public bool AutoApply { get => _autoApply; set { _autoApply = value; OnChanged(); } }
    public bool Neutralize { get => _neutralize; set { _neutralize = value; OnChanged(); } }

    public ObservableCollection<PackItem> Packs { get; } = new();
    public PackItem? SelectedPack { get; set; }
    public void ScanPacks()
    {
        Packs.Clear();
        if (!Directory.Exists(PacksRoot)) return;

        foreach (var dir in Directory.EnumerateDirectories(PacksRoot))
        {
            var name = Path.GetFileName(dir)!;
            var hasRomfs = Directory.Exists(Path.Combine(dir, "romfs"));
            var hasKnownRoots = RomfsLayoutFixer.HasRecognizedRoots(dir);

            // show packs that already have romfs OR have any recognized root directly under pack
            if (hasRomfs || hasKnownRoots)
            {
                Packs.Add(new PackItem(name, enabled: true)
                {
                    NeedsLayoutFix = !hasRomfs && hasKnownRoots
                });
            }
        }
    }
    public void SetAll(bool on) { foreach (var p in Packs) p.IsEnabled = on; OnChanged(nameof(Packs)); }
    public void MoveSelected(int delta)
    {
        if (SelectedPack is null) return;
        var i = Packs.IndexOf(SelectedPack); var j = i + delta;
        if (i < 0 || j < 0 || j >= Packs.Count) return;
        Packs.Move(i, j);
    }
    private void OnChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new(n));
}