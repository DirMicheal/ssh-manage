using SshManage.Models;
using SshManage.Services;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace SshManage.Views;

public partial class SiteManagePage : Page
{
    private readonly SshConfigService _configService;
    private ObservableCollection<SshSite> _allSites = new();
    private ObservableCollection<SshSite> _filteredSites = new();

    public SiteManagePage()
    {
        InitializeComponent();
        _configService = ServiceLocator.ConfigService;
        Loaded += SiteManagePage_Loaded;
    }

    private void SiteManagePage_Loaded(object sender, RoutedEventArgs e)
    {
        LoadSites();
    }

    private void LoadSites()
    {
        var sites = _configService.LoadSites();
        _allSites = new ObservableCollection<SshSite>(sites);
        _filteredSites = new ObservableCollection<SshSite>(sites);
        SiteListView.ItemsSource = _filteredSites;
        LoadGroups();
    }

    private void LoadGroups()
    {
        GroupTreeView.Items.Clear();
        var allNode = new TreeViewItem
        {
            Header = "📋 全部站点",
            Tag = null
        };
        allNode.IsExpanded = true;
        GroupTreeView.Items.Add(allNode);

        var groups = _configService.GetGroups(_allSites);
        foreach (var group in groups)
        {
            var groupNode = new TreeViewItem
            {
                Header = $"📁 {group.Name}",
                Tag = group.Name
            };

            foreach (var site in group.Sites)
            {
                var siteNode = new TreeViewItem
                {
                    Header = $"🌐 {site.Host}",
                    Tag = site
                };
                groupNode.Items.Add(siteNode);
            }

            groupNode.IsExpanded = true;
            allNode.Items.Add(groupNode);
        }

        if (allNode.HasItems)
        {
            allNode.IsSelected = true;
        }
    }

    private void BtnAddSite_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SiteEditDialog
        {
            Owner = Window.GetWindow(this),
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        if (dialog.ShowDialog() == true)
        {
            var site = dialog.Site;
            if (!string.IsNullOrEmpty(site.Host))
            {
                _allSites.Add(site);
                _filteredSites.Add(site);
                LoadGroups();
                SaveConfig();
            }
        }
    }

    private void BtnEditSite_Click(object sender, RoutedEventArgs e)
    {
        if (SiteListView.SelectedItem is not SshSite selectedSite)
            return;

        var dialog = new SiteEditDialog(selectedSite)
        {
            Owner = Window.GetWindow(this),
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        if (dialog.ShowDialog() == true)
        {
            var updated = dialog.Site;
            var index = _allSites.IndexOf(selectedSite);
            if (index >= 0)
            {
                _allSites[index] = updated;
                var filterIndex = _filteredSites.IndexOf(selectedSite);
                if (filterIndex >= 0)
                {
                    _filteredSites[filterIndex] = updated;
                }
            }
            LoadGroups();
            SaveConfig();
        }
    }

    private void BtnDeleteSite_Click(object sender, RoutedEventArgs e)
    {
        if (SiteListView.SelectedItem is not SshSite selectedSite)
            return;

        var result = MessageBox.Show(
            $"确定要删除站点 \"{selectedSite.Host}\" 吗？\n\n此操作将修改SSH配置文件，删除后可通过备份恢复。\n\n是否继续？",
            "确认删除",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
            return;

        var confirmResult = MessageBox.Show(
            $"二次确认：即将删除站点 \"{selectedSite.Host}\"，是否确认？",
            "二次确认删除",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirmResult != MessageBoxResult.Yes)
            return;

        _configService.BackupConfig(BackupType.AutoBeforeDelete, $"删除站点 {selectedSite.Host} 前自动备份");

        _allSites.Remove(selectedSite);
        _filteredSites.Remove(selectedSite);
        LoadGroups();
        SaveConfig();

        _configService.CleanupOldBackups();
    }

    private void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        LoadSites();
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        SaveConfig();
        MessageBox.Show("配置已保存！", "保存成功", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void SaveConfig()
    {
        try
        {
            _configService.BackupConfig(BackupType.AutoBeforeSave, "保存配置前自动备份");
            _configService.SaveSites(_allSites);
            _configService.CleanupOldBackups();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"保存失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SiteListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var hasSelection = SiteListView.SelectedItem != null;
        BtnEditSite.IsEnabled = hasSelection;
        BtnDeleteSite.IsEnabled = hasSelection;
    }

    private void GroupTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (GroupTreeView.SelectedItem is TreeViewItem item)
        {
            if (item.Tag == null)
            {
                _filteredSites = new ObservableCollection<SshSite>(_allSites);
            }
            else if (item.Tag is string groupName)
            {
                var filtered = _allSites.Where(s => s.GroupName == groupName 
                    || (string.IsNullOrEmpty(s.GroupName) && groupName == "默认分组"));
                _filteredSites = new ObservableCollection<SshSite>(filtered);
            }
            else if (item.Tag is SshSite site)
            {
                _filteredSites = new ObservableCollection<SshSite> { site };
                SiteListView.SelectedItem = site;
            }

            SiteListView.ItemsSource = _filteredSites;
        }
    }

    private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var searchText = SearchTextBox.Text?.Trim().ToLower() ?? string.Empty;

        if (string.IsNullOrEmpty(searchText))
        {
            _filteredSites = new ObservableCollection<SshSite>(_allSites);
        }
        else
        {
            var filtered = _allSites.Where(s =>
                s.Host.ToLower().Contains(searchText) ||
                s.HostName.ToLower().Contains(searchText) ||
                s.User.ToLower().Contains(searchText) ||
                (s.Remark?.ToLower().Contains(searchText) ?? false) ||
                (s.GroupName?.ToLower().Contains(searchText) ?? false));
            _filteredSites = new ObservableCollection<SshSite>(filtered);
        }

        SiteListView.ItemsSource = _filteredSites;
    }
}
