namespace TrpfdManager.Core;
public sealed partial class TrpfdService
{
    internal string DebugRomfsRoot => _romfsRoot;
    internal void DebugEchoConfig(System.Collections.Generic.List<string> sink)
    {
        sink.Add("[cfg] GameRoot=" + _gameRoot);
        sink.Add("[cfg] RomfsRoot=" + _romfsRoot);
        sink.Add("[cfg] StripPrefix=" + _stripPrefix);
        sink.Add("[cfg] AutoBackup=" + _autoBackup);
    }
    internal string DebugResolveTrpfd() => ResolveTrpfdPath();
}