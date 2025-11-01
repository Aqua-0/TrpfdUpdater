using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TrpfdManager.Core;

public static class OverlayService
{
    public static IEnumerable<string> BuildOverlay(string packsRoot, IEnumerable<string> orderedPackNames)
    {
        var seen = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

        foreach (var name in orderedPackNames)
        {
            var packDir = Path.Combine(packsRoot, name);
            var romfs = Path.Combine(packDir, "romfs");
            if (Directory.Exists(romfs))
            {
                foreach (var full in Directory.EnumerateFiles(romfs, "*", SearchOption.AllDirectories))
                {
                    var rel = Path.GetRelativePath(romfs, full).Replace('\\', '/');
                    if (rel.EndsWith(".trpfd", System.StringComparison.OrdinalIgnoreCase)) continue;
                    if (seen.Add(rel)) yield return full;
                }
                continue;
            }

            // No romfs, but allow known roots directly under pack
            if (RomfsLayoutFixer.HasRecognizedRoots(packDir))
            {
                foreach (var root in RomfsLayoutFixer.KnownRomfsRoots)
                {
                    var rootDir = Path.Combine(packDir, root);
                    if (!Directory.Exists(rootDir)) continue;

                    foreach (var full in Directory.EnumerateFiles(rootDir, "*", SearchOption.AllDirectories))
                    {
                        var relUnderRoot = Path.GetRelativePath(packDir, full).Replace('\\', '/'); // e.g. "ik_chara/..."
                        if (seen.Add(relUnderRoot)) yield return full;
                    }
                }
            }
        }
    }
}