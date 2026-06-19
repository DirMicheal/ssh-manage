using SshManage.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace SshManage.Services;

public class MigrationService
{
    private readonly SshConfigService _configService;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public MigrationService(SshConfigService configService)
    {
        _configService = configService;
    }

    public bool ExportToJson(string filePath, IEnumerable<SshSite> sites, string? remark = null)
    {
        try
        {
            var data = new MigrationData
            {
                Version = "2.0",
                ExportTime = DateTime.Now,
                Remark = remark ?? "SSH配置管理器导出",
                Sites = sites.Select(s => new MigrationSiteData
                {
                    Host = s.Host,
                    HostName = s.HostName,
                    Port = s.Port,
                    User = s.User,
                    IdentityFile = s.IdentityFile,
                    GroupName = s.GroupName,
                    Remark = s.Remark,
                    ProxyCommand = s.ProxyCommand,
                    ForwardAgent = s.ForwardAgent,
                    ProxyJump = s.ProxyJump,
                    ServerAliveInterval = s.ServerAliveInterval,
                    ServerAliveCountMax = s.ServerAliveCountMax,
                    Compression = s.Compression,
                    AdditionalOptions = s.AdditionalOptions
                }).ToList()
            };

            var json = JsonSerializer.Serialize(data, _jsonOptions);
            File.WriteAllText(filePath, json, new UTF8Encoding(false));
            return true;
        }
        catch
        {
            return false;
        }
    }

    public (bool Success, List<SshSite> Sites, string Message) ImportFromJson(string filePath)
    {
        try
        {
            var json = File.ReadAllText(filePath, new UTF8Encoding(false));
            var data = JsonSerializer.Deserialize<MigrationData>(json);

            if (data == null || data.Sites == null)
                return (false, new List<SshSite>(), "导入文件格式无效");

            var sites = new List<SshSite>();
            foreach (var siteData in data.Sites)
            {
                var site = new SshSite
                {
                    Host = siteData.Host ?? string.Empty,
                    HostName = siteData.HostName ?? string.Empty,
                    Port = siteData.Port > 0 ? siteData.Port : 22,
                    User = siteData.User ?? string.Empty,
                    IdentityFile = siteData.IdentityFile,
                    GroupName = siteData.GroupName,
                    Remark = siteData.Remark,
                    ProxyCommand = siteData.ProxyCommand,
                    ForwardAgent = siteData.ForwardAgent,
                    ProxyJump = siteData.ProxyJump,
                    ServerAliveInterval = siteData.ServerAliveInterval,
                    ServerAliveCountMax = siteData.ServerAliveCountMax > 0 ? siteData.ServerAliveCountMax : 3,
                    Compression = siteData.Compression,
                    AdditionalOptions = siteData.AdditionalOptions ?? new Dictionary<string, string>()
                };

                sites.Add(site);
            }

            return (true, sites, $"成功导入 {sites.Count} 个站点配置");
        }
        catch (Exception ex)
        {
            return (false, new List<SshSite>(), $"导入失败: {ex.Message}");
        }
    }

    public bool ExportToOpensshConfig(string filePath, IEnumerable<SshSite> sites)
    {
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine("# SSH配置文件 - 由SSH配置管理器导出");
            sb.AppendLine($"# 导出时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();

            foreach (var site in sites)
            {
                if (!string.IsNullOrEmpty(site.Remark))
                    sb.AppendLine($"# 备注: {site.Remark}");
                if (!string.IsNullOrEmpty(site.GroupName))
                    sb.AppendLine($"# 分组: {site.GroupName}");

                sb.AppendLine($"Host {site.Host}");
                sb.AppendLine($"\tHostName {site.HostName}");
                sb.AppendLine($"\tPort {site.Port}");
                if (!string.IsNullOrEmpty(site.User))
                    sb.AppendLine($"\tUser {site.User}");
                if (!string.IsNullOrEmpty(site.IdentityFile))
                    sb.AppendLine($"\tIdentityFile {SshConfigService.ToSshPath(site.IdentityFile)}");
                if (!string.IsNullOrEmpty(site.ProxyCommand))
                    sb.AppendLine($"\tProxyCommand {site.ProxyCommand}");
                if (site.ForwardAgent)
                    sb.AppendLine($"\tForwardAgent yes");
                if (!string.IsNullOrEmpty(site.ProxyJump))
                    sb.AppendLine($"\tProxyJump {site.ProxyJump}");
                if (site.ServerAliveInterval > 0)
                    sb.AppendLine($"\tServerAliveInterval {site.ServerAliveInterval}");
                if (site.Compression)
                    sb.AppendLine($"\tCompression yes");

                foreach (var opt in site.AdditionalOptions)
                    sb.AppendLine($"\t{opt.Key} {opt.Value}");

                sb.AppendLine();
            }

            File.WriteAllText(filePath, sb.ToString(), new UTF8Encoding(false));
            return true;
        }
        catch
        {
            return false;
        }
    }

