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

    public string ConfigPath => _sshConfigPath;
    public string SshDirectory => _sshDirectory;

    public SshConfigService()
    {
        _sshDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ssh");
        _sshConfigPath = Path.Combine(_sshDirectory, "config");
    }

    public bool ConfigExists => File.Exists(_sshConfigPath);

    public List<SshSite> LoadSites()
    {
        var sites = new List<SshSite>();

        if (!File.Exists(_sshConfigPath))
            return sites;

        var lines = File.ReadAllLines(_sshConfigPath);
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
                            currentSite.IdentityFile = value;
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
                sb.AppendLine($"\tIdentityFile {site.IdentityFile}");

            foreach (var opt in site.AdditionalOptions)
            {
                sb.AppendLine($"\t{opt.Key} {opt.Value}");
            }

            sb.AppendLine();
        }

        File.WriteAllText(_sshConfigPath, sb.ToString());
    }

    public string GetRawConfig()
    {
        return File.Exists(_sshConfigPath) ? File.ReadAllText(_sshConfigPath) : string.Empty;
    }

    public void SaveRawConfig(string content)
    {
        EnsureSshDirectory();
        File.WriteAllText(_sshConfigPath, content);
    }

    public void EnsureSshDirectory()
    {
        if (!Directory.Exists(_sshDirectory))
        {
            Directory.CreateDirectory(_sshDirectory);
        }
    }

    public void BackupConfig()
    {
        if (!File.Exists(_sshConfigPath)) return;

        var backupPath = $"{_sshConfigPath}.backup.{DateTime.Now:yyyyMMddHHmmss}";
        File.Copy(_sshConfigPath, backupPath);
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
