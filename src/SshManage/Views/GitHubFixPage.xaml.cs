using SshManage.Helpers;
using SshManage.Models;
using SshManage.Services;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SshManage.Views;

public partial class GitHubFixPage : Page
{
    private readonly GitHubFixService _gitHubFixService;
    private readonly SshConfigService _configService;
    private GitHubDiagnosticResult? _diagnosticResult;

    public GitHubFixPage()
    {
        InitializeComponent();
        _gitHubFixService = ServiceLocator.GitHubFixService;
        _configService = ServiceLocator.ConfigService;
        Loaded += GitHubFixPage_Loaded;
    }

    private void GitHubFixPage_Loaded(object sender, RoutedEventArgs e)
    {
        RunDiagnosis();
    }

    private void BtnDiagnose_Click(object sender, RoutedEventArgs e)
    {
        RunDiagnosis();
    }

    private void RunDiagnosis()
    {
        StatusTextBlock.Text = "正在诊断...";
        _diagnosticResult = _gitHubFixService.Diagnose();
        UpdateDiagnosticUI(_diagnosticResult);
        StatusTextBlock.Text = _diagnosticResult.Summary;
    }

    private void BtnFixAll_Click(object sender, RoutedEventArgs e)
    {
        if (_diagnosticResult == null)
        {
            MessageBox.Show("请先运行诊断", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (_diagnosticResult.IsHealthy)
        {
            MessageBox.Show("当前配置正常，无需修复", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var confirm = MessageBox.Show(
            $"检测到 {_diagnosticResult.Issues.Count} 个问题，确定要一键修复吗？",
            "确认修复",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes)
            return;

        var doubleConfirm = MessageBox.Show(
            "修复操作将修改SSH配置文件和known_hosts，是否继续？",
            "二次确认",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (doubleConfirm != MessageBoxResult.Yes)
            return;

        StatusTextBlock.Text = "正在修复...";
        var success = _gitHubFixService.FixAll(_diagnosticResult);

        if (success)
        {
            StatusTextBlock.Text = "修复完成";
            StatusTextBlock.Foreground = (Brush)FindResource("SuccessBrush");
            MessageBox.Show("所有问题已修复成功！", "修复完成", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            StatusTextBlock.Text = "部分问题修复失败";
            StatusTextBlock.Foreground = (Brush)FindResource("ErrorBrush");
            MessageBox.Show("部分问题修复失败，可能需要管理员权限", "修复结果", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        RunDiagnosis();
    }

    private void BtnTestConnection_Click(object sender, RoutedEventArgs e)
    {
        StatusTextBlock.Text = "正在测试GitHub SSH连接...";
        BtnTestConnection.IsEnabled = false;
        BottomStatusTextBlock.Text = "连接测试中，请稍候...";

        _gitHubFixService.TestGitHubConnection((success, message) =>
        {
            Dispatcher.Invoke(() =>
            {
                BtnTestConnection.IsEnabled = true;

                if (success)
                {
                    StatusTextBlock.Text = "GitHub SSH连接成功";
                    StatusTextBlock.Foreground = (Brush)FindResource("SuccessBrush");
                    BottomStatusTextBlock.Text = message;
                    BottomStatusTextBlock.Foreground = (Brush)FindResource("SuccessBrush");
                }
                else
                {
                    StatusTextBlock.Text = "GitHub SSH连接失败";
                    StatusTextBlock.Foreground = (Brush)FindResource("ErrorBrush");
                    BottomStatusTextBlock.Text = message;
                    BottomStatusTextBlock.Foreground = (Brush)FindResource("ErrorBrush");
                }
            });
        });
    }

    private void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        RunDiagnosis();
    }

    private void UpdateDiagnosticUI(GitHubDiagnosticResult result)
    {
        UpdateCheckIcon(IconSshAvailable, result.SshAvailable);
        UpdateCheckIcon(IconGitAvailable, result.GitAvailable);
        UpdateCheckIcon(IconSshAgentRunning, result.SshAgentRunning);
        UpdateCheckIcon(IconHasAnyKey, result.HasAnyKey);
        UpdateCheckIcon(IconHasMichealKey, result.HasMichealKey);
        UpdateCheckIcon(IconHasGitHubConfig, result.HasGitHubConfig);
        UpdateCheckIcon(IconKnownHostsConfigured, result.KnownHostsConfigured);

        IssueCountTextBlock.Text = result.Issues.Count > 0 ? $"{result.Issues.Count} 个问题" : string.Empty;
        IssuesListView.ItemsSource = new ObservableCollection<string>(result.Issues);

        GitHubSitesListView.ItemsSource = new ObservableCollection<SshSite>(result.GitHubSites);

        if (result.HasMichealKey && result.MichealKeys.Count > 0)
        {
            MichealKeyCard.Visibility = Visibility.Visible;
            MichealKeyPanel.Children.Clear();

            foreach (var key in result.MichealKeys)
            {
                var panel = new StackPanel();
                StackPanelHelper.SetSpacing(panel, 4);

                var nameBlock = new TextBlock
                {
                    Text = $"名称: {key.Name}",
                    FontSize = 13,
                    Foreground = (Brush)FindResource("TextPrimaryBrush")
                };
                panel.Children.Add(nameBlock);

                var pathBlock = new TextBlock
                {
                    Text = $"私钥路径: {key.PrivateKeyPath}",
                    FontSize = 12,
                    Foreground = (Brush)FindResource("TextSecondaryBrush")
                };
                panel.Children.Add(pathBlock);

                var typeBlock = new TextBlock
                {
                    Text = $"类型: {key.Type} ({key.KeySize} bit)",
                    FontSize = 12,
                    Foreground = (Brush)FindResource("TextSecondaryBrush")
                };
                panel.Children.Add(typeBlock);

                if (!string.IsNullOrEmpty(key.Fingerprint))
                {
                    var fpBlock = new TextBlock
                    {
                        Text = $"指纹: {key.Fingerprint}",
                        FontSize = 12,
                        Foreground = (Brush)FindResource("TextSecondaryBrush")
                    };
                    panel.Children.Add(fpBlock);
                }

                MichealKeyPanel.Children.Add(panel);
            }
        }
        else
        {
            MichealKeyCard.Visibility = Visibility.Collapsed;
        }

        StatusTextBlock.Foreground = result.IsHealthy
            ? (Brush)FindResource("SuccessBrush")
            : (Brush)FindResource("TextSecondaryBrush");
    }

    private static void UpdateCheckIcon(TextBlock icon, bool passed)
    {
        icon.Text = passed ? "✅" : "❌";
    }
}
