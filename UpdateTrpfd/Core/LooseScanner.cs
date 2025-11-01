using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TrpfdManager.Core;

/// Recognizes loose files under a romfs folder and returns paths starting at "romfs/".
internal static class LooseScanner
{
    private static readonly HashSet<string> KnownTopDirs = new(StringComparer.OrdinalIgnoreCase)
    {
        "ai_influence","avalon","field","ik_ai_behavior","ik_chara","ik_demo","ik_effect",
        "ik_event","ik_message","ik_pokemon","light","param_ai","param_chr","script",
        "system_resource","system","ui","world","arc"
    };

    public static string[] ScanRecognized(string romfsRoot)
    {
        if (string.IsNullOrWhiteSpace(romfsRoot) || !Directory.Exists(romfsRoot))
            return Array.Empty<string>();

        var root = Path.GetFullPath(romfsRoot);
        var outList = new List<string>(256);

        foreach (var abs in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            // relative to romfs
            var rel = Path.GetRelativePath(root, abs).Replace('\\', '/');

            // skip descriptor
            if (rel.Equals("arc/data.trpfd", StringComparison.OrdinalIgnoreCase))
                continue;

            // only known top-level dirs
            var top = rel.Split('/', 2)[0];
            if (!KnownTopDirs.Contains(top))
                continue;

            // ensure the printed path starts at "romfs/"
            outList.Add($"romfs/{rel}");
        }

        return outList.ToArray();
    }
}