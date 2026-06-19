using SshManage.Models;
using SshManage.Services;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SshManage.Views;

public partial class BackupManagePage : Page
{
    private readonly SshConfigService _configService;
    private ObservableCollection<BackupRecord> _backups = new();

    public BackupManagePage()
    {
        InitializeComponent();
        _configService = ServiceLocator.ConfigService;
        Loaded += BackupManagePage_Loaded;
    }

    private void BackupManagePage_Loaded(object sender, RoutedEventArgs e)
    {
        LoadBackups();
        BackupPathTextBox.Text = _configService.AppDataDirectory;
    }

    private void LoadBackups()
    {
        var records = _configService.GetBackupRecords();
        _backups = new ObservableCollection<BackupRecord>(records);
        BackupListView.ItemsSource = _backups;
        StatusText.Text = $"共 {_backups.Count} 个备份";
        UpdateSelectionState();
    }

    private void BackupListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateSelectionState();
    }

    private void UpdateSelectionState()
    {
        var hasSelection = BackupListView.SelectedItem != null;
        BtnRestore.IsEnabled = hasSelection;
        BtnDeleteBackup.IsEnabled = hasSelection;
    }

    private void BtnCreateBackup_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _configService.BackupConfig(BackupType.Manual);
            LoadBackups();
            MessageBox.Show("备份创建成功！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (System.Exception ex)
        {
            MessageBox.Show($"创建备份失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnRestore_Click(object sender, RoutedEventArgs e)
    {
        RestoreSelectedBackup();
    }

    private void BackupListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject d && FindAncestor<ListViewItem>(d) != null)
        {
            RestoreSelectedBackup();
        }
    }

    private void RestoreSelectedBackup()
    {
        if (BackupListView.SelectedItem is not BackupRecord selected)
            return;

        var result = MessageBox.Show(
            $"确定要恢复此备份吗？\n\n备份时间: {selected.CreatedAt:yyyy-MM-dd HH:mm:ss}\n备份类型: {selected.DisplayType}\n\n此操作将替换当前SSH配置文件，恢复前会自动创建当前配置的备份。\n\n是否继续？",
            "确认恢复",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
            return;

        var confirmResult = MessageBox.Show(
            $"二次确认：即将恢复 {selected.CreatedAt:yyyy-MM-dd HH:mm:ss} 的备份，是否确认？",
            "二次确认恢复",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirmResult != MessageBoxResult.Yes)
            return;

        var success = _configService.RestoreBackup(selected.FilePath);
        if (success)
        {
            LoadBackups();
            MessageBox.Show("备份恢复成功！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            MessageBox.Show("备份恢复失败", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnDeleteBackup_Click(object sender, RoutedEventArgs e)
    {
        if (BackupListView.SelectedItem is not BackupRecord selected)
            return;

        var result = MessageBox.Show(
            $"确定要删除此备份吗？\n\n备份时间: {selected.CreatedAt:yyyy-MM-dd HH:mm:ss}\n\n此操作不可恢复！\n\n是否继续？",
            "确认删除",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
            return;

        var confirmResult = MessageBox.Show(
            $"二次确认：即将永久删除 {selected.CreatedAt:yyyy-MM-dd HH:mm:ss} 的备份，是否确认？",
            "二次确认删除",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirmResult != MessageBoxResult.Yes)
            return;

        var success = _configService.DeleteBackup(selected.FilePath);
        if (success)
        {
            LoadBackups();
            MessageBox.Show("备份已删除", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            MessageBox.Show("删除备份失败", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnCleanup_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "确定要清理旧备份吗？\n\n将保留最近的20个备份，删除更早的备份记录。\n\n是否继续？",
            "确认清理",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
            return;

        _configService.CleanupOldBackups();
        LoadBackups();
        MessageBox.Show("旧备份清理完成", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        LoadBackups();
    }

    private static T? FindAncestor<T>(DependencyObject current) where T : DependencyObject
    {
        while (current != null)
        {
            if (current is T t)
                return t;
            current = System.Windows.Media.VisualTreeHelper.GetParent(current);
        }
        return null;
    }
}
