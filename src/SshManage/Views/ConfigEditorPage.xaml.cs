using SshManage.Models;
using SshManage.Services;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace SshManage.Views;

public partial class ConfigEditorPage : Page
{
    private readonly SshConfigService _configService;
    private string _originalContent = string.Empty;
    private bool _isModified;

    public ConfigEditorPage()
    {
        InitializeComponent();
        _configService = ServiceLocator.ConfigService;
        Loaded += ConfigEditorPage_Loaded;
    }

    private void ConfigEditorPage_Loaded(object sender, RoutedEventArgs e)
    {
        FilePathTextBlock.Text = _configService.ConfigPath;
        LoadConfig();
    }

    private void LoadConfig()
    {
        try
        {
            _originalContent = _configService.GetRawConfig();
            ConfigTextBox.Text = _originalContent;
            _isModified = false;
            UpdateStatus("配置已加载");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"加载配置失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnLoad_Click(object sender, RoutedEventArgs e)
    {
        if (_isModified)
        {
            var result = MessageBox.Show(
                "配置已修改，是否要重新加载？未保存的更改将丢失。",
                "确认重新加载",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;
        }

        LoadConfig();
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (!_isModified)
        {
            MessageBox.Show("配置没有修改", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            _configService.BackupConfig(BackupType.AutoBeforeSave, "配置编辑器保存前自动备份");
            _configService.SaveRawConfig(ConfigTextBox.Text);
            _originalContent = ConfigTextBox.Text;
            _isModified = false;
            UpdateStatus("配置已保存，已自动创建备份");
            MessageBox.Show("配置保存成功！已自动创建备份。", "保存成功", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"保存失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnBackup_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _configService.BackupConfig(BackupType.Manual, "手动创建备份");
            UpdateStatus("备份已创建");
            MessageBox.Show("备份创建成功！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"创建备份失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnRevert_Click(object sender, RoutedEventArgs e)
    {
        if (!_isModified)
        {
            MessageBox.Show("没有可撤销的修改", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var result = MessageBox.Show(
            "确定要撤销所有修改吗？",
            "确认撤销",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            ConfigTextBox.Text = _originalContent;
            _isModified = false;
            UpdateStatus("已撤销修改");
        }
    }

    private void BtnFormat_Click(object sender, RoutedEventArgs e)
    {
        var content = ConfigTextBox.Text;
        var formatted = FormatConfig(content);
        ConfigTextBox.Text = formatted;
        UpdateStatus("配置已格式化");
    }

    private string FormatConfig(string content)
    {
        var lines = content.Split('\n');
        var result = new List<string>();
        var inHostBlock = false;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            if (string.IsNullOrWhiteSpace(trimmed))
            {
                if (result.Count > 0 && !string.IsNullOrWhiteSpace(result[^1]))
                {
                    result.Add(string.Empty);
                }
                inHostBlock = false;
                continue;
            }

            if (trimmed.StartsWith('#'))
            {
                result.Add(trimmed);
                continue;
            }

            if (trimmed.StartsWith("Host ", StringComparison.OrdinalIgnoreCase))
            {
                if (result.Count > 0 && result[^1] != string.Empty)
                {
                    result.Add(string.Empty);
                }
                result.Add(trimmed);
                inHostBlock = true;
            }
            else if (inHostBlock)
            {
                result.Add("    " + trimmed);
            }
            else
            {
                result.Add(trimmed);
            }
        }

        while (result.Count > 0 && string.IsNullOrWhiteSpace(result[^1]))
        {
            result.RemoveAt(result.Count - 1);
        }

        return string.Join("\n", result) + "\n";
    }

    private void BtnOpenFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dir = _configService.SshDirectory;
            if (Directory.Exists(dir))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = dir,
                    UseShellExecute = true
                });
            }
            else
            {
                MessageBox.Show("SSH目录不存在", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"打开目录失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ConfigTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _isModified = ConfigTextBox.Text != _originalContent;
        UpdateModifiedStatus();
    }

    private void UpdateModifiedStatus()
    {
        if (_isModified)
        {
            StatusTextBlock.Text = "• 未保存的更改";
            StatusTextBlock.Foreground = (System.Windows.Media.Brush)FindResource("WarningBrush");
        }
    }

    private void UpdateStatus(string message)
    {
        StatusTextBlock.Text = message;
        StatusTextBlock.Foreground = (System.Windows.Media.Brush)FindResource("TextSecondaryBrush");
    }
}
