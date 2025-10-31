using Microsoft.CodeAnalysis;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using TrpfdManager.Core;
using WinForms = System.Windows.Forms;

namespace TrpfdManager;
public partial class MainWindow : Window
{
    private readonly MainVm _vm = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;
        _vm.LogAdd("ready");
    }

    private void BrowseGameRoot_Click(object? sender, RoutedEventArgs e)
    {
        using var dlg = new WinForms.FolderBrowserDialog { ShowNewFolderButton = false, Description = "Select Game Root" };
        if (dlg.ShowDialog() == WinForms.DialogResult.OK) _vm.GameRoot = dlg.SelectedPath.Trim();
    }

    private void BrowseRomfsRoot_Click(object? sender, RoutedEventArgs e)
    {
        using var dlg = new WinForms.FolderBrowserDialog { ShowNewFolderButton = false, Description = "Select Romfs Root" };
        if (dlg.ShowDialog() == WinForms.DialogResult.OK) _vm.RomfsRoot = dlg.SelectedPath.Trim();
    }

    private async void UpdateButton_Click(object? sender, RoutedEventArgs e) => await RunSyncOnce();

    private async void ExportCsvButton_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var svc = BuildService();
            var csvPath = await Task.Run(() => svc.ExportCsv());
            _vm.ResolvedTrpfdPath = svc.ResolvedTrpfdPath;
            _vm.LogAdd($"[csv] {csvPath}");
            try { System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{csvPath}\""); } catch { }
        }
        catch (Exception ex) { _vm.LogAdd("[error] " + ex.ToString()); }
    }

    private async void ListLooseButton_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var svc = BuildService();
            var lines = await Task.Run(() => svc.ListRecognizedLooseFiles(limit: 500).ToArray());
            _vm.ResolvedTrpfdPath = svc.ResolvedTrpfdPath;
            foreach (var line in lines) _vm.LogAdd(line);
        }
        catch (Exception ex) { _vm.LogAdd("[error] " + ex.ToString()); }
    }

    private async void TestConfigButton_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var svc = BuildService();
            var lines = await Task.Run(() => Diagnostics.TestConfig(svc));
            foreach (var line in lines) _vm.LogAdd(line);
            _vm.ResolvedTrpfdPath = svc.ResolvedTrpfdPath;
        }
        catch (Exception ex) { _vm.LogAdd("[error] " + ex.ToString()); }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        _vm.StopWatcher();
        base.OnClosing(e);
    }

    private async Task RunSyncOnce()
    {
        try
        {
            _vm.Status = "running";
            var svc = BuildService();
            var changes = await Task.Run(() => svc.FullSync(dryRun: _vm.DryRun));
            _vm.ResolvedTrpfdPath = svc.ResolvedTrpfdPath;
            _vm.LastBackupPath = svc.LastBackupPath;
            _vm.LogAdd(changes);
            _vm.Status = "done";
            if (_vm.Watch) { _vm.StartWatcher(svc); _vm.ResolvedTrpfdPath = svc.ResolvedTrpfdPath; }
            else _vm.StopWatcher();
        }
        catch (Exception ex) { _vm.LogAdd("[error] " + ex.ToString()); _vm.Status = "error"; }
    }

    private TrpfdService BuildService()
    {
        if (string.IsNullOrWhiteSpace(_vm.GameRoot)) throw new InvalidOperationException("set Game Root");
        if (string.IsNullOrWhiteSpace(_vm.RomfsRoot)) throw new InvalidOperationException("set Romfs Root");
        return new TrpfdService(_vm.GameRoot, _vm.RomfsRoot, _vm.StripPrefix, _vm.AutoBackup,
            log: s => Dispatcher.Invoke(() => _vm.LogAdd(s)));
    }
}