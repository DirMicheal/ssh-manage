using CommunityToolkit.Mvvm.ComponentModel;
using System.IO;

namespace SshManage.Models;

public partial class SshKey : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _privateKeyPath = string.Empty;

    [ObservableProperty]
    private string _publicKeyPath = string.Empty;

    [ObservableProperty]
    private string _type = "RSA";

    [ObservableProperty]
    private int _keySize = 2048;

    [ObservableProperty]
    private string? _comment;

    [ObservableProperty]
    private bool _hasPermissionIssue;

    [ObservableProperty]
    private string? _permissionStatus;

    public string PublicKeyContent { get; set; } = string.Empty;

    public bool HasPublicKey => File.Exists(PublicKeyPath);

    public bool HasPrivateKey => File.Exists(PrivateKeyPath);
}
