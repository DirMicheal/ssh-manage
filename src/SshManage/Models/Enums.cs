namespace SshManage.Models;

public enum KeyType
{
    RSA,
    ECDSA,
    Ed25519,
    DSA
}

public enum ConnectionStatus
{
    Unknown,
    Testing,
    Success,
    Failed,
    Timeout
}

public enum ModuleType
{
    SiteManage,
    KeyManage,
    PermissionFix,
    ConnectionTest,
    ConfigEditor,
    About
}
