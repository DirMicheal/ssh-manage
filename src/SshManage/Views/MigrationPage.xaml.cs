using SshManage.Models;
using SshManage.Services;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace SshManage.Views;

public partial class MigrationPage : Page
{
    private readonly SshConfigService _configService;
    private readonly MigrationService _migrationService;
    private ObservableCollection<SshSite> _importedSites = new();

    public MigrationPage()
    {
        InitializeComponent();
        _configService = ServiceLocator.ConfigService;
        _migrationService = ServiceLocator.MigrationService;
        ImportPreviewListView.ItemsSource = _importedSites;
    }

    private void BtnExport_Click(object sender, RoutedEventArgs e)
    {
        var sites = _configService.LoadSites();
        if (sites.Count == 0)
        {
            StatusText.Text = "当前没有可导出的站点配置";
            return;
        }

        var formatIndex = ExportFormatComboBox.SelectedIndex;
        var dialog = new SaveFileDialog();

        if (formatIndex == 0)
        {
            dialog.Filter = "JSON文件|*.json";
            dialog.DefaultExt = ".json";
            dialog.FileName = "ssh-config-export.json";
        }
        else
        {
            dialog.Filter = "OpenSSH配置文件|*.config";
            dialog.DefaultExt = ".config";
            dialog.FileName = "ssh-config-export.config";
        }

        if (dialog.ShowDialog() != true)
            return;

        bool success;
        if (formatIndex == 0)
            success = _migrationService.ExportToJson(dialog.FileName, sites);
        else
            success = _migrationService.ExportToOpensshConfig(dialog.FileName, sites);

        StatusText.Text = success
            ? $"成功导出 {sites.Count} 个站点配置到 {dialog.FileName}"
            : "导出失败，请检查文件路径和权限";
    }

    private void BtnImport_Click(object sender, RoutedEventArgs e)
    {
        var formatIndex = ImportFormatComboBox.SelectedIndex;
        var dialog = new OpenFileDialog();

        if (formatIndex == 0)
        {
            dialog.Filter = "JSON文件|*.json";
            dialog.DefaultExt = ".json";
        }
        else
        {
            dialog.Filter = "OpenSSH配置文件|*.config;*.cfg";
            dialog.DefaultExt = ".config";
        }

        if (dialog.ShowDialog() != true)
            return;

        bool success;
        List<SshSite> imported;
        string message;

        if (formatIndex == 0)
            (success, imported, message) = _migrationService.ImportFromJson(dialog.FileName);
        else
            (success, imported, message) = _migrationService.ImportFromOpensshConfig(dialog.FileName);

        if (!success)
        {
            StatusText.Text = message;
            _importedSites.Clear();
            BtnMerge.IsEnabled = false;
            return;
        }

        _importedSites.Clear();
        foreach (var site in imported)
            _importedSites.Add(site);

        var existingSites = _configService.LoadSites();
        var warnings = _migrationService.ValidateImportedSites(imported, existingSites);

        if (warnings.Count > 0)
        {
            var warningText = string.Join("\n", warnings);
            MessageBox.Show(warningText, "导入警告", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        BtnMerge.IsEnabled = _importedSites.Count > 0;
        StatusText.Text = message;
    }

    private void BtnMerge_Click(object sender, RoutedEventArgs e)
    {
        if (_importedSites.Count == 0)
            return;

        var result = MessageBox.Show(
            $"确定要将 {_importedSites.Count} 个站点合并到现有配置吗？同名站点将被覆盖。",
            "确认合并",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
            return;

        var existingSites = _configService.LoadSites();
        var importedHosts = _importedSites.Select(s => s.Host.ToLowerInvariant()).ToHashSet();
        var mergedSites = existingSites.Where(s => !importedHosts.Contains(s.Host.ToLowerInvariant())).ToList();
        mergedSites.AddRange(_importedSites);

        _configService.SaveSites(mergedSites);

        _importedSites.Clear();
        BtnMerge.IsEnabled = false;
        StatusText.Text = $"合并完成，当前共 {mergedSites.Count} 个站点配置";
    }
}
