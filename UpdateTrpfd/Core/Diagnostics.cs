using System;
using System.Collections.Generic;
using System.IO;

namespace TrpfdManager.Core;

internal static class Diagnostics
{
    public static IEnumerable<string> TestConfig(TrpfdService svc)
    {
        var lines = new List<string>();
        try
        {
            lines.Add("[diag] starting validation");
            svc.DebugEchoConfig(lines);
            var fdPath = svc.DebugResolveTrpfd();
            lines.Add("[diag] resolved TRPFD = " + fdPath);
            lines.Add("[diag] TRPFD exists: " + File.Exists(fdPath));
            lines.Add("[diag] ROMFS exists: " + Directory.Exists(svc.DebugRomfsRoot));
            lines.Add("[diag] listing first 5 files under ROMFS:");
            int count = 0;
            foreach (var f in Directory.EnumerateFiles(svc.DebugRomfsRoot, "*", SearchOption.AllDirectories))
            {
                lines.Add(" - " + f);
                if (++count >= 5) break;
            }
            lines.Add("[diag] OK");
        }
        catch (Exception ex)
        {
            lines.Add("[diag-error] " + ex.ToString());
        }
        return lines;
    }
}