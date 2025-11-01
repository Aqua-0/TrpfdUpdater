using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TrpfdManager.Core;

public static class RomfsLayoutFixer
{
    // Add or edit as needed
    public static readonly string[] KnownRomfsRoots = new[]
    {
        "ai_influence","avalon","field","ik_ai_behavior","ik_chara","ik_demo",
        "ik_effect","ik_event","ik_message","ik_pokemon","light","param_ai",
        "param_chr","script","system_resource","system","ui","world"
    };

    public static bool HasRecognizedRoots(string packDir) =>
        KnownRomfsRoots.Any(r => Directory.Exists(Path.Combine(packDir, r)));

    public static int FixPacks(string packsRoot, IEnumerable<string> packNames, bool dryRun, Action<string> log)
    {
        int fixedCount = 0;
        foreach (var name in packNames)
        {
            var packDir = Path.Combine(packsRoot, name);
            if (!Directory.Exists(packDir)) continue;

            var romfs = Path.Combine(packDir, "romfs");
            if (Directory.Exists(romfs))
            {
                log($"[fix] skip: {name} already has romfs/");
                continue;
            }

            var roots = KnownRomfsRoots
                .Select(r => (root: r, src: Path.Combine(packDir, r)))
                .Where(t => Directory.Exists(t.src))
                .ToList();

            if (roots.Count == 0) continue;

            log($"[fix] {name}: creating romfs/ and moving {roots.Count} root(s)");
            if (!dryRun) Directory.CreateDirectory(romfs);

            foreach (var (root, src) in roots)
            {
                var dst = Path.Combine(romfs, root);
                if (Directory.Exists(dst))
                {
                    log($"[fix] skip merge: romfs/{root} already exists in {name}");
                    continue;
                }
                if (!dryRun) Directory.Move(src, dst); // moves the whole tree
                log($"[fix] moved {root} -> romfs/{root}");
            }

            fixedCount++;
        }
        return fixedCount;
    }
}