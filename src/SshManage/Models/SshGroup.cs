using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace SshManage.Models;

public partial class SshGroup : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private bool _isExpanded = true;

    public ObservableCollection<SshSite> Sites { get; set; } = new();

    public SshGroup(string name)
    {
        _name = name;
    }
}
