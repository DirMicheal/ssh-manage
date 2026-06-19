using SshManage.Models;
using SshManage.Services;
using System;
using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace SshManage.Views;

public partial class SiteEditDialog : Window
{
    private readonly SshConfigService _configService;
    private readonly TemplateService _templateService;
    public SshSite Site { get; private set; }
    private readonly bool _isEdit;
    private readonly string? _originalHost;

    public SiteEditDialog()
    {
        InitializeComponent();
        _configService = ServiceLocator.ConfigService;
        _templateService = ServiceLocator.TemplateService;
        Site = new SshSite { Port = 22 };
        _isEdit = false;
        Title = "新增站点";
        DataContext = this;
        HostTextBox.TextChanged += HostTextBox_TextChanged;
    }

    public SiteEditDialog(SshSite site)
    {
        InitializeComponent();
        _configService = ServiceLocator.ConfigService;
        _templateService = ServiceLocator.TemplateService;
        Site = site;
        _isEdit = true;
        _originalHost = site.Host;
        Title = "编辑站点";
        DataContext = this;

        HostTextBox.Text = site.Host;
        HostNameTextBox.Text = site.HostName;
        PortTextBox.Text = site.Port.ToString();
        UserTextBox.Text = site.User;
        IdentityFileTextBox.Text = site.IdentityFile ?? string.Empty;
        ForwardAgentCheckBox.IsChecked = site.ForwardAgent;
        ProxyCommandTextBox.Text = site.ProxyCommand ?? string.Empty;
        ProxyJumpTextBox.Text = site.ProxyJump ?? string.Empty;
        ServerAliveIntervalTextBox.Text = site.ServerAliveInterval.ToString();
        CompressionCheckBox.IsChecked = site.Compression;
        GroupTextBox.Text = site.GroupName ?? string.Empty;
        RemarkTextBox.Text = site.Remark ?? string.Empty;

        HostTextBox.TextChanged += HostTextBox_TextChanged;
    }

    private void HostTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        var host = HostTextBox.Text?.Trim() ?? string.Empty;
        if (!string.IsNullOrEmpty(host) && _configService.HasDuplicateHost(host, _originalHost))
        {
            HostWarningText.Text = $"⚠ 主机名 \"{host}\" 已存在，保存后将覆盖同名配置";
            HostWarningText.Visibility = Visibility.Visible;
        }
        else
        {
            HostWarningText.Visibility = Visibility.Collapsed;
        }
    }

    private void BtnBrowseKey_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "私钥文件|*.*|所有文件|*.*",
            Title = "选择私钥文件"
        };

        var sshDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ssh");
        if (Directory.Exists(sshDir))
        {
            dialog.InitialDirectory = sshDir;
        }

        if (dialog.ShowDialog() == true)
        {
            IdentityFileTextBox.Text = dialog.FileName;
        }
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(HostTextBox.Text))
        {
            MessageBox.Show("请输入主机名(Host)", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            HostTextBox.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(HostNameTextBox.Text))
        {
            MessageBox.Show("请输入服务器地址", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            HostNameTextBox.Focus();
            return;
        }

        if (!int.TryParse(PortTextBox.Text, out var port) || port <= 0 || port > 65535)
        {
            MessageBox.Show("请输入有效的端口号 (1-65535)", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            PortTextBox.Focus();
            return;
        }

        int aliveInterval = 0;
        if (!string.IsNullOrWhiteSpace(ServerAliveIntervalTextBox.Text))
        {
            if (!int.TryParse(ServerAliveIntervalTextBox.Text, out aliveInterval) || aliveInterval < 0)
            {
                MessageBox.Show("请输入有效的心跳间隔（0表示不设置）", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                ServerAliveIntervalTextBox.Focus();
                return;
            }
        }

        Site.Host = HostTextBox.Text.Trim();
        Site.HostName = HostNameTextBox.Text.Trim();
        Site.Port = port;
        Site.User = UserTextBox.Text.Trim();
        Site.IdentityFile = string.IsNullOrWhiteSpace(IdentityFileTextBox.Text) ? null : IdentityFileTextBox.Text.Trim();
        Site.ForwardAgent = ForwardAgentCheckBox.IsChecked == true;
        Site.ProxyCommand = string.IsNullOrWhiteSpace(ProxyCommandTextBox.Text) ? null : ProxyCommandTextBox.Text.Trim();
        Site.ProxyJump = string.IsNullOrWhiteSpace(ProxyJumpTextBox.Text) ? null : ProxyJumpTextBox.Text.Trim();
        Site.ServerAliveInterval = aliveInterval;
        Site.Compression = CompressionCheckBox.IsChecked == true;
        Site.GroupName = string.IsNullOrWhiteSpace(GroupTextBox.Text) ? null : GroupTextBox.Text.Trim();
        Site.Remark = string.IsNullOrWhiteSpace(RemarkTextBox.Text) ? null : RemarkTextBox.Text.Trim();

        var errors = _configService.ValidateSite(Site, _originalHost);
        var warnings = errors.Where(err => err.Contains("已存在")).ToList();
        var criticalErrors = errors.Where(err => !err.Contains("已存在") && !err.Contains("不存在")).ToList();

        if (criticalErrors.Count > 0)
        {
            MessageBox.Show(string.Join("\n", criticalErrors), "校验失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (warnings.Count > 0)
        {
            var result = MessageBox.Show(
                string.Join("\n", warnings) + "\n\n是否继续保存？",
                "风控警告",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;
        }

        var action = _isEdit ? "编辑" : "新增";
        Title = $"{action}站点 - {Site.Host}";

        DialogResult = true;
        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
