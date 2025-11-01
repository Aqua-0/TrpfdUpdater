using System.IO;
namespace TrpfdManager.Core;
internal static class PathRules
{
    public static string ToGameKey(string fullPath, string romfsRoot, string stripPrefix)
    {
        var rel = Path.GetRelativePath(romfsRoot, fullPath).Replace('\\', '/');
        if (!string.IsNullOrEmpty(stripPrefix))
        {
            var p = stripPrefix.Replace('\\', '/').Trim('/');
            if (rel.StartsWith(p + "/", System.StringComparison.OrdinalIgnoreCase))
                rel = rel[(p.Length + 1)..];
        }
        return rel.TrimStart('/');
    }
}