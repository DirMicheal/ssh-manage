using SshManage.Models;
using SshManage.Services;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace SshManage.Views;

public partial class KeyManagePage : Page
{
    private readonly SshKeyService _keyService;
    private ObservableCollection<SshKey> _keys = new();

    public KeyManagePage()
    {
        InitializeComponent();
        _keyService = ServiceLocator.KeyService;
        Loaded += KeyManagePage_Loaded;
    }

    private void KeyManagePage_Loaded(object sender, RoutedEventArgs e)
    {
        LoadKeys();
    }

    private void LoadKeys()
    {
        var keys = _keyService.GetAllKeys();
        _keys = new ObservableCollection<SshKey>(keys);
        KeyListView.ItemsSource = _keys;
        UpdateSelectionState();
    }

    private void KeyListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateSelectionState();

        if (KeyListView.SelectedItem is SshKey selectedKey)
        {
            PublicKeyPreviewTextBox.Text = selectedKey.PublicKeyContent;
            PrivateKeyPathTextBox.Text = selectedKey.PrivateKeyPath;
            MichealKeyBadge.Visibility = selectedKey.IsMichealKey ? Visibility.Visible : Visibility.Collapsed;
        }
        else
        {
            PublicKeyPreviewTextBox.Text = string.Empty;
            PrivateKeyPathTextBox.Text = string.Empty;
            MichealKeyBadge.Visibility = Visibility.Collapsed;
        }
    }

    private void UpdateSelectionState()
    {
        var hasSelection = KeyListView.SelectedItem != null;
        BtnCopyPublicKey.IsEnabled = hasSelection;
        BtnFixPermission.IsEnabled = hasSelection;
        BtnDeleteKey.IsEnabled = hasSelection;
    }

    private void BtnGenerateKey_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new GenerateKeyDialog
        {
            Owner = Window.GetWindow(this),
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        if (dialog.ShowDialog() == true)
        {
            LoadKeys();
            MessageBox.Show("密钥生成成功！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void BtnCopyPublicKey_Click(object sender, RoutedEventArgs e)
    {
        if (KeyListView.SelectedItem is not SshKey selectedKey)
            return;

        if (!selectedKey.HasPublicKey)
        {
            MessageBox.Show("未找到公钥文件", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var success = _keyService.CopyPublicKeyToClipboard(selectedKey.PublicKeyPath);
        if (success)
        {
            MessageBox.Show("公钥已复制到剪贴板", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            MessageBox.Show("复制公钥失败", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnFixPermission_Click(object sender, RoutedEventArgs e)
    {
        if (KeyListView.SelectedItem is not SshKey selectedKey)
            return;

        if (!selectedKey.HasPrivateKey)
        {
            MessageBox.Show("未找到私钥文件", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var success = _keyService.FixPermission(selectedKey.PrivateKeyPath);
        if (success)
        {
            MessageBox.Show("权限修复成功！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
            LoadKeys();
        }
        else
        {
            MessageBox.Show("权限修复失败，请以管理员身份运行程序", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnDeleteKey_Click(object sender, RoutedEventArgs e)
    {
        if (KeyListView.SelectedItem is not SshKey selectedKey)
            return;

        var keyLabel = selectedKey.IsMichealKey ? $"⭐ Micheal私钥 \"{selectedKey.Name}\"" : $"密钥 \"{selectedKey.Name}\"";

        var result = MessageBox.Show(
            $"确定要删除{keyLabel}吗？\n\n此操作将永久删除私钥和公钥文件，不可恢复！\n\n是否继续？",
            "确认删除",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
            return;

        var confirmResult = MessageBox.Show(
            $"二次确认：即将永久删除{keyLabel}，是否确认？\n\n删除后使用该密钥的站点将无法连接！",
            "二次确认删除",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirmResult != MessageBoxResult.Yes)
            return;

        var success = _keyService.DeleteKey(selectedKey.Name);
        if (success)
        {
            LoadKeys();
            MessageBox.Show("密钥已删除", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            MessageBox.Show("删除失败", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        LoadKeys();
    }
}
