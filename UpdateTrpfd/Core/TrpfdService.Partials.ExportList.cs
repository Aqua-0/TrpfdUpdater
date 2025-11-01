using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace TrpfdManager.Core;
public sealed partial class TrpfdService
{
    public string ExportCsv()
    {
        var path = ResolveTrpfdPath();
        var fd = Load(path);
        var dir = Path.GetDirectoryName(path) ?? AppContext.BaseDirectory;
        var csv = Path.Combine(dir, $"data.trpfd-entries-{DateTime.Now:yyyyMMdd-HHmmss}.csv");
        using var w = new StreamWriter(csv, false);
        w.WriteLine("HashHex,PackIndex,PackName,IsUnused");
        for (int i = 0; i < fd.FileHashes.Count; i++)
        {
            var h = fd.FileHashes[i];
            var fi = fd.FileInfo[i];
            long packIdx = (long)(fi?.PackIndex ?? 0UL);
            string packName = (packIdx >= 0 && packIdx < fd.PackNames.Count) ? (fd.PackNames[(int)packIdx] ?? "") : "";
            w.WriteLine($"0x{h:X16},{packIdx},\"{packName.Replace("\"", "\"\"")}\",0");
        }
        for (int i = 0; i < fd.UnusedHashes.Count; i++)
        {
            var h = fd.UnusedHashes[i];
            var fi = fd.UnusedFileInfo[i];
            long packIdx = (long)(fi?.PackIndex ?? 0UL);
            string packName = (packIdx >= 0 && packIdx < fd.PackNames.Count) ? (fd.PackNames[(int)packIdx] ?? "") : "";
            w.WriteLine($"0x{h:X16},{packIdx},\"{packName.Replace("\"", "\"\"")}\",1");
        }
        _log("[csv] wrote " + csv);
        return csv;
    }

    public IEnumerable<string> ListRecognizedLooseFiles(int limit = 500)
    {
        ResolveTrpfdPath();
        var fd = Load(_trpfdPath);
        int shown = 0, total = 0;
        yield return "[scan] romfs = " + _romfsRoot;
        foreach (var f in Directory.EnumerateFiles(_romfsRoot, "*", SearchOption.AllDirectories))
        {
            if (f.EndsWith(".bak", StringComparison.OrdinalIgnoreCase)) continue;
            total++;
            var rel = PathRules.ToGameKey(f, _romfsRoot, _stripPrefix);
            var h = GfFnv1a64.Hash(rel);
            string state = fd.UnusedHashes.Contains(h) ? "override" : fd.FileHashes.Contains(h) ? "present-but-mapped" : "unknown";
            if (shown < limit) { shown++; yield return $"[{state}] {rel}  hash=0x{h:X16}"; }
        }
        yield return $"[summary] files={total} showing={Math.Min(total, shown)}";
    }
}