using SshManage.Models;
using SshManage.Services;
using System.Collections.ObjectModel;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

namespace SshManage.Views;

public partial class ConnectionTestPage : Page
{
    private readonly SshConfigService _configService;
    private readonly ConnectionTestService _testService;
    private ObservableCollection<SshSite> _sites = new();
    private CancellationTokenSource? _cts;
    private bool _isTesting;

    public ConnectionTestPage()
    {
        InitializeComponent();
        _configService = ServiceLocator.ConfigService;
        _testService = ServiceLocator.ConnectionTestService;
        Loaded += ConnectionTestPage_Loaded;
    }

    private void ConnectionTestPage_Loaded(object sender, RoutedEventArgs e)
    {
        LoadSites();
    }

    private void LoadSites()
    {
        var sites = _configService.LoadSites();
        _sites = new ObservableCollection<SshSite>(sites);
        SiteListView.ItemsSource = _sites;
    }

    private void SiteListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        BtnTestSelected.IsEnabled = SiteListView.SelectedItem != null && !_isTesting;
    }

    private async void BtnTestAll_Click(object sender, RoutedEventArgs e)
    {
        if (_sites.Count == 0)
        {
            MessageBox.Show("没有可检测的站点", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!int.TryParse(TimeoutTextBox.Text, out var timeout) || timeout <= 0)
        {
            MessageBox.Show("请输入有效的超时时间", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _isTesting = true;
        _cts = new CancellationTokenSource();
        BtnTestAll.IsEnabled = false;
        BtnTestSelected.IsEnabled = false;
        BtnStop.IsEnabled = true;
        ProgressBar.Visibility = Visibility.Visible;
        ProgressBar.Maximum = _sites.Count;
        ProgressBar.Value = 0;
        StatusTextBlock.Text = "正在检测...";

        var successCount = 0;
        var failCount = 0;

        try
        {
            for (int i = 0; i < _sites.Count; i++)
            {
                if (_cts.Token.IsCancellationRequested)
                    break;

                var site = _sites[i];
                var host = string.IsNullOrEmpty(site.HostName) ? site.Host : site.HostName;

                StatusTextBlock.Text = $"正在检测 {site.Host} ({i + 1}/{_sites.Count})";

                var result = await _testService.TestConnectionAsync(host, site.Port, timeout, _cts.Token);

                site.Remark = $"{(result.Success ? "✅" : "❌")} {result.Message} ({result.Duration.TotalMilliseconds:F0}ms)";

                if (result.Success)
                    successCount++;
                else
                    failCount++;

                ProgressBar.Value = i + 1;

                SiteListView.ItemsSource = null;
                SiteListView.ItemsSource = _sites;
            }

            if (_cts.Token.IsCancellationRequested)
            {
                StatusTextBlock.Text = "检测已取消";
            }
            else
            {
                StatusTextBlock.Text = $"检测完成 - 成功: {successCount}，失败: {failCount}";
            }
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = $"检测异常: {ex.Message}";
        }
        finally
        {
            _isTesting = false;
            _cts?.Dispose();
            _cts = null;
            BtnTestAll.IsEnabled = true;
            BtnTestSelected.IsEnabled = SiteListView.SelectedItem != null;
            BtnStop.IsEnabled = false;
            ProgressBar.Visibility = Visibility.Collapsed;
        }
    }

    private async void BtnTestSelected_Click(object sender, RoutedEventArgs e)
    {
        if (SiteListView.SelectedItem is not SshSite selectedSite)
            return;

        if (!int.TryParse(TimeoutTextBox.Text, out var timeout) || timeout <= 0)
        {
            MessageBox.Show("请输入有效的超时时间", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _isTesting = true;
        BtnTestAll.IsEnabled = false;
        BtnTestSelected.IsEnabled = false;
        BtnStop.IsEnabled = true;
        ProgressBar.Visibility = Visibility.Visible;
        ProgressBar.Maximum = 1;
        ProgressBar.Value = 0;
        StatusTextBlock.Text = $"正在检测 {selectedSite.Host}...";

        try
        {
            var host = string.IsNullOrEmpty(selectedSite.HostName) ? selectedSite.Host : selectedSite.HostName;
            var result = await _testService.TestConnectionAsync(host, selectedSite.Port, timeout);

            selectedSite.Remark = $"{(result.Success ? "✅" : "❌")} {result.Message} ({result.Duration.TotalMilliseconds:F0}ms)";

            SiteListView.ItemsSource = null;
            SiteListView.ItemsSource = _sites;

            ProgressBar.Value = 1;
            StatusTextBlock.Text = result.Success ? "连接成功" : "连接失败";
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = $"检测异常: {ex.Message}";
        }
        finally
        {
            _isTesting = false;
            BtnTestAll.IsEnabled = true;
            BtnTestSelected.IsEnabled = true;
            BtnStop.IsEnabled = false;
            ProgressBar.Visibility = Visibility.Collapsed;
        }
    }

    private void BtnStop_Click(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        StatusTextBlock.Text = "正在停止...";
    }

    private void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        LoadSites();
        StatusTextBlock.Text = "站点列表已刷新";
    }
}
