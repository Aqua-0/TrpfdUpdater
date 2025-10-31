using System;
using System.IO;

namespace TrpfdManager.Core;
internal static class PathRules
{
    public static string ToGameKey(string fullPath, string romfsRoot, string stripPrefix)
    {
        if (string.IsNullOrWhiteSpace(fullPath))
            throw new ArgumentException("fullPath is empty in ToGameKey.");
        if (string.IsNullOrWhiteSpace(romfsRoot))
            throw new ArgumentException("Romfs Root is empty in ToGameKey.");

        var rel = Path.GetRelativePath(romfsRoot, fullPath).Replace('\\', '/');

        if (!string.IsNullOrEmpty(stripPrefix))
        {
            var p = stripPrefix.Replace('\\', '/').Trim('/');
            if (rel.StartsWith(p + "/", StringComparison.OrdinalIgnoreCase))
                rel = rel[(p.Length + 1)..];
        }
        return rel.TrimStart('/');
    }
}