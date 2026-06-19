using CommunityToolkit.Mvvm.ComponentModel;

namespace SshManage.Models;

public partial class SshSite : ObservableObject
{
    [ObservableProperty]
    private string _host = string.Empty;

    [ObservableProperty]
    private string _hostName = string.Empty;

    [ObservableProperty]
    private int _port = 22;

    [ObservableProperty]
    private string _user = string.Empty;

    [ObservableProperty]
    private string? _identityFile;

    [ObservableProperty]
    private string? _groupName;

    [ObservableProperty]
    private string? _remark;

    [ObservableProperty]
    private string? _proxyCommand;

    [ObservableProperty]
    private bool _forwardAgent;

    [ObservableProperty]
    private string? _proxyJump;

    [ObservableProperty]
    private int _serverAliveInterval;

    [ObservableProperty]
    private int _serverAliveCountMax = 3;

    [ObservableProperty]
    private bool _compression;

    [ObservableProperty]
    private bool _isExpanded;

    public Dictionary<string, string> AdditionalOptions { get; set; } = new();

    public string DisplayName
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Remark))
                return $"{Host} ({Remark})";
            return Host;
        }
    }
}
