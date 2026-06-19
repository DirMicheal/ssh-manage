using CommunityToolkit.Mvvm.ComponentModel;

namespace SshManage.Models;

public partial class SiteTemplate : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

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
    private string? _remark;

    [ObservableProperty]
    private DateTime _createdAt = DateTime.Now;

    public Dictionary<string, string> AdditionalOptions { get; set; } = new();

    public static SiteTemplate FromSite(SshSite site, string templateName, string description = "")
    {
        return new SiteTemplate
        {
            Name = templateName,
            Description = description,
            HostName = site.HostName,
            Port = site.Port,
            User = site.User,
            IdentityFile = site.IdentityFile,
            GroupName = site.GroupName,
            ProxyCommand = site.ProxyCommand,
            ForwardAgent = site.ForwardAgent,
            ProxyJump = site.ProxyJump,
            ServerAliveInterval = site.ServerAliveInterval,
            ServerAliveCountMax = site.ServerAliveCountMax,
            Compression = site.Compression,
            Remark = site.Remark,
            AdditionalOptions = new Dictionary<string, string>(site.AdditionalOptions)
        };
    }

    public SshSite ToSite(string host)
    {
        return new SshSite
        {
            Host = host,
            HostName = HostName,
            Port = Port,
            User = User,
            IdentityFile = IdentityFile,
            GroupName = GroupName,
            ProxyCommand = ProxyCommand,
            ForwardAgent = ForwardAgent,
            ProxyJump = ProxyJump,
            ServerAliveInterval = ServerAliveInterval,
            ServerAliveCountMax = ServerAliveCountMax,
            Compression = Compression,
            Remark = Remark,
            AdditionalOptions = new Dictionary<string, string>(AdditionalOptions)
        };
    }
}