    public (bool Success, List<SshSite> Sites, string Message) ImportFromOpensshConfig(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
                return (false, new List<SshSite>(), "文件不存在");

            var tempConfigPath = filePath;
            var originalConfig = _configService.ConfigPath;

            var tempService = new TempConfigService(tempConfigPath);
            var sites = tempService.LoadSites();

            return (true, sites, $"成功导入 {sites.Count} 个站点配置");
        }
        catch (Exception ex)
        {
            return (false, new List<SshSite>(), $"导入失败: {ex.Message}");
        }
    }

    public List<string> ValidateImportedSites(List<SshSite> importedSites, List<SshSite> existingSites)
    {
        var warnings = new List<string>();
        var existingHosts = existingSites.Select(s => s.Host.ToLowerInvariant()).ToHashSet();

        foreach (var site in importedSites)
        {
            if (existingHosts.Contains(site.Host.ToLowerInvariant()))
            {
                warnings.Add($"主机名 \"{site.Host}\" 与现有配置冲突，导入后将覆盖");
            }

            if (!string.IsNullOrEmpty(site.IdentityFile))
            {
                var normalizedPath = SshConfigService.NormalizePath(site.IdentityFile);
                if (!File.Exists(normalizedPath))
                {
                    warnings.Add($"站点 \"{site.Host}\" 引用的私钥文件不存在: {site.IdentityFile}");
                }
            }
        }

        return warnings;
    }
}

internal class TempConfigService
{
    private readonly string _configPath;

    public TempConfigService(string configPath)
    {
        _configPath = configPath;
    }

    public List<SshSite> LoadSites()
    {
        var sites = new List<SshSite>();
        if (!File.Exists(_configPath)) return sites;

        var lines = File.ReadAllLines(_configPath, new UTF8Encoding(false));
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
                        currentSite.GroupName = comment.Substring(comment.IndexOf(':') + 1).Trim();
                    else if (comment.StartsWith("备注:") || comment.StartsWith("Remark:"))
                        currentSite.Remark = comment.Substring(comment.IndexOf(':') + 1).Trim();
                }
                continue;
            }

            if (trimmedLine.StartsWith("Host ", StringComparison.OrdinalIgnoreCase))
            {
                if (currentSite != null) sites.Add(currentSite);
                currentSite = new SshSite { Host = trimmedLine.Substring(5).Trim() };
            }
            else if (currentSite != null)
            {
                var parts = trimmedLine.Split(new[] { ' ', '\t' }, 2, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2)
                {
                    var key = parts[0].Trim().ToLower();
                    var value = parts[1].Trim();
                    switch (key)
                    {
                        case "hostname": case "host name": currentSite.HostName = value; break;
                        case "port": if (int.TryParse(value, out var p)) currentSite.Port = p; break;
                        case "user": currentSite.User = value; break;
                        case "identityfile": case "identity file": currentSite.IdentityFile = SshConfigService.NormalizePath(value); break;
                        case "proxycommand": case "proxy command": currentSite.ProxyCommand = value; break;
                        case "forwardagent": case "forward agent": currentSite.ForwardAgent = value.Equals("yes", StringComparison.OrdinalIgnoreCase); break;
                        case "proxyjump": case "proxy jump": currentSite.ProxyJump = value; break;
                        case "serveraliveinterval": if (int.TryParse(value, out var i)) currentSite.ServerAliveInterval = i; break;
                        case "compression": currentSite.Compression = value.Equals("yes", StringComparison.OrdinalIgnoreCase); break;
                        default: currentSite.AdditionalOptions[parts[0].Trim()] = value; break;
                    }
                }
            }
        }

        if (currentSite != null) sites.Add(currentSite);
        return sites;
    }
}

public class MigrationData
{
    public string Version { get; set; } = "2.0";
    public DateTime ExportTime { get; set; }
    public string Remark { get; set; } = string.Empty;
    public List<MigrationSiteData> Sites { get; set; } = new();
}

public class MigrationSiteData
{
    public string Host { get; set; } = string.Empty;
    public string HostName { get; set; } = string.Empty;
    public int Port { get; set; } = 22;
    public string User { get; set; } = string.Empty;
    public string? IdentityFile { get; set; }
    public string? GroupName { get; set; }
    public string? Remark { get; set; }
    public string? ProxyCommand { get; set; }
    public bool ForwardAgent { get; set; }
    public string? ProxyJump { get; set; }
    public int ServerAliveInterval { get; set; }
    public int ServerAliveCountMax { get; set; } = 3;
    public bool Compression { get; set; }
    public Dictionary<string, string>? AdditionalOptions { get; set; }
}
