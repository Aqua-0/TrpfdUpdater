using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace TrpfdManager.Core;
public static class OverlayNeutralizer
{
    private const string MarkerName = ".neutralized.trpfd.json";

    public static void Neutralize(string packsRoot, IEnumerable<string> selectedNames, string outputRoot, System.Action<string> log)
    {
        var actions = new List<(string Original, string Disabled)>();
        foreach (var name in selectedNames)
        {
            var trpfd = Path.Combine(packsRoot, name, "romfs", "arc", "data.trpfd");
            if (!File.Exists(trpfd)) continue;
            var disabled = trpfd + ".disabled_by_updater";
            if (!File.Exists(disabled))
            {
                File.Move(trpfd, disabled);
                actions.Add((trpfd, disabled));
                log("[neutralize] " + trpfd);
            }
        }
        if (actions.Count > 0)
        {
            Directory.CreateDirectory(outputRoot);
            File.WriteAllText(Path.Combine(outputRoot, MarkerName), JsonSerializer.Serialize(actions));
        }
    }

    public static void Restore(string outputRoot, System.Action<string> log)
    {
        var marker = Path.Combine(outputRoot, MarkerName);
        if (!File.Exists(marker)) { log("[restore] no marker"); return; }
        var actions = JsonSerializer.Deserialize<List<Neutralized>>(File.ReadAllText(marker)) ?? new();
        foreach (var a in actions)
        {
            if (File.Exists(a.Disabled) && !File.Exists(a.Original))
            {
                File.Move(a.Disabled, a.Original);
                log("[restore] " + a.Original);
            }
        }
        File.Delete(marker);
    }
    private record Neutralized(string Original, string Disabled);
}