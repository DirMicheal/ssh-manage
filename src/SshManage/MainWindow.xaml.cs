using SshManage.Views;
using System.Windows;
using System.Windows.Controls;

namespace SshManage;

public partial class MainWindow : Window
{
    private readonly SiteManagePage _siteManagePage = new();
    private readonly KeyManagePage _keyManagePage = new();
    private readonly PermissionFixPage _permissionFixPage = new();
    private readonly ConnectionTestPage _connectionTestPage = new();
    private readonly ConfigEditorPage _configEditorPage = new();

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (NavListBox.Items.Count > 0)
        {
            NavListBox.SelectedIndex = 0;
        }
    }

    private void NavListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (NavListBox.SelectedItem is not ListBoxItem item)
            return;

        var tag = item.Tag?.ToString();
        Page? page = tag switch
        {
            "SiteManage" => _siteManagePage,
            "KeyManage" => _keyManagePage,
            "PermissionFix" => _permissionFixPage,
            "ConnectionTest" => _connectionTestPage,
            "ConfigEditor" => _configEditorPage,
            _ => null
        };

        if (page != null)
        {
            MainFrame.Navigate(page);
        }
    }
}
