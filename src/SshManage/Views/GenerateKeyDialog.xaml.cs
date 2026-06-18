using SshManage.Models;
using SshManage.Services;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace SshManage.Views;

public partial class GenerateKeyDialog : Window
{
    private readonly SshKeyService _keyService;

    public GenerateKeyDialog()
    {
        InitializeComponent();
        _keyService = ServiceLocator.KeyService;
        KeyTypeComboBox_SelectionChanged(null!, null!);
    }

    private void KeyTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (KeyTypeComboBox.SelectedItem is not ComboBoxItem item)
            return;

        var type = item.Tag?.ToString();
        KeySizeComboBox.Items.Clear();

        switch (type)
        {
            case "RSA":
                AddKeySizeOption("1024", "1024");
                AddKeySizeOption("2048", "2048", true);
                AddKeySizeOption("4096", "4096");
                break;
            case "ECDSA":
                AddKeySizeOption("256", "256", true);
                AddKeySizeOption("384", "384");
                AddKeySizeOption("521", "521");
                break;
            case "Ed25519":
                AddKeySizeOption("256", "256", true);
                KeySizeComboBox.IsEnabled = false;
                break;
            case "DSA":
                AddKeySizeOption("1024", "1024", true);
                break;
            default:
                KeySizeComboBox.IsEnabled = true;
                break;
        }
    }

    private void AddKeySizeOption(string content, string tag, bool isSelected = false)
    {
        var item = new ComboBoxItem
        {
            Content = content,
            Tag = tag
        };
        if (isSelected)
            item.IsSelected = true;
        KeySizeComboBox.Items.Add(item);
    }

    private async void BtnGenerate_Click(object sender, RoutedEventArgs e)
    {
        var keyName = KeyNameTextBox.Text?.Trim();
        if (string.IsNullOrEmpty(keyName))
        {
            MessageBox.Show("请输入密钥名称", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            KeyNameTextBox.Focus();
            return;
        }

        if (KeyTypeComboBox.SelectedItem is not ComboBoxItem typeItem || typeItem.Tag == null)
        {
            MessageBox.Show("请选择密钥类型", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (KeySizeComboBox.SelectedItem is not ComboBoxItem sizeItem || sizeItem.Tag == null)
        {
            MessageBox.Show("请选择密钥长度", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var passphrase = PassphraseBox.Password;
        var confirmPassphrase = ConfirmPassphraseBox.Password;

        if (passphrase != confirmPassphrase)
        {
            MessageBox.Show("两次输入的密码不一致", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            ConfirmPassphraseBox.Focus();
            return;
        }

        var keyType = Enum.Parse<KeyType>(typeItem.Tag.ToString()!);
        var keySize = int.Parse(sizeItem.Tag.ToString()!);

        var keyPath = Path.Combine(_keyService.SshDirectory, keyName);
        if (File.Exists(keyPath))
        {
            var result = MessageBox.Show(
                $"密钥名称 \"{keyName}\" 已存在，是否覆盖？",
                "确认覆盖",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes)
                return;
        }

        BtnGenerate.IsEnabled = false;
        BtnGenerate.Content = "生成中...";

        try
        {
            var success = await _keyService.GenerateKeyAsync(
                keyName,
                keyType,
                keySize,
                string.IsNullOrEmpty(passphrase) ? null : passphrase,
                string.IsNullOrEmpty(CommentTextBox.Text) ? null : CommentTextBox.Text.Trim());

            if (success)
            {
                MessageBox.Show("密钥生成成功！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show("密钥生成失败，请确保系统已安装OpenSSH", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"生成失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            BtnGenerate.IsEnabled = true;
            BtnGenerate.Content = "生成";
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
