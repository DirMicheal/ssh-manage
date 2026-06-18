using SshManage.Models;
using SshManage.Services;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace SshManage.Views;

public partial class PermissionFixPage : Page
{
    private readonly SshKeyService _keyService;
    private ObservableCollection<SshKey> _keys = new();

    public PermissionFixPage()
    {
        InitializeComponent();
        _keyService = ServiceLocator.KeyService;
        Loaded += PermissionFixPage_Loaded;
    }

    private void PermissionFixPage_Loaded(object sender, RoutedEventArgs e)
    {
        LoadKeys();
    }

    private void LoadKeys()
    {
        var keys = _keyService.GetAllKeys().Where(k => k.HasPrivateKey).ToList();
        _keys = new ObservableCollection<SshKey>(keys);
        KeyListView.ItemsSource = _keys;
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        var issueCount = _keys.Count(k => k.HasPermissionIssue);
        IssueCountTextBlock.Text = issueCount > 0 ? $"{issueCount} 个存在权限问题" : string.Empty;

        var hasIssues = issueCount > 0;
        BtnFixAll.IsEnabled = hasIssues;

        var hasSelection = KeyListView.SelectedItem != null;
        BtnFixSelected.IsEnabled = hasSelection;
    }

    private void BtnScan_Click(object sender, RoutedEventArgs e)
    {
        LoadKeys();
        var issueCount = _keys.Count(k => k.HasPermissionIssue);
        if (issueCount > 0)
        {
            StatusTextBlock.Text = $"扫描完成，发现 {issueCount} 个私钥存在权限问题";
            StatusTextBlock.Foreground = (System.Windows.Media.Brush)FindResource("ErrorBrush");
        }
        else
        {
            StatusTextBlock.Text = "扫描完成，所有私钥权限正常";
            StatusTextBlock.Foreground = (System.Windows.Media.Brush)FindResource("SuccessBrush");
        }
    }

    private void BtnFixAll_Click(object sender, RoutedEventArgs e)
    {
        var issueKeys = _keys.Where(k => k.HasPermissionIssue).ToList();
        if (issueKeys.Count == 0)
        {
            MessageBox.Show("没有需要修复的权限问题", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var result = MessageBox.Show(
            $"确定要修复 {issueKeys.Count} 个私钥的权限吗？",
            "确认修复",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
            return;

        var successCount = 0;
        var failCount = 0;

        foreach (var key in issueKeys)
        {
            if (_keyService.FixPermission(key.PrivateKeyPath))
            {
                key.HasPermissionIssue = false;
                key.PermissionStatus = "权限正常";
                successCount++;
            }
            else
            {
                failCount++;
            }
        }

        KeyListView.ItemsSource = null;
        KeyListView.ItemsSource = _keys;

        var message = $"修复完成！\n成功: {successCount} 个";
        if (failCount > 0)
        {
            message += $"\n失败: {failCount} 个\n\n失败的文件可能需要以管理员身份运行程序";
        }

        MessageBox.Show(message, "修复结果", MessageBoxButton.OK, 
            failCount > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);

        UpdateStatus();
    }

    private void BtnFixSelected_Click(object sender, RoutedEventArgs e)
    {
        if (KeyListView.SelectedItem is not SshKey selectedKey)
            return;

        if (!selectedKey.HasPermissionIssue)
        {
            MessageBox.Show("该密钥权限正常，无需修复", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var success = _keyService.FixPermission(selectedKey.PrivateKeyPath);
        if (success)
        {
            selectedKey.HasPermissionIssue = false;
            selectedKey.PermissionStatus = "权限正常";
            KeyListView.ItemsSource = null;
            KeyListView.ItemsSource = _keys;
            MessageBox.Show("权限修复成功！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            MessageBox.Show("权限修复失败，请以管理员身份运行程序", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        UpdateStatus();
    }

    private void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        LoadKeys();
    }

    private void KeyListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        BtnFixSelected.IsEnabled = KeyListView.SelectedItem != null;
    }
}
