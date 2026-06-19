using SshManage.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SshManage.Services;

public class SshConfigService
{
    private readonly string _sshConfigPath;
    private readonly string _sshDirectory;
    private readonly string _appDataDirectory;

    public string ConfigPath => _sshConfigPath;
    public string SshDirectory => _sshDirectory;
    public string AppDataDirectory => _appDataDirectory;

    public SshConfigService()
    {
        _sshDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ssh");
        _sshConfigPath = Path.Combine(_sshDirectory, "config");
        _appDataDirectory = Path.Combine(_sshDirectory, "ssh-manage");
    }

    public bool ConfigExists => File.Exists(_sshConfigPath);

    public static string NormalizePath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return path;

        if (path.StartsWith("~") || path.StartsWith("%USERPROFILE%"))
        {
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (path.StartsWith("~"))
                path = userProfile + path.Substring(1);
            else if (path.StartsWith("%USERPROFILE%"))
                path = userProfile + path.Substring("%USERPROFILE%".Length);
        }

        path = path.Replace('/', '\\');
        try
        {
            path = Path.GetFullPath(path);
        }
        catch
        {
        }

        return path;
    }

    public static string ToSshPath(string windowsPath)
    {
        if (string.IsNullOrEmpty(windowsPath))
            return windowsPath;

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (windowsPath.StartsWith(userProfile, StringComparison.OrdinalIgnoreCase))
        {
            return "~" + windowsPath.Substring(userProfile.Length).Replace('\\', '/');
        }

        return windowsPath.Replace('\\', '/');
    }

    public bool HasDuplicateHost(string host, string? excludeHost = null)
    {
        var sites = LoadSites();
        return sites.Any(s => s.Host.Equals(host, StringComparison.OrdinalIgnoreCase)
            && !s.Host.Equals(excludeHost, StringComparison.OrdinalIgnoreCase));
    }

    public List<string> ValidateSite(SshSite site, string? originalHost = null)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(site.Host))
            errors.Add("主机名(Host)不能为空");

        if (string.IsNullOrWhiteSpace(site.HostName))
            errors.Add("服务器地址不能为空");

        if (site.Port <= 0 || site.Port > 65535)
            errors.Add("端口号必须在1-65535之间");

        if (HasDuplicateHost(site.Host, originalHost))
            errors.Add($"主机名 \"{site.Host}\" 已存在，同名站点可能导致配置冲突");

        if (!string.IsNullOrEmpty(site.IdentityFile))
        {
            var normalizedPath = NormalizePath(site.IdentityFile);
            if (!File.Exists(normalizedPath))
                errors.Add($"私钥文件不存在: {site.IdentityFile}");
        }

        if (!string.IsNullOrEmpty(site.ProxyCommand) && site.ProxyCommand.Contains('|'))
        {
            if (!site.ProxyCommand.TrimStart().StartsWith("nc", StringComparison.OrdinalIgnoreCase)
                && !site.ProxyCommand.TrimStart().StartsWith("ssh", StringComparison.OrdinalIgnoreCase)
                && !site.ProxyCommand.TrimStart().StartsWith("connect", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("ProxyCommand 格式可能不正确，请检查命令是否有效");
            }
        }

        return errors;
    }

    public List<SshSite> LoadSites()
    {
        var sites = new List<SshSite>();

        if (!File.Exists(_sshConfigPath))
            return sites;

        var lines = File.ReadAllLines(_sshConfigPath, new UTF8Encoding(false));
        SshSite? currentSite = null;

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();

            if (string.IsNullOrWhiteSpace(trimmedLine) || trimmedLine.StartsWith('#'))
            {
                if (currentSite != null && trimmedLine.StartsWith('#'))
                {
                    var comment = trimmedLine.TrimStart('#').Trim();
                    if (comment.StartsWith("分组:") || comment.StartsWith("Group:"))
                    {
                        currentSite.GroupName = comment.Substring(comment.IndexOf(':') + 1).Trim();
                    }
                    else if (comment.StartsWith("备注:") || comment.StartsWith("Remark:"))
                    {
                        currentSite.Remark = comment.Substring(comment.IndexOf(':') + 1).Trim();
                    }
                }
                continue;
            }

            if (trimmedLine.StartsWith("Host ", StringComparison.OrdinalIgnoreCase))
            {
                if (currentSite != null)
                    sites.Add(currentSite);

                currentSite = new SshSite();
                var hostValue = trimmedLine.Substring(5).Trim();
                currentSite.Host = hostValue;
            }
            else if (currentSite != null)
            {
                var parts = trimmedLine.Split(new[] { ' ', '\t' }, 2, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2)
                {
                    var key = parts[0].Trim();
                    var value = parts[1].Trim();

                    switch (key.ToLower())
                    {
                        case "hostname":
                        case "host name":
                            currentSite.HostName = value;
                            break;
                        case "port":
                            if (int.TryParse(value, out var port))
                                currentSite.Port = port;
                            break;
                        case "user":
                            currentSite.User = value;
                            break;
                        case "identityfile":
                        case "identity file":
                            currentSite.IdentityFile = NormalizePath(value);
                            break;
                        case "proxycommand":
                        case "proxy command":
                            currentSite.ProxyCommand = value;
                            break;
                        case "forwardagent":
                        case "forward agent":
                            currentSite.ForwardAgent = value.Equals("yes", StringComparison.OrdinalIgnoreCase);
                            break;
                        case "proxyjump":
                        case "proxy jump":
                            currentSite.ProxyJump = value;
                            break;
                        case "serveraliveinterval":
                        case "server alive interval":
                            if (int.TryParse(value, out var interval))
                                currentSite.ServerAliveInterval = interval;
                            break;
                        case "serveralivecountmax":
                        case "server alive count max":
                            if (int.TryParse(value, out var max))
                                currentSite.ServerAliveCountMax = max;
                            break;
                        case "compression":
                            currentSite.Compression = value.Equals("yes", StringComparison.OrdinalIgnoreCase);
                            break;
                        default:
                            currentSite.AdditionalOptions[key] = value;
                            break;
                    }
                }
            }
        }

        if (currentSite != null)
            sites.Add(currentSite);

        return sites;
    }

    public void SaveSites(IEnumerable<SshSite> sites)
    {
        EnsureSshDirectory();

        var sb = new StringBuilder();

        sb.AppendLine("# SSH配置文件 - 由SSH配置管理器自动生成");
        sb.AppendLine($"# 生成时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();

        foreach (var site in sites)
        {
            if (!string.IsNullOrEmpty(site.Remark))
            {
                sb.AppendLine($"# 备注: {site.Remark}");
            }
            if (!string.IsNullOrEmpty(site.GroupName))
            {
                sb.AppendLine($"# 分组: {site.GroupName}");
            }

            sb.AppendLine($"Host {site.Host}");
            sb.AppendLine($"\tHostName {site.HostName}");
            sb.AppendLine($"\tPort {site.Port}");
            if (!string.IsNullOrEmpty(site.User))
                sb.AppendLine($"\tUser {site.User}");
            if (!string.IsNullOrEmpty(site.IdentityFile))
                sb.AppendLine($"\tIdentityFile {ToSshPath(site.IdentityFile)}");
            if (!string.IsNullOrEmpty(site.ProxyCommand))
                sb.AppendLine($"\tProxyCommand {site.ProxyCommand}");
            if (site.ForwardAgent)
                sb.AppendLine($"\tForwardAgent yes");
            if (!string.IsNullOrEmpty(site.ProxyJump))
                sb.AppendLine($"\tProxyJump {site.ProxyJump}");
            if (site.ServerAliveInterval > 0)
                sb.AppendLine($"\tServerAliveInterval {site.ServerAliveInterval}");
            if (site.ServerAliveCountMax != 3)
                sb.AppendLine($"\tServerAliveCountMax {site.ServerAliveCountMax}");
            if (site.Compression)
                sb.AppendLine($"\tCompression yes");

            foreach (var opt in site.AdditionalOptions)
            {
                sb.AppendLine($"\t{opt.Key} {opt.Value}");
            }

            sb.AppendLine();
        }

        File.WriteAllText(_sshConfigPath, sb.ToString(), new UTF8Encoding(false));
    }

    public string GetRawConfig()
    {
        if (!File.Exists(_sshConfigPath))
            return string.Empty;

        try
        {
            return File.ReadAllText(_sshConfigPath, new UTF8Encoding(false));
        }
        catch
        {
            try
            {
                return File.ReadAllText(_sshConfigPath, Encoding.Default);
            }
            catch
            {
                return string.Empty;
            }
        }
    }

    public void SaveRawConfig(string content)
    {
        EnsureSshDirectory();
        File.WriteAllText(_sshConfigPath, content, new UTF8Encoding(false));
    }

    public void EnsureSshDirectory()
    {
        if (!Directory.Exists(_sshDirectory))
        {
            Directory.CreateDirectory(_sshDirectory);
        }
    }

    public void EnsureAppDataDirectory()
    {
        EnsureSshDirectory();
        if (!Directory.Exists(_appDataDirectory))
        {
            Directory.CreateDirectory(_appDataDirectory);
        }
    }

    public void BackupConfig(BackupType backupType = BackupType.Manual, string description = "")
    {
        if (!File.Exists(_sshConfigPath)) return;

        EnsureAppDataDirectory();

        var typePrefix = backupType switch
        {
            BackupType.AutoBeforeSave => "autosave",
            BackupType.AutoBeforeDelete => "autodelete",
            _ => "manual"
        };

        var backupPath = Path.Combine(_appDataDirectory, $"config.{typePrefix}.{DateTime.Now:yyyyMMddHHmmss}.bak");
        File.Copy(_sshConfigPath, backupPath);

        var descPath = backupPath + ".desc";
        var desc = string.IsNullOrEmpty(description)
            ? $"{backupType.GetDisplayName()} - {DateTime.Now:yyyy-MM-dd HH:mm:ss}"
            : description;
        File.WriteAllText(descPath, desc, new UTF8Encoding(false));
    }

    public List<BackupRecord> GetBackupRecords()
    {
        var records = new List<BackupRecord>();

        if (!Directory.Exists(_appDataDirectory))
            return records;

        foreach (var file in Directory.GetFiles(_appDataDirectory, "config.*.bak"))
        {
            try
            {
                var fileName = Path.GetFileName(file);
                var parts = fileName.Split('.');
                if (parts.Length < 3) continue;

                var backupType = parts[1] switch
                {
                    "autosave" => BackupType.AutoBeforeSave,
                    "autodelete" => BackupType.AutoBeforeDelete,
                    _ => BackupType.Manual
                };

                var fileInfo = new FileInfo(file);
                var desc = "";
                var descPath = file + ".desc";
                if (File.Exists(descPath))
                {
                    desc = File.ReadAllText(descPath, new UTF8Encoding(false));
                }

                int siteCount = 0;
                try
                {
                    var content = File.ReadAllText(file, new UTF8Encoding(false));
                    siteCount = content.Split('\n').Count(l => l.Trim().StartsWith("Host ", StringComparison.OrdinalIgnoreCase));
                }
                catch { }

                records.Add(new BackupRecord
                {
                    FilePath = file,
                    Description = desc,
                    BackupType = backupType,
                    CreatedAt = fileInfo.CreationTime,
                    FileSize = fileInfo.Length,
                    SiteCount = siteCount
                });
            }
            catch { }
        }

        return records.OrderByDescending(r => r.CreatedAt).ToList();
    }

    public bool RestoreBackup(string backupFilePath)
    {
        if (!File.Exists(backupFilePath))
            return false;

        try
        {
            BackupConfig(BackupType.AutoBeforeSave, "恢复备份前自动保存");
            File.Copy(backupFilePath, _sshConfigPath, true);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool DeleteBackup(string backupFilePath)
    {
        try
        {
            if (File.Exists(backupFilePath))
                File.Delete(backupFilePath);

            var descPath = backupFilePath + ".desc";
            if (File.Exists(descPath))
                File.Delete(descPath);

            return true;
        }
        catch
        {
            return false;
        }
    }

    public void CleanupOldBackups(int maxBackups = 20)
    {
        var records = GetBackupRecords();
        if (records.Count <= maxBackups) return;

        var toDelete = records.Skip(maxBackups).ToList();
        foreach (var record in toDelete)
        {
            DeleteBackup(record.FilePath);
        }
    }

    public List<SshGroup> GetGroups(IEnumerable<SshSite> sites)
    {
        var groups = new Dictionary<string, SshGroup>();
        var defaultGroup = new SshGroup("默认分组");
        groups["默认分组"] = defaultGroup;

        foreach (var site in sites)
        {
            var groupName = string.IsNullOrEmpty(site.GroupName) ? "默认分组" : site.GroupName;
            if (!groups.ContainsKey(groupName))
            {
                groups[groupName] = new SshGroup(groupName);
            }
            groups[groupName].Sites.Add(site);
        }

        return groups.Values.ToList();
    }
}

public static class BackupTypeExtensions
{
    public static string GetDisplayName(this BackupType type) => type switch
    {
        BackupType.Manual => "手动备份",
        BackupType.AutoBeforeSave => "保存前自动备份",
        BackupType.AutoBeforeDelete => "删除前自动备份",
        _ => "未知"
    };
}
