using SshManage.Services;

namespace SshManage.Services;

public static class ServiceLocator
{
    public static SshConfigService ConfigService { get; } = new();
    public static SshKeyService KeyService { get; } = new();
    public static ConnectionTestService ConnectionTestService { get; } = new();
    public static TemplateService TemplateService { get; } = new();
    public static MigrationService MigrationService { get; } = new(ConfigService);
    public static GitHubFixService GitHubFixService { get; } = new(KeyService, ConfigService);
}
