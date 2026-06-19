using SshManage.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace SshManage.Services;

public class GitHubFixService
{
    private readonly SshKeyService _keyService;
    private readonly SshConfigService _configService;

    public GitHubFixService(SshKeyService keyService, SshConfigService configService)
    {
        _keyService = keyService;
        _configService = configService;
    }

    public GitHubDiagnosticResult Diagnose()
    {
        var result = new GitHubDiagnosticResult();

        result.SshAvailable = CheckSshAvailable();
        result.GitAvailable = CheckGitAvailable();
        result.SshAgentRunning = CheckSshAgentRunning();

        var keys = _keyService.GetAllKeys();
        result.HasAnyKey = keys.Any(k => k.HasPrivateKey);
        result.HasMichealKey = keys.Any(k => k.IsMichealKey);
        result.MichealKeys = keys.Where(k => k.IsMichealKey).ToList();
        result.KeysWithPermissionIssue = keys.Where(k => k.HasPrivateKey && k.HasPermissionIssue).ToList();

        var sites = _configService.LoadSites();
        var githubSites = sites.Where(s =>
            s.HostName.Contains("github.com", StringComparison.OrdinalIgnoreCase) ||
            s.Host.Contains("github", StringComparison.OrdinalIgnoreCase)).ToList();
        result.GitHubSites = githubSites;
        result.HasGitHubConfig = githubSites.Count > 0;

        if (result.HasGitHubConfig)
        {
            foreach (var site in githubSites)
            {
                if (string.IsNullOrEmpty(site.IdentityFile))
                    result.Issues.Add($"GitHub站点 \"{site.Host}\" 未配置私钥文件");
                else
                {
                    var normalizedPath = SshConfigService.NormalizePath(site.IdentityFile);
                    if (!File.Exists(normalizedPath))
                        result.Issues.Add($"GitHub站点 \"{site.Host}\" 的私钥文件不存在: {site.IdentityFile}");
                }
            }
        }
        else
        {
            result.Issues.Add("未找到GitHub SSH配置（Host包含github的站点）");
        }

        if (!result.SshAgentRunning)
            result.Issues.Add("SSH Agent服务未运行，可能导致密钥认证失败");

        if (result.KeysWithPermissionIssue.Count > 0)
            result.Issues.Add($"{result.KeysWithPermissionIssue.Count} 个私钥文件权限异常");

        if (!result.SshAvailable)
            result.Issues.Add("系统未安装OpenSSH客户端");

        result.KnownHostsConfigured = CheckGitHubKnownHosts();

        return result;
    }

    public bool FixAll(GitHubDiagnosticResult diagnostic)
    {
        var allSuccess = true;

        if (diagnostic.KeysWithPermissionIssue.Count > 0)
        {
            foreach (var key in diagnostic.KeysWithPermissionIssue)
            {
                if (!_keyService.FixPermission(key.PrivateKeyPath))
                    allSuccess = false;
            }
        }

        if (!diagnostic.HasGitHubConfig && diagnostic.HasAnyKey)
        {
            var defaultKey = diagnostic.MichealKeys.FirstOrDefault()
                ?? _keyService.GetAllKeys().FirstOrDefault(k => k.HasPrivateKey);

            if (defaultKey != null)
            {
                var sites = _configService.LoadSites();
                sites.Add(new SshSite
                {
                    Host = "github.com",
                    HostName = "github.com",
                    User = "git",
                    IdentityFile = defaultKey.PrivateKeyPath,
                    GroupName = "GitHub",
                    Remark = "GitHub SSH配置（自动创建）"
                });
                _configService.BackupConfig(BackupType.AutoBeforeSave, "GitHub一键修复前自动备份");
                _configService.SaveSites(sites);
            }
            else
            {
                allSuccess = false;
            }
        }

        if (!diagnostic.KnownHostsConfigured)
        {
            AddGitHubKnownHosts();
        }

        if (!diagnostic.SshAgentRunning)
        {
            TryStartSshAgent();
        }

        return allSuccess;
    }

