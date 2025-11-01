using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace TrpfdManager.Core;

internal static class ModScanner
{
    private static readonly HashSet<string> KnownTopDirs = new(StringComparer.OrdinalIgnoreCase)
        {
            "ai_influence","avalon","field","ik_ai_behavior","ik_chara","ik_demo","ik_effect",
            "ik_event","ik_message","ik_pokemon","light","param_ai","param_chr","script",
            "system_resource","system","ui","world","arc"
        };

    public static string[] ScanFolder(string modRoot)
    {
        if (string.IsNullOrWhiteSpace(modRoot) || !Directory.Exists(modRoot))
            return Array.Empty<string>();

        var root = Path.GetFullPath(modRoot);
        var results = new List<string>(256);

        foreach (var abs in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(root, abs).Replace('\\', '/');

            if (ShouldSkip(rel))
                continue;

            var norm = NormalizeToRomfs(rel);
            if (norm is null) continue;

            // require top-level dir to be known
            var tail = norm.Substring("romfs/".Length);
            var top = tail.Split('/', 2)[0];
            if (KnownTopDirs.Contains(top))
                results.Add(norm);
        }

        return results.ToArray();
    }

    public static string[] ScanZip(string zipPath)
    {
        if (string.IsNullOrWhiteSpace(zipPath) || !File.Exists(zipPath))
            return Array.Empty<string>();

        var list = new List<string>(256);
        using var z = ZipFile.OpenRead(zipPath);
        foreach (var e in z.Entries)
        {
            // skip folders
            if (string.IsNullOrEmpty(e.Name))
                continue;

            var rel = e.FullName.Replace('\\', '/');

            if (ShouldSkip(rel))
                continue;

            var norm = NormalizeToRomfs(rel);
            if (norm is null) continue;

            var tail = norm.Substring("romfs/".Length);
            var top = tail.Split('/', 2)[0];
            if (KnownTopDirs.Contains(top))
                list.Add(norm);
        }
        return list.ToArray();
    }

    // --- helpers ---

