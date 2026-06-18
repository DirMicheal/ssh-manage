using SshManage.Models;
using System;
using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace SshManage.Views;

public partial class SiteEditDialog : Window
{
    public SshSite Site { get; private set; }
    private readonly bool _isEdit;

    public SiteEditDialog()
    {
        InitializeComponent();
        Site = new SshSite { Port = 22 };
        _isEdit = false;
        Title = "新增站点";
        DataContext = this;
    }

    public SiteEditDialog(SshSite site)
    {
        InitializeComponent();
        Site = site;
        _isEdit = true;
        Title = "编辑站点";
        DataContext = this;

        HostTextBox.Text = site.Host;
        HostNameTextBox.Text = site.HostName;
        PortTextBox.Text = site.Port.ToString();
        UserTextBox.Text = site.User;
        IdentityFileTextBox.Text = site.IdentityFile ?? string.Empty;
        GroupTextBox.Text = site.GroupName ?? string.Empty;
        RemarkTextBox.Text = site.Remark ?? string.Empty;
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

        Site.Host = HostTextBox.Text.Trim();
        Site.HostName = HostNameTextBox.Text.Trim();
        Site.Port = port;
        Site.User = UserTextBox.Text.Trim();
        Site.IdentityFile = string.IsNullOrWhiteSpace(IdentityFileTextBox.Text) ? null : IdentityFileTextBox.Text.Trim();
        Site.GroupName = string.IsNullOrWhiteSpace(GroupTextBox.Text) ? null : GroupTextBox.Text.Trim();
        Site.Remark = string.IsNullOrWhiteSpace(RemarkTextBox.Text) ? null : RemarkTextBox.Text.Trim();

        DialogResult = true;
        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
