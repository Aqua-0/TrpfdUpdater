namespace TrpfdManager;

public sealed class Settings
{
    // UI
    public bool IsDark { get; set; } = true;
    public bool ShowLog { get; set; } = true;
    public double LogHeight { get; set; } = 220;
    public int LastTabIndex { get; set; } = 1; // default to Mods Merger

    // Single
    public string? GameRoot { get; set; }
    public string? RomfsRoot { get; set; }
    public string? StripPrefix { get; set; }
    public bool Watch { get; set; }
    public bool DryRun { get; set; }
    public bool AutoBackup { get; set; }

    // Overlay
    public string? PacksRoot { get; set; }
    public string? BaseTrpfd { get; set; }
    public string? OutputRoot { get; set; }
    public bool OverlayNeutralize { get; set; }
    public bool OverlayAutoApply { get; set; }

    // Mods Merger
    public string? ModsRoot { get; set; }
    public string? ModsTargetRomfs { get; set; }
}