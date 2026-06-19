using CommunityToolkit.Mvvm.ComponentModel;

namespace SshManage.Models;

public partial class BackupRecord : ObservableObject
{
    [ObservableProperty]
    private string _filePath = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private BackupType _backupType;

    [ObservableProperty]
    private DateTime _createdAt;

    [ObservableProperty]
    private long _fileSize;

    [ObservableProperty]
    private int _siteCount;

    public string DisplayType => BackupType switch
    {
        BackupType.Manual => "手动备份",
        BackupType.AutoBeforeSave => "保存前自动",
        BackupType.AutoBeforeDelete => "删除前自动",
        _ => "未知"
    };

    public string DisplaySize
    {
        get
        {
            if (FileSize < 1024) return $"{FileSize} B";
            if (FileSize < 1024 * 1024) return $"{FileSize / 1024.0:F1} KB";
            return $"{FileSize / (1024.0 * 1024.0):F1} MB";
        }
    }
}