    private bool CheckSshAvailable()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "ssh",
                Arguments = "-V",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };

            using var process = Process.Start(psi);
            if (process == null) return false;
            process.WaitForExit(5000);
            return true;
        }
        catch { return false; }
    }

    private bool CheckGitAvailable()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "--version",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true
            };

            using var process = Process.Start(psi);
            if (process == null) return false;
            process.WaitForExit(5000);
            return process.ExitCode == 0;
        }
        catch { return false; }
    }

    private bool CheckSshAgentRunning()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "ssh-add",
                Arguments = "-l",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };

            using var process = Process.Start(psi);
            if (process == null) return false;
            process.WaitForExit(5000);
            return process.ExitCode == 0 || process.ExitCode == 1;
        }
        catch { return false; }
    }

    private bool CheckGitHubKnownHosts()
    {
        try
        {
            var knownHostsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".ssh", "known_hosts");

            if (!File.Exists(knownHostsPath))
                return false;

            var content = File.ReadAllText(knownHostsPath, new UTF8Encoding(false));
            return content.Contains("github.com", StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    private void AddGitHubKnownHosts()
    {
        try
        {
            _configService.EnsureSshDirectory();
            var knownHostsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".ssh", "known_hosts");

            var githubKeys = new[]
            {
                "github.com ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIOMqqnkVzrm0SdG6UOoqKLsabgH5C9okWi0dh2l9GKJl",
                "github.com ecdsa-sha2-nistp256 AAAAE2VjZHNhLXNoYTItbmlzdHAyNTYAAAAIbmlzdHAyNTYAAABBBEmKSENjQEezOmxkZMy7opKgwFB9nkt5YRrYMjNuG5N87uRgg6CLrbo5wAdT/y6v0mKV0U2w0WZ2YB/++Tpockg=",
                "github.com ssh-rsa AAAAB3NzaC1yc2EAAAADAQABAAABgQCj7ndNxQowgcQnjshcLrqPEiiphnt+VTTvDP6mHBL9j1aNUkY4Ue1gvwnGLVlOhGeYrnZaMgRK6+DBm0js1V9JYj9kSEgZWLUU4RXlNE2j3rNi1rJlYzF9ly0oWKnS8tGZ5TmT7LiE0oFONiFGYhF1Y2gKKGLM2Kik1Yr0KHkeM8U6HBQh+rMNaIvt2O2OlEgS7b0sAHjxo7r5Tga4DkAhlgn5fKMiiUgHIQWKrXkB7f0iH5K/M+ULW8D3WY6qqoy3QK3wZ1FqVnJFPvKZH9MlnE6gt8OMUFmD8hFFSz6GKSczcm3/S5JfOHwL8Lp5DBE7yI2vNnZ7tY7r7RjNaG8v2Kj+JDti7Ssq7s1B9yKh7lQ8dUZ5nYlXaiE+OH9jGi/5Eg0Cbl1GKsU0r0lRl6+kBz5nHqO+0DcXaCzlW0lV0qOq4i9youoVqW4q4x9ouN6OH7cb2b5GXmE5tR6U2tJe9tkX5VHneEW8Ysw/1I9Cm+fA7E4JQU9D3qNa2w=="
            };

            var existingLines = new HashSet<string>();
            if (File.Exists(knownHostsPath))
            {
                foreach (var line in File.ReadAllLines(knownHostsPath, new UTF8Encoding(false)))
                    existingLines.Add(line.Trim());
            }

            using var writer = new StreamWriter(knownHostsPath, true, new UTF8Encoding(false));
            foreach (var key in githubKeys)
            {
                if (!existingLines.Contains(key.Trim()))
                    writer.WriteLine(key);
            }
        }
        catch { }
    }

    private bool TryStartSshAgent()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = "-Command \"Start-Service ssh-agent -ErrorAction SilentlyContinue\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(psi);
            if (process == null) return false;
            process.WaitForExit(10000);
            return process.ExitCode == 0;
        }
        catch { return false; }
    }

    public async void TestGitHubConnection(Action<bool, string> callback)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "ssh",
                Arguments = "-T git@github.com",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(psi);
            if (process == null)
            {
                callback(false, "无法启动SSH进程");
                return;
            }

            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (error.Contains("successfully authenticated", StringComparison.OrdinalIgnoreCase))
            {
                callback(true, "GitHub SSH连接成功！身份验证通过。");
            }
            else if (error.Contains("Permission denied", StringComparison.OrdinalIgnoreCase))
            {
                callback(false, "权限被拒绝，请检查密钥是否已添加到GitHub账户。");
            }
            else if (error.Contains("Connection refused", StringComparison.OrdinalIgnoreCase))
            {
                callback(false, "连接被拒绝，请检查网络连接。");
            }
            else
            {
                callback(false, $"连接测试结果: {error.Trim()}");
            }
        }
        catch (Exception ex)
        {
            callback(false, $"测试失败: {ex.Message}");
        }
    }
}

public class GitHubDiagnosticResult
{
    public bool SshAvailable { get; set; }
    public bool GitAvailable { get; set; }
    public bool SshAgentRunning { get; set; }
    public bool HasAnyKey { get; set; }
    public bool HasMichealKey { get; set; }
    public List<SshKey> MichealKeys { get; set; } = new();
    public List<SshKey> KeysWithPermissionIssue { get; set; } = new();
    public bool HasGitHubConfig { get; set; }
    public List<SshSite> GitHubSites { get; set; } = new();
    public bool KnownHostsConfigured { get; set; }
    public List<string> Issues { get; set; } = new();

    public bool IsHealthy => Issues.Count == 0 && SshAvailable && HasGitHubConfig && SshAgentRunning;

    public string Summary
    {
        get
        {
            if (IsHealthy) return "GitHub SSH配置正常，可以正常使用";
            return $"发现 {Issues.Count} 个问题需要修复";
        }
    }
}