    private static bool ShouldSkip(string rel)
    {
        var name = Path.GetFileName(rel).Replace('\\', '/');

        if (rel.StartsWith("__MACOSX/", StringComparison.OrdinalIgnoreCase))
            return true; // macOS resource fork folder

        if (name.Equals(".DS_Store", StringComparison.OrdinalIgnoreCase))
            return true; // macOS metadata file

        if (name.StartsWith("._", StringComparison.Ordinal)) // resource fork files
            return true;

        if (rel.EndsWith(".trpfd", StringComparison.OrdinalIgnoreCase))
            return true; // skip all TRPFDs

        if (rel.Equals("arc/data.trpfd", StringComparison.OrdinalIgnoreCase) ||
            rel.EndsWith("/arc/data.trpfd", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    private static string? NormalizeToRomfs(string rel)
    {
        // prefer the deepest romfs/ anchor
        var r = rel.Replace('\\', '/');
        var rLower = r.ToLowerInvariant();

        var lastRomfs = rLower.LastIndexOf("romfs/", StringComparison.Ordinal);
        if (lastRomfs >= 0)
        {
            var tail = r.Substring(lastRomfs).TrimStart('/'); // starts with "romfs/"
                                                              // collapse repeated romfs/romfs/
            while (tail.StartsWith("romfs/", StringComparison.OrdinalIgnoreCase))
            {
                var idx = tail.IndexOf('/', 6);
                if (idx < 0) break;
                // if next segment is again "romfs", drop the first
                if (tail.Length >= 12 && tail.Substring(6, 6).Equals("romfs/", StringComparison.OrdinalIgnoreCase))
                    tail = tail.Substring(6);
                else
                    break;
            }
            if (!tail.StartsWith("romfs/", StringComparison.OrdinalIgnoreCase))
                tail = "romfs/" + tail.TrimStart('/');
            return tail;
        }

        // else: find first known top-level folder and prepend romfs/
        var parts = r.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length; i++)
        {
            if (KnownTopDirs.Contains(parts[i]))
            {
                var tail = string.Join('/', parts.Skip(i));
                return "romfs/" + tail;
            }
        }
        return null;
    }
}

internal static class ConflictReporter
{
    public static List<(string Path, List<string> Packs)> FindConflicts(IEnumerable<(string PackName, string[] Files)> inOrder)
    {
        var map = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var (pack, files) in inOrder)
        {
            foreach (var rf in files)
            {
                // normalize to path relative to romfs/
                var rel = rf.StartsWith("romfs/", StringComparison.OrdinalIgnoreCase) ? rf[6..] : rf;
                if (!map.TryGetValue(rel, out var list)) map[rel] = list = new List<string>(1);
                if (list.Count == 0 || !list[^1].Equals(pack, StringComparison.OrdinalIgnoreCase)) list.Add(pack);
            }
        }

        return map.Where(kv => kv.Value.Count > 1)
                  .Select(kv => (kv.Key, kv.Value))
                  .OrderBy(t => t.Key, StringComparer.OrdinalIgnoreCase)
                  .ToList();
    }
}

internal static class ModCopier
{
    public static void CopySelected(string targetRomfs, IEnumerable<(string PackName, bool IsZip, string FullPath, string[] Files)> inOrder, bool dryRun, Action<string> log)
    {
        if (string.IsNullOrWhiteSpace(targetRomfs)) { log("[mods] target romfs not set."); return; }
        var normTarget = Path.GetFullPath(targetRomfs);
        Directory.CreateDirectory(normTarget);

        int total = 0, overwritten = 0, skipped = 0;

        foreach (var (pack, isZip, fullPath, files) in inOrder)
        {
            foreach (var rf in files)
            {
                var rel = rf.StartsWith("romfs/", StringComparison.OrdinalIgnoreCase) ? rf[6..] : rf;
                var dest = Path.GetFullPath(Path.Combine(normTarget, rel));
                if (!dest.StartsWith(normTarget, StringComparison.OrdinalIgnoreCase)) { skipped++; continue; }

                if (dryRun) { total++; continue; }
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);

                if (isZip)
                {
                    using var z = ZipFile.OpenRead(fullPath);
                    var entry = z.Entries.FirstOrDefault(e => MatchesEntry(e, rf));
                    if (entry == null) { skipped++; continue; }
                    if (File.Exists(dest)) overwritten++;
                    using var src = entry.Open();
                    using var fs = File.Create(dest);
                    src.CopyTo(fs);
                }
                else
                {
                    var srcAbs = ResolveFolderSource(fullPath, rf);
                    if (srcAbs == null) { skipped++; continue; }
                    if (File.Exists(dest)) overwritten++;
                    File.Copy(srcAbs, dest, overwrite: true);
                }
                total++;
            }
        }

        log($"[mods] copy done. wrote {total} file(s), overwritten {overwritten}, skipped {skipped}.");
    }

    private static bool MatchesEntry(ZipArchiveEntry e, string romfsRel)
    {
        var eRel = e.FullName.Replace('\\', '/');
        if (eRel.StartsWith("romfs/", StringComparison.OrdinalIgnoreCase))
            return eRel.Equals(romfsRel, StringComparison.OrdinalIgnoreCase);

        var tail = romfsRel.StartsWith("romfs/", StringComparison.OrdinalIgnoreCase) ? romfsRel[6..] : romfsRel;
        return eRel.EndsWith(tail, StringComparison.OrdinalIgnoreCase);
    }

    private static string? ResolveFolderSource(string modRoot, string romfsRel)
    {
        var abs1 = Path.Combine(modRoot, romfsRel.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(abs1)) return abs1;

        var tail = (romfsRel.StartsWith("romfs/", StringComparison.OrdinalIgnoreCase) ? romfsRel[6..] : romfsRel)
                   .Replace('/', Path.DirectorySeparatorChar);
        var abs2 = Path.Combine(modRoot, tail);
        if (File.Exists(abs2)) return abs2;

        var name = Path.GetFileName(tail);
        return Directory.EnumerateFiles(modRoot, name, SearchOption.AllDirectories)
                        .FirstOrDefault(p => p.EndsWith(tail, StringComparison.OrdinalIgnoreCase));
    }
}