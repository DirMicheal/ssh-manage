using SshManage.Models;
using SshManage.Services;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace SshManage.Views;

public partial class AgentManagePage : Page
{
    private readonly SshKeyService _keyService;
    private ObservableCollection<SshKey> _localKeys = new();
    private ObservableCollection<SshKey> _agentKeys = new();

    public AgentManagePage()
    {
        InitializeComponent();
        _keyService = ServiceLocator.KeyService;
        Loaded += AgentManagePage_Loaded;
    }

    private void AgentManagePage_Loaded(object sender, RoutedEventArgs e)
    {
        LoadLocalKeys();
        LoadAgentKeys();
    }

    private void LoadLocalKeys()
    {
        var keys = _keyService.GetAllKeys();
        _localKeys = new ObservableCollection<SshKey>(keys);
        LocalKeyListView.ItemsSource = _localKeys;
        UpdateButtonState();
    }

    private void LoadAgentKeys()
    {
        var keys = _keyService.GetAgentKeys();
        _agentKeys = new ObservableCollection<SshKey>(keys);
        AgentKeyListView.ItemsSource = _agentKeys;
        UpdateAgentStatus();
        UpdateButtonState();
    }

    private void UpdateAgentStatus()
    {
        if (_agentKeys.Count > 0)
        {
            AgentStatusText.Text = $"运行中 ({_agentKeys.Count} 个密钥)";
        }
        else
        {
            AgentStatusText.Text = "未运行或无密钥";
        }
    }

    private void UpdateButtonState()
    {
        var hasLocalSelection = LocalKeyListView.SelectedItem != null;
        var hasAgentSelection = AgentKeyListView.SelectedItem != null;
        BtnAddToAgent.IsEnabled = hasLocalSelection;
        BtnRemoveFromAgent.IsEnabled = hasAgentSelection;
    }

    private void LocalKeyListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateButtonState();
    }

    private void AgentKeyListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateButtonState();
    }

    private void BtnAddToAgent_Click(object sender, RoutedEventArgs e)
    {
        if (LocalKeyListView.SelectedItem is not SshKey selectedKey)
            return;

        if (!selectedKey.HasPrivateKey)
        {
            MessageBox.Show("未找到私钥文件，无法添加到Agent", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var success = _keyService.AddKeyToAgent(selectedKey.PrivateKeyPath);
        if (success)
        {
            StatusText.Text = $"已将密钥 \"{selectedKey.Name}\" 添加到Agent";
            LoadAgentKeys();
        }
        else
        {
            MessageBox.Show("添加密钥到Agent失败，请确认ssh-agent正在运行", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnRemoveFromAgent_Click(object sender, RoutedEventArgs e)
    {
        if (AgentKeyListView.SelectedItem is not SshKey selectedKey)
            return;

        var success = _keyService.RemoveKeyFromAgent(selectedKey.Name);
        if (success)
        {
            StatusText.Text = $"已从Agent移除密钥 \"{selectedKey.Name}\"";
            LoadAgentKeys();
        }
        else
        {
            MessageBox.Show("从Agent移除密钥失败", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnClearAgent_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "确定要清空Agent中的所有密钥吗？\n\n此操作将移除Agent中的所有密钥，可能影响当前SSH连接。",
            "确认清空",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
            return;

        var confirmResult = MessageBox.Show(
            "二次确认：即将清空Agent中所有密钥，是否确认？\n\n清空后使用Agent转发的连接将断开！",
            "二次确认清空",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirmResult != MessageBoxResult.Yes)
            return;

        var success = _keyService.ClearAgent();
        if (success)
        {
            StatusText.Text = "已清空Agent中的所有密钥";
            LoadAgentKeys();
        }
        else
        {
            MessageBox.Show("清空Agent失败，请确认ssh-agent正在运行", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnRefreshAgent_Click(object sender, RoutedEventArgs e)
    {
        LoadAgentKeys();
        StatusText.Text = "Agent密钥列表已刷新";
    }

    private void BtnRefreshKeys_Click(object sender, RoutedEventArgs e)
    {
        LoadLocalKeys();
        LoadAgentKeys();
        StatusText.Text = "密钥列表已刷新";
    }
}
