using SshManage.Services;

namespace SshManage.Services;

public static class ServiceLocator
{
    public static SshConfigService ConfigService { get; } = new();
    public static SshKeyService KeyService { get; } = new();
    public static ConnectionTestService ConnectionTestService { get; } = new();
}
