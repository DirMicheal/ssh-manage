using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

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
    private bool _isExpanded;

    public Dictionary<string, string> AdditionalOptions { get; set; } = new();
}
