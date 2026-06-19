using SshManage.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace SshManage.Services;

public class SshKeyService
{
    private readonly string _sshDirectory;
    private const string MICHEAL_KEY_IDENTIFIER = "micheal";

    public string SshDirectory => _sshDirectory;

    public SshKeyService()
    {
        _sshDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ssh");
    }

    public List<SshKey> GetAllKeys()
    {
        var keys = new List<SshKey>();

        if (!Directory.Exists(_sshDirectory))
            return keys;

        var privateKeyFiles = new List<string>();
        var publicKeyFiles = new Dictionary<string, string>();

        foreach (var file in Directory.GetFiles(_sshDirectory))
        {
            var fileName = Path.GetFileName(file);
            if (fileName.EndsWith(".pub"))
            {
                var keyName = fileName.Substring(0, fileName.Length - 4);
                publicKeyFiles[keyName] = file;
            }
            else if (!fileName.Contains('.'))
            {
                if (IsPrivateKey(file))
                {
                    privateKeyFiles.Add(file);
                }
            }
        }

        var keyNames = new HashSet<string>();
        foreach (var privFile in privateKeyFiles)
        {
            keyNames.Add(Path.GetFileName(privFile));
        }
        foreach (var pubName in publicKeyFiles.Keys)
        {
            keyNames.Add(pubName);
        }

        foreach (var keyName in keyNames.OrderBy(k => k))
        {
            var key = new SshKey
            {
                Name = keyName,
                PrivateKeyPath = Path.Combine(_sshDirectory, keyName),
                PublicKeyPath = Path.Combine(_sshDirectory, keyName + ".pub"),
                Type = DetectKeyType(Path.Combine(_sshDirectory, keyName))
            };

            key.HasPermissionIssue = CheckPermissionIssue(key.PrivateKeyPath);
            key.PermissionStatus = key.HasPermissionIssue ? "权限异常" : "权限正常";
            key.IsMichealKey = DetectMichealKey(key);

            if (key.HasPublicKey)
            {
                try
                {
                    key.PublicKeyContent = File.ReadAllText(key.PublicKeyPath, new UTF8Encoding(false)).Trim();
                }
                catch
                {
                    key.PublicKeyContent = string.Empty;
                }
            }

            key.Fingerprint = GetKeyFingerprint(key);

            keys.Add(key);
        }

        return keys;
    }

    private bool DetectMichealKey(SshKey key)
    {
        if (!key.HasPrivateKey)
            return false;

        var nameLower = key.Name.ToLowerInvariant();
        if (nameLower.Contains(MICHEAL_KEY_IDENTIFIER))
            return true;

        if (key.HasPublicKey)
        {
            try
            {
                var content = key.PublicKeyContent.ToLowerInvariant();
                if (content.Contains(MICHEAL_KEY_IDENTIFIER))
                    return true;
            }
            catch { }
        }

        if (!string.IsNullOrEmpty(key.Comment) && key.Comment.ToLowerInvariant().Contains(MICHEAL_KEY_IDENTIFIER))
            return true;

        return false;
    }

    private string? GetKeyFingerprint(SshKey key)
    {
        if (!key.HasPublicKey)
            return null;

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "ssh-keygen",
                Arguments = $"-l -f \"{key.PublicKeyPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(startInfo);
            if (process == null) return null;

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(5000);

            if (process.ExitCode == 0 && !string.IsNullOrEmpty(output))
            {
                return output.Trim();
            }
        }
        catch { }

