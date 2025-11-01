using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace TrpfdManager
{
    public partial class MainWindow : Window
    {
        private readonly MainVm _vm = new();
        private Settings _settings = new();
        private double _savedLogHeight = 220;
        private static string DefaultModsRoot =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TrpfdManager", "mods");
        public MainWindow()
        {
            InitializeComponent();
            DataContext = _vm;

            _settings = SettingsService.Load();
            ApplySettingsToVm(_settings);

            ThemeManager.Apply(_vm.IsDark);
            ApplyTitleBarTheme(_vm.IsDark);

            _savedLogHeight = Math.Max(120, _settings.LogHeight);
            LogRow.Height = _vm.ShowLog ? new GridLength(_savedLogHeight) : new GridLength(0);

            _vm.PropertyChanged += VmOnPropertyChanged;
            LogContainer.SizeChanged += (_, __) => { if (_vm.ShowLog && LogRow.ActualHeight > 0) _savedLogHeight = LogRow.ActualHeight; };

            Loaded += (_, __) => _vm.LogAdd("ready");
            Closing += OnClosing;
        }
        private void ApplySettingsToVm(Settings s)
        {
            _vm.IsDark = s.IsDark;
            _vm.ShowLog = s.ShowLog;
            _vm.SelectedTabIndex = s.LastTabIndex; // NEW

            _vm.GameRoot = s.GameRoot ?? "";
            _vm.RomfsRoot = s.RomfsRoot ?? "";
            _vm.StripPrefix = s.StripPrefix ?? "";
            _vm.Watch = s.Watch;
            _vm.DryRun = s.DryRun;
            _vm.AutoBackup = s.AutoBackup;

            _vm.Overlay.PacksRoot = s.PacksRoot ?? "";
            _vm.Overlay.BaseTrpfd = s.BaseTrpfd ?? "";
            _vm.Overlay.OutputRoot = s.OutputRoot ?? "";
            _vm.Overlay.Neutralize = s.OverlayNeutralize;
            _vm.Overlay.AutoApply = s.OverlayAutoApply;

            _vm.Mods.ModsRoot = string.IsNullOrWhiteSpace(s.ModsRoot)
                ? System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TrpfdManager", "mods")
                : s.ModsRoot;
            _vm.Mods.TargetRomfs = s.ModsTargetRomfs ?? "";
        }
        private void ModsChooseRoot_Click(object? s, RoutedEventArgs e)
        {
            var picked = PickFolder(_vm.Mods.ModsRoot);
            if (picked is null) return;
            _vm.Mods.ModsRoot = picked;
            _vm.LogAdd($"[mods] root = {picked}");
        }
        private void ModsOpenFolder_Click(object? s, RoutedEventArgs e)
        {
            var root = string.IsNullOrWhiteSpace(_vm.Mods.ModsRoot) ? DefaultModsRoot : _vm.Mods.ModsRoot;
            _vm.Mods.ModsRoot = root;
            Directory.CreateDirectory(root);
            try
            {
                Process.Start(new ProcessStartInfo { FileName = "explorer.exe", Arguments = $"\"{root}\"", UseShellExecute = true });
            }
            catch { _vm.LogAdd("[mods] open folder failed."); }
        }
        private Settings CaptureSettings() => new Settings
        {
            IsDark = _vm.IsDark,
            ShowLog = _vm.ShowLog,
            LogHeight = Math.Max(0, _savedLogHeight),
            LastTabIndex = _vm.SelectedTabIndex, // NEW

            GameRoot = _vm.GameRoot,
            RomfsRoot = _vm.RomfsRoot,
            StripPrefix = _vm.StripPrefix,
            Watch = _vm.Watch,
            DryRun = _vm.DryRun,
            AutoBackup = _vm.AutoBackup,

            PacksRoot = _vm.Overlay.PacksRoot,
            BaseTrpfd = _vm.Overlay.BaseTrpfd,
            OutputRoot = _vm.Overlay.OutputRoot,
            OverlayNeutralize = _vm.Overlay.Neutralize,
            OverlayAutoApply = _vm.Overlay.AutoApply,

            ModsRoot = _vm.Mods.ModsRoot,
            ModsTargetRomfs = _vm.Mods.TargetRomfs,
        };
        private async void ModsSummary_Click(object? sender, RoutedEventArgs e)
        {
            var selected = _vm.Mods.Packs.Where(p => p.IsEnabled).ToList();
            if (selected.Count == 0) { _vm.LogAdd("[mods] no mods selected."); return; }

            _vm.LogAdd("[mods] building summary…");
            // Scan off the UI thread
            var scanned = await Task.Run(() =>
                selected.Select(p =>
                {
                    var files = p.IsZip
                        ? Core.ModScanner.ScanZip(p.FullPath)
                        : Core.ModScanner.ScanFolder(p.FullPath);

                    var trimmed = files.Select(f => f.StartsWith("romfs/", System.StringComparison.OrdinalIgnoreCase) ? f[6..] : f)
                                       .ToArray();
                    return (p.Name, Files: trimmed);
                }).ToList());

            // Build path → ordered pack list
            var map = new Dictionary<string, List<string>>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var item in scanned)
            {
                var pack = item.Name;
                var files = item.Files;

                foreach (var rel in files)
                {
                    if (!map.TryGetValue(rel, out var list)) map[rel] = list = new System.Collections.Generic.List<string>(1);
                    if (list.Count == 0 || !list[^1].Equals(pack, StringComparison.OrdinalIgnoreCase)) list.Add(pack);
                }
            }

            // Compute wins/loses per pack
            var win = selected.ToDictionary(p => p.Name, _ => 0, System.StringComparer.OrdinalIgnoreCase);
            var lose = selected.ToDictionary(p => p.Name, _ => 0, System.StringComparer.OrdinalIgnoreCase);

            foreach (var kv in map)
            {
                var chain = kv.Value;
                if (chain.Count == 0) continue;
                var last = chain[^1];
                win[last]++;
                if (chain.Count > 1)
                {
                    foreach (var pk in chain.Take(chain.Count - 1))
                        lose[pk]++;
                }
            }

            var items = scanned
                .Select(t => new PackSummaryItem
                {
                    Name = t.Name,
                    FileCount = t.Files.Length,
                    Wins = win.TryGetValue(t.Name, out var w) ? w : 0,
                    Loses = lose.TryGetValue(t.Name, out var l) ? l : 0,
                    Files = t.Files.OrderBy(s => s, System.StringComparer.OrdinalIgnoreCase).ToList()
                })
                .OrderByDescending(i => i.Wins)
                .ThenByDescending(i => i.FileCount)
                .ThenBy(i => i.Name, System.StringComparer.OrdinalIgnoreCase)
                .ToList();

            new PackSummaryWindow(items).Show();
        }
        private async void ModsCopyAndBuild_Click(object? sender, RoutedEventArgs e)
        {
            // 1) Validate
            if (string.IsNullOrWhiteSpace(_vm.Mods.TargetRomfs))
            {
                _vm.LogAdd("[mods] choose a target romfs first.");
                return;
            }
            if (!File.Exists(_vm.Overlay.BaseTrpfd))
            {
                _vm.LogAdd("[mods] Overlay.BaseTrpfd not set. Pick a Base TRPFD on the Overlay tab.");
                return;
            }

            // 2) Prepare selected sequence in current order
            var selected = _vm.Mods.Packs.Where(p => p.IsEnabled).ToList();
            if (selected.Count == 0) { _vm.LogAdd("[mods] no mods selected."); return; }

            _vm.LogAdd($"[mods] copying {selected.Count} mod(s) → target…");
            var seq = selected.Select(p =>
            {
                var files = p.IsZip ? Core.ModScanner.ScanZip(p.FullPath) : Core.ModScanner.ScanFolder(p.FullPath);
                return (PackName: p.Name, IsZip: p.IsZip, FullPath: p.FullPath, Files: files);
            }).ToList();

            // 3) Copy files to target romfs
            Core.ModCopier.CopySelected(_vm.Mods.TargetRomfs, seq, _vm.DryRun, _vm.LogAdd);

            // 4) Build TRPFD in target romfs
            var targetRomfs = _vm.Mods.TargetRomfs;
            var arcDir = Path.Combine(targetRomfs, "arc");
            Directory.CreateDirectory(arcDir);
            var targetTrpfd = Path.Combine(arcDir, "data.trpfd");

            // Collect overlay from target romfs itself
            var overlayFiles = Core.LooseScanner.ScanRecognized(targetRomfs);

            _vm.LogAdd($"[mods] building TRPFD at {targetTrpfd} from {_vm.Overlay.BaseTrpfd} with {overlayFiles.Length} file(s)…");

            if (_vm.DryRun)
            {
                _vm.LogAdd("[mods] dry-run: TRPFD not written.");
                return;
            }

            // Use TrpfdService with this target romfs context
            var svc = new Core.TrpfdService(
                gameRoot: string.Empty,
                romfsRoot: targetRomfs,
                stripPrefix: string.Empty,
                autoBackup: _vm.AutoBackup,
                log: _vm.LogAdd
            );

            await Task.Run(() => svc.FullSyncFromOverlay(_vm.Overlay.BaseTrpfd, targetTrpfd, overlayFiles, _vm.DryRun));
            _vm.LogAdd("[mods] TRPFD build complete.");
        }
        private async void ModsListLoose_Click(object? sender, RoutedEventArgs e)
        {
            var mods = _vm.Mods;
            if (string.IsNullOrWhiteSpace(mods.ModsRoot) || !Directory.Exists(mods.ModsRoot))
            {
                _vm.LogAdd("[mods] ModsRoot not set or missing.");
                return;
            }

            var selected = mods.Packs.Where(p => p.IsEnabled).ToList();
            if (selected.Count == 0)
            {
                _vm.LogAdd("[mods] No mods selected.");
                return;
            }

            _vm.LogAdd($"[mods] scanning {selected.Count} enabled mod(s) for recognized loose files…");

            int total = 0;
            foreach (var pack in selected)
            {
                string[] list = await Task.Run(() =>
                    pack.IsZip
                        ? Core.ModScanner.ScanZip(pack.FullPath)
                        : Core.ModScanner.ScanFolder(pack.FullPath));

                // trim to after "romfs/"
                var trimmed = list.Select(f => f.StartsWith("romfs/", StringComparison.OrdinalIgnoreCase) ? f[6..] : f)
                                  .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                                  .ToArray();

                total += trimmed.Length;
                _vm.LogAdd($"[mods] {pack.Name}: {trimmed.Length} file(s)");
                foreach (var f in trimmed.Take(200))
                    _vm.LogAdd("  " + f);
                if (trimmed.Length > 200)
                    _vm.LogAdd($"  … +{trimmed.Length - 200} more");
            }

            _vm.LogAdd($"[mods] total recognized across selected: {total}");
        }
        private void OnClosing(object? sender, CancelEventArgs e)
        {
            try
            {
                if (_vm.ShowLog && LogRow.ActualHeight > 0) _savedLogHeight = LogRow.ActualHeight;
                _settings = CaptureSettings();
                SettingsService.Save(_settings);
            }
            catch { }
        }

        // ---------- Theme ----------
        private void VmOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainVm.ShowLog))
            {
                if (_vm.ShowLog) LogRow.Height = new GridLength(Math.Max(120, _savedLogHeight));
                else { _savedLogHeight = LogRow.ActualHeight > 0 ? LogRow.ActualHeight : _savedLogHeight; LogRow.Height = new GridLength(0); }
            }
            else if (e.PropertyName == nameof(MainVm.IsDark))
            {
                ThemeManager.Apply(_vm.IsDark);
                ApplyTitleBarTheme(_vm.IsDark);
            }
        }

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_19 = 19;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_20 = 20;
        [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
        private void ApplyTitleBarTheme(bool dark)
        {
            try
            {
                var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                int useDark = dark ? 1 : 0;
                _ = DwmSetWindowAttribute(handle, DWMWA_USE_IMMERSIVE_DARK_MODE_20, ref useDark, sizeof(int));
                _ = DwmSetWindowAttribute(handle, DWMWA_USE_IMMERSIVE_DARK_MODE_19, ref useDark, sizeof(int));
            }
            catch { }
        }
        private void DarkMode_Checked(object? sender, RoutedEventArgs e)
        {
            ThemeManager.Apply(_vm.IsDark);
            ApplyTitleBarTheme(_vm.IsDark);
        }

        // ---------- Common pickers ----------
        private static string? PickFolder(string? initial = null)
        {
            using var dlg = new WinForms.FolderBrowserDialog { ShowNewFolderButton = true, SelectedPath = string.IsNullOrWhiteSpace(initial) ? "" : initial! };
            return dlg.ShowDialog() == WinForms.DialogResult.OK ? dlg.SelectedPath : null;
        }
        private static string? PickFile(string filter, string? initialDir = null)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = filter, CheckFileExists = true };
            if (!string.IsNullOrWhiteSpace(initialDir) && Directory.Exists(initialDir)) dlg.InitialDirectory = initialDir;
            return dlg.ShowDialog() == true ? dlg.FileName : null;
        }

        // ---------- Overlay handlers ----------
        private void BrowsePacksRoot_Click(object? s, RoutedEventArgs e) { var p = PickFolder(_vm.Overlay.PacksRoot); if (p != null) { _vm.Overlay.PacksRoot = p; _vm.LogAdd($"[overlay] PacksRoot = {p}"); } }
        private void BrowseBaseTrpfd_Click(object? s, RoutedEventArgs e) { var p = PickFile("TRPFD|*.trpfd|All Files|*.*", Directory.Exists(_vm.Overlay.PacksRoot) ? _vm.Overlay.PacksRoot : null); if (p != null) { _vm.Overlay.BaseTrpfd = p; _vm.LogAdd($"[overlay] BaseTrpfd = {p}"); } }
        private void BrowseOutputRoot_Click(object? s, RoutedEventArgs e) { var p = PickFolder(_vm.Overlay.OutputRoot); if (p != null) { _vm.Overlay.OutputRoot = p; _vm.LogAdd($"[overlay] OutputRoot = {p}"); } }

        private void ScanPacks_Click(object? s, RoutedEventArgs e)
        {
            try { _vm.Overlay.GetType().GetMethod("ScanPacks")?.Invoke(_vm.Overlay, null); _vm.LogAdd("[overlay] scan complete."); }
            catch (Exception ex) { _vm.LogAdd("[overlay] scan error: " + ex.Message); }
        }

        private async void FixLayout_Click(object? s, RoutedEventArgs e)
        {
            try
            {
                var ov = _vm.Overlay;
                if (string.IsNullOrWhiteSpace(ov.PacksRoot) || !Directory.Exists(ov.PacksRoot)) { _vm.LogAdd("[overlay] PacksRoot missing."); return; }
                var fixer = Type.GetType("TrpfdManager.Core.RomfsLayoutFixer, UpdateTrpfd");
                var method = fixer?.GetMethod("FixPacks", new[] { typeof(string), typeof(string[]), typeof(bool), typeof(Action<string>) });
                if (method != null)
                {
                    var selected = ov.Packs.Where(p => p.IsEnabled).Select(p => p.Name).ToArray();
                    var count = (int)method.Invoke(null, new object[] { ov.PacksRoot, selected, _vm.DryRun, new Action<string>(_vm.LogAdd) })!;
                    _vm.LogAdd($"[overlay] layout fixed for {count} pack(s).");
                    ov.GetType().GetMethod("ScanPacks")?.Invoke(ov, null);
                    return;
                }
                // Fallback is omitted to avoid surprises.
                _vm.LogAdd("[overlay] no fixer available.");
            }
            catch (Exception ex) { _vm.LogAdd("[overlay] fix layout error: " + ex.Message); }
        }

        private void EnableAll_Click(object? s, RoutedEventArgs e) { foreach (var p in _vm.Overlay.Packs) p.IsEnabled = true; _vm.LogAdd("[overlay] enabled all."); }
        private void DisableAll_Click(object? s, RoutedEventArgs e) { foreach (var p in _vm.Overlay.Packs) p.IsEnabled = false; _vm.LogAdd("[overlay] disabled all."); }

        private void RestoreNeutralized_Click(object? s, RoutedEventArgs e)
        {
            try
            {
                var ov = _vm.Overlay; if (string.IsNullOrWhiteSpace(ov.PacksRoot) || !Directory.Exists(ov.PacksRoot)) { _vm.LogAdd("[overlay] PacksRoot missing."); return; }
                int restored = 0;
                foreach (var pack in ov.Packs.Select(p => p.Name))
                {
                    var arc = Path.Combine(ov.PacksRoot, pack, "romfs", "arc");
                    var bak = Path.Combine(arc, "data.trpfd.neutralized");
                    var live = Path.Combine(arc, "data.trpfd");
                    if (File.Exists(bak) && !File.Exists(live))
                    { if (_vm.DryRun) _vm.LogAdd($"[dry] restore {bak}"); else { Directory.CreateDirectory(arc); File.Move(bak, live); restored++; } }
                }
                _vm.LogAdd($"[overlay] restored {restored} TRPFD(s).");
            }
            catch (Exception ex) { _vm.LogAdd("[overlay] restore error: " + ex.Message); }
        }

        private async void ApplyOverlay_Click(object? s, RoutedEventArgs e)
        {
            try
            {
                var ov = _vm.Overlay;
                if (!File.Exists(ov.BaseTrpfd)) { _vm.LogAdd("[overlay] Base TRPFD not found."); return; }
                if (string.IsNullOrWhiteSpace(ov.OutputRoot)) { _vm.LogAdd("[overlay] OutputRoot not set."); return; }

                if (ov.Neutralize && Directory.Exists(ov.PacksRoot))
                {
                    foreach (var pack in ov.Packs.Where(p => p.IsEnabled).Select(p => p.Name))
                    {
                        var arc = Path.Combine(ov.PacksRoot, pack, "romfs", "arc");
                        var live = Path.Combine(arc, "data.trpfd");
                        var bak = Path.Combine(arc, "data.trpfd.neutralized");
                        if (File.Exists(live) && !File.Exists(bak))
                        { if (_vm.DryRun) _vm.LogAdd($"[dry] neutralize {live}"); else { Directory.CreateDirectory(arc); File.Move(live, bak); } }
                    }
                }

                var overlayFiles = _vm.Overlay.Packs
                    .Where(p => p.IsEnabled)
                    .Select(p => Path.Combine(ov.PacksRoot, p.Name, "romfs"))
                    .Where(Directory.Exists)
                    .SelectMany(root => Core.LooseScanner.ScanRecognized(root))
                    .ToArray();

                var target = Path.Combine(ov.OutputRoot, "currenttrpfd", "romfs", "arc", "data.trpfd");
                var svc = new Core.TrpfdService(
                    _vm.GameRoot ?? string.Empty,
                    _vm.RomfsRoot ?? string.Empty,
                    _vm.StripPrefix ?? string.Empty,
                    _vm.AutoBackup,
                    _vm.LogAdd
                );
                await Task.Run(() => svc.FullSyncFromOverlay(ov.BaseTrpfd, target, overlayFiles, _vm.DryRun));
                _vm.LogAdd("[overlay] apply complete.");
            }
            catch (Exception ex) { _vm.LogAdd("[overlay] apply error: " + ex.Message); }
        }

        private async void ListOverlayLoose_Click(object? s, RoutedEventArgs e)
        {
            var ov = _vm.Overlay;
            if (string.IsNullOrWhiteSpace(ov.PacksRoot) || !Directory.Exists(ov.PacksRoot)) { _vm.LogAdd("[overlay] PacksRoot not set."); return; }
            var selected = ov.Packs?.Where(p => p.IsEnabled).ToList();
            if (selected is null || selected.Count == 0) { _vm.LogAdd("[overlay] No packs selected."); return; }

            _vm.LogAdd($"[overlay] scanning {selected.Count} enabled pack(s) …");
            int total = 0;
            foreach (var p in selected)
            {
                var romfs = Path.Combine(ov.PacksRoot!, p.Name, "romfs");
                var list = await Task.Run(() => Core.LooseScanner.ScanRecognized(romfs));
                total += list.Length;
                _vm.LogAdd($"[overlay] {p.Name}: {list.Length} file(s)");
                foreach (var f in list.Take(200)) _vm.LogAdd("  " + f);
                if (list.Length > 200) _vm.LogAdd($"  … +{list.Length - 200} more");
            }
            _vm.LogAdd($"[overlay] total recognized: {total}");
        }

        private void OverlayConflicts_Click(object? s, RoutedEventArgs e)
        {
            var ov = _vm.Overlay;
            if (string.IsNullOrWhiteSpace(ov.PacksRoot) || !Directory.Exists(ov.PacksRoot)) { _vm.LogAdd("[overlay] PacksRoot not set."); return; }
            var seq = ov.Packs.Where(p => p.IsEnabled)
                              .Select(p =>
                              {
                                  var romfs = Path.Combine(ov.PacksRoot!, p.Name, "romfs");
                                  var files = Core.LooseScanner.ScanRecognized(romfs);
                                  return (PackName: p.Name, Files: files);
                              }).ToList();
            var conflicts = Core.ConflictReporter.FindConflicts(seq.Select(t => (t.PackName, t.Files)));
            if (conflicts.Count == 0) { _vm.LogAdd("[overlay] no conflicts."); return; }
            _vm.LogAdd($"[overlay] conflicts: {conflicts.Count} path(s)");
            foreach (var c in conflicts.Take(500))
            {
                var path = c.Path;
                var packs = c.Packs;
                _vm.LogAdd($"  {path}  ← {string.Join(" < ", packs)}");
            }
            if (conflicts.Count > 500) _vm.LogAdd($"  … +{conflicts.Count - 500} more");
        }

        private void MoveUp_Click(object? s, RoutedEventArgs e)
        {
            var ov = _vm.Overlay; var list = ov.Packs; var sel = ov.SelectedPack; if (sel is null) return;
            var idx = list.IndexOf(sel); if (idx > 0) { list.RemoveAt(idx); list.Insert(idx - 1, sel); ov.SelectedPack = sel; _vm.LogAdd("[overlay] moved up."); }
        }
        private void MoveDown_Click(object? s, RoutedEventArgs e)
        {
            var ov = _vm.Overlay; var list = ov.Packs; var sel = ov.SelectedPack; if (sel is null) return;
            var idx = list.IndexOf(sel); if (idx >= 0 && idx < list.Count - 1) { list.RemoveAt(idx); list.Insert(idx + 1, sel); ov.SelectedPack = sel; _vm.LogAdd("[overlay] moved down."); }
        }

        private void ModsScan_Click(object? s, RoutedEventArgs e)
        {
            var root = string.IsNullOrWhiteSpace(_vm.Mods.ModsRoot) ? DefaultModsRoot : _vm.Mods.ModsRoot;
            _vm.Mods.ModsRoot = root;
            Directory.CreateDirectory(root);

            _vm.Mods.Packs.Clear();

            foreach (var z in Directory.EnumerateFiles(root, "*.zip", SearchOption.TopDirectoryOnly).OrderBy(Path.GetFileName))
            {
                var name = Path.GetFileNameWithoutExtension(z);
                var files = Core.ModScanner.ScanZip(z);
                _vm.Mods.Packs.Add(new ModPackVm { Name = name, FullPath = z, IsZip = true, IsEnabled = true, RecognizedCount = files.Length });
            }
            foreach (var d in Directory.EnumerateDirectories(root).OrderBy(Path.GetFileName))
            {
                var name = Path.GetFileName(d);
                var files = Core.ModScanner.ScanFolder(d);
                _vm.Mods.Packs.Add(new ModPackVm { Name = name, FullPath = d, IsZip = false, IsEnabled = true, RecognizedCount = files.Length });
            }

            _vm.LogAdd($"[mods] scanned. found {_vm.Mods.Packs.Count} pack(s).");
        }

        private void ModsEnableAll_Click(object? s, RoutedEventArgs e) { foreach (var p in _vm.Mods.Packs) p.IsEnabled = true; _vm.LogAdd("[mods] enabled all."); }
        private void ModsDisableAll_Click(object? s, RoutedEventArgs e) { foreach (var p in _vm.Mods.Packs) p.IsEnabled = false; _vm.LogAdd("[mods] disabled all."); }

        private void ModsMoveUp_Click(object? s, RoutedEventArgs e)
        {
            var list = _vm.Mods.Packs; var sel = _vm.Mods.Selected; if (sel is null) return;
            var idx = list.IndexOf(sel); if (idx > 0) { list.RemoveAt(idx); list.Insert(idx - 1, sel); _vm.Mods.Selected = sel; _vm.LogAdd("[mods] moved up."); }
        }
        private void ModsMoveDown_Click(object? s, RoutedEventArgs e)
        {
            var list = _vm.Mods.Packs; var sel = _vm.Mods.Selected; if (sel is null) return;
            var idx = list.IndexOf(sel); if (idx >= 0 && idx < list.Count - 1) { list.RemoveAt(idx); list.Insert(idx + 1, sel); _vm.Mods.Selected = sel; _vm.LogAdd("[mods] moved down."); }
        }

        private void ModsChooseTarget_Click(object? s, RoutedEventArgs e)
        {
            var path = PickFolder(_vm.Mods.TargetRomfs);
            if (path is null) return;
            _vm.Mods.TargetRomfs = path;
            _vm.LogAdd($"[mods] target romfs = {path}");
        }

        private void ModsCopyToRomfs_Click(object? s, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_vm.Mods.TargetRomfs)) { _vm.LogAdd("[mods] choose a target romfs first."); return; }
            var selected = _vm.Mods.Packs.Where(p => p.IsEnabled).ToList();
            if (selected.Count == 0) { _vm.LogAdd("[mods] no mods selected."); return; }

            _vm.LogAdd($"[mods] copying {selected.Count} mod(s) → target…");
            var seq = selected.Select(p =>
            {
                var files = p.IsZip ? Core.ModScanner.ScanZip(p.FullPath) : Core.ModScanner.ScanFolder(p.FullPath);
                return (PackName: p.Name, IsZip: p.IsZip, FullPath: p.FullPath, Files: files);
            }).ToList();

            Core.ModCopier.CopySelected(_vm.Mods.TargetRomfs, seq, _vm.DryRun, _vm.LogAdd);
        }

        private void ModsConflicts_Click(object? s, RoutedEventArgs e)
        {
            var selected = _vm.Mods.Packs.Where(p => p.IsEnabled).ToList();
            if (selected.Count == 0) { _vm.LogAdd("[mods] no mods selected."); return; }

            var seq = selected.Select(p =>
            {
                var files = p.IsZip ? Core.ModScanner.ScanZip(p.FullPath) : Core.ModScanner.ScanFolder(p.FullPath);
                return (p.Name, files);
            }).ToList();

            var conflicts = Core.ConflictReporter.FindConflicts(seq.Select(t => (t.Name, t.files)));
            if (conflicts.Count == 0) { _vm.LogAdd("[mods] no conflicts."); return; }
            _vm.LogAdd($"[mods] conflicts: {conflicts.Count} path(s)");
            foreach (var (path, packs) in conflicts.Take(500))
                _vm.LogAdd($"  {path}  ← {string.Join(" < ", packs)}");
            if (conflicts.Count > 500) _vm.LogAdd($"  … +{conflicts.Count - 500} more");
        }
    }
}