        return null;
    }

    private bool IsPrivateKey(string filePath)
    {
        try
        {
            var firstLine = File.ReadLines(filePath).FirstOrDefault();
            return firstLine != null && firstLine.Contains("PRIVATE KEY");
        }
        catch
        {
            return false;
        }
    }

    private string DetectKeyType(string privateKeyPath)
    {
        if (!File.Exists(privateKeyPath))
            return "未知";

        try
        {
            var content = File.ReadAllText(privateKeyPath, new UTF8Encoding(false));
            if (content.Contains("BEGIN RSA PRIVATE KEY") || content.Contains("RSA PRIVATE KEY"))
                return "RSA";
            if (content.Contains("BEGIN EC PRIVATE KEY") || content.Contains("ECDSA"))
                return "ECDSA";
            if (content.Contains("BEGIN OPENSSH PRIVATE KEY"))
            {
                if (content.Contains("ssh-ed25519"))
                    return "Ed25519";
                return "OpenSSH";
            }
            if (content.Contains("BEGIN DSA PRIVATE KEY"))
                return "DSA";
            return "未知";
        }
        catch
        {
            return "未知";
        }
    }

    public bool CheckPermissionIssue(string privateKeyPath)
    {
        if (!File.Exists(privateKeyPath))
            return false;

        try
        {
            var fileInfo = new FileInfo(privateKeyPath);
            var fileSecurity = fileInfo.GetAccessControl();
            var rules = fileSecurity.GetAccessRules(true, true, typeof(NTAccount));

            var currentUser = WindowsIdentity.GetCurrent().Name;
            var adminSid = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
            var systemSid = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);

            foreach (FileSystemAccessRule rule in rules)
            {
                if (rule.AccessControlType != AccessControlType.Allow)
                    continue;

                var identity = rule.IdentityReference;

                if (identity.Value == currentUser)
                    continue;

                try
                {
                    if (identity is SecurityIdentifier sid)
                    {
                        if (sid.Equals(adminSid) || sid.Equals(systemSid))
                            continue;
                    }
                }
                catch { }

                try
                {
                    var ntAccount = identity.Translate(typeof(NTAccount)) as NTAccount;
                    if (ntAccount != null)
                    {
                        var name = ntAccount.Value.ToLower();
                        if (name.Contains("administrators") || name.Contains("system"))
                            continue;
                    }
                }
                catch { }

                if ((rule.FileSystemRights & FileSystemRights.Read) != 0)
                {
                    return true;
                }
            }

            return false;
        }
        catch
        {
            return true;
        }
    }

    public bool FixPermission(string privateKeyPath)
    {
        if (!File.Exists(privateKeyPath))
            return false;

        try
        {
            var fileInfo = new FileInfo(privateKeyPath);
            var fileSecurity = new FileSecurity();

            var currentUser = WindowsIdentity.GetCurrent().Name;

            fileSecurity.SetOwner(new NTAccount(currentUser));

            fileSecurity.AddAccessRule(new FileSystemAccessRule(
                currentUser,
                FileSystemRights.FullControl,
                AccessControlType.Allow));

            fileSecurity.AddAccessRule(new FileSystemAccessRule(
                new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
                FileSystemRights.FullControl,
                AccessControlType.Allow));

            fileSecurity.AddAccessRule(new FileSystemAccessRule(
                new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
                FileSystemRights.FullControl,
                AccessControlType.Allow));

            fileSecurity.SetAccessRuleProtection(true, false);

            fileInfo.SetAccessControl(fileSecurity);

            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> GenerateKeyAsync(string keyName, KeyType type, int keySize, string? passphrase = null, string? comment = null)
    {
        var keyPath = Path.Combine(_sshDirectory, keyName);

        if (File.Exists(keyPath))
            return false;

        try
        {
            Directory.CreateDirectory(_sshDirectory);

            var keyTypeName = type switch
            {
                KeyType.RSA => "rsa",
                KeyType.ECDSA => "ecdsa",
                KeyType.Ed25519 => "ed25519",
                KeyType.DSA => "dsa",
                _ => "rsa"
            };

            var args = new List<string>
            {
                "-t", keyTypeName,
                "-b", keySize.ToString(),
                "-f", keyPath,
                "-N", passphrase ?? string.Empty
            };

            if (!string.IsNullOrEmpty(comment))
            {
                args.Add("-C");
                args.Add(comment);
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = "ssh-keygen",
                Arguments = string.Join(" ", args),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
                return false;

            await process.WaitForExitAsync();

            return process.ExitCode == 0 && File.Exists(keyPath);
        }
        catch
        {
            return false;
        }
    }

    public string? GetPublicKeyContent(string publicKeyPath)
    {
        if (!File.Exists(publicKeyPath))
            return null;

        try
        {
            return File.ReadAllText(publicKeyPath, new UTF8Encoding(false)).Trim();
        }
        catch
        {
            return null;
        }
    }

    public bool CopyPublicKeyToClipboard(string publicKeyPath)
    {
        var content = GetPublicKeyContent(publicKeyPath);
        if (string.IsNullOrEmpty(content))
            return false;

        try
        {
            System.Windows.Clipboard.SetText(content);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool DeleteKey(string keyName)
    {
        var privateKeyPath = Path.Combine(_sshDirectory, keyName);
        var publicKeyPath = Path.Combine(_sshDirectory, keyName + ".pub");

        var deleted = false;

        if (File.Exists(privateKeyPath))
        {
            try
            {
                File.Delete(privateKeyPath);
                deleted = true;
            }
            catch { }
        }

        if (File.Exists(publicKeyPath))
        {
            try
            {
                File.Delete(publicKeyPath);
                deleted = true;
            }
            catch { }
        }

        return deleted;
    }

    public bool RenameKey(string oldName, string newName)
    {
        var oldPrivatePath = Path.Combine(_sshDirectory, oldName);
        var oldPublicPath = Path.Combine(_sshDirectory, oldName + ".pub");
        var newPrivatePath = Path.Combine(_sshDirectory, newName);
        var newPublicPath = Path.Combine(_sshDirectory, newName + ".pub");

        if (File.Exists(newPrivatePath) || File.Exists(newPublicPath))
            return false;

        var renamed = false;

        if (File.Exists(oldPrivatePath))
        {
            try
            {
                File.Move(oldPrivatePath, newPrivatePath);
                renamed = true;
            }
            catch { return false; }
        }

        if (File.Exists(oldPublicPath))
        {
            try
            {
                File.Move(oldPublicPath, newPublicPath);
                renamed = true;
            }
            catch
            {
                if (File.Exists(newPrivatePath))
                {
                    File.Move(newPrivatePath, oldPrivatePath);
                }
                return false;
            }
        }

        return renamed;
    }

    public List<SshKey> GetAgentKeys()
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "ssh-add",
                Arguments = "-l",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(startInfo);
            if (process == null) return new List<SshKey>();

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(5000);

            if (process.ExitCode != 0)
                return new List<SshKey>();

            var agentKeys = new List<SshKey>();
            foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    var type = parts[0];
                    var fingerprint = parts[1];

                    string keyName = "agent-key";
                    if (parts.Length >= 4)
                    {
                        var lastPart = parts[^1];
                        if (lastPart.Contains('(') && lastPart.Contains(')'))
                        {
                            var start = lastPart.IndexOf('(') + 1;
                            var end = lastPart.IndexOf(')');
                            if (start < end)
                                keyName = lastPart.Substring(start, end - start);
                        }
                    }

                    agentKeys.Add(new SshKey
                    {
                        Name = keyName,
                        Type = type,
                        Fingerprint = fingerprint,
                        Comment = "Agent中的密钥"
                    });
                }
            }

            return agentKeys;
        }
        catch
        {
            return new List<SshKey>();
        }
    }

    public bool AddKeyToAgent(string privateKeyPath)
    {
        if (!File.Exists(privateKeyPath))
            return false;

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "ssh-add",
                Arguments = $"\"{privateKeyPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(startInfo);
            if (process == null) return false;

            process.WaitForExit(10000);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public bool RemoveKeyFromAgent(string privateKeyPath)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "ssh-add",
                Arguments = $"-d \"{privateKeyPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(startInfo);
            if (process == null) return false;

            process.WaitForExit(10000);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public bool ClearAgent()
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "ssh-add",
                Arguments = "-D",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(startInfo);
            if (process == null) return false;

            process.WaitForExit(10000);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
