using SshManage.Models;
using SshManage.Services;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace SshManage.Views;

public partial class TemplateManagePage : Page
{
    private readonly SshConfigService _configService;
    private readonly TemplateService _templateService;
    private ObservableCollection<SiteTemplate> _templates = new();
    private ObservableCollection<SshSite> _sites = new();

    public TemplateManagePage()
    {
        InitializeComponent();
        _configService = ServiceLocator.ConfigService;
        _templateService = ServiceLocator.TemplateService;
        Loaded += TemplateManagePage_Loaded;
    }

    private void TemplateManagePage_Loaded(object sender, RoutedEventArgs e)
    {
        LoadData();
    }

    private void LoadData()
    {
        LoadTemplates();
        LoadSites();
    }

    private void LoadTemplates()
    {
        var templates = _templateService.GetAllTemplates();
        _templates = new ObservableCollection<SiteTemplate>(templates);
        TemplateListView.ItemsSource = _templates;
        UpdateDetailPanel(null);
    }

    private void LoadSites()
    {
        var sites = _configService.LoadSites();
        _sites = new ObservableCollection<SshSite>(sites);
    }

    private void BtnSaveAsTemplate_Click(object sender, RoutedEventArgs e)
    {
        if (_sites.Count == 0)
        {
            MessageBox.Show("暂无站点，请先添加站点。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var selectWindow = new Window
        {
            Title = "选择站点",
            Width = 400,
            Height = 300,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = Window.GetWindow(this),
            ResizeMode = ResizeMode.NoResize,
            Background = SystemColors.WindowBrush
        };

        var panel = new StackPanel { Margin = new Thickness(16) };
        panel.Children.Add(new TextBlock
        {
            Text = "选择要保存为模板的站点：",
            FontSize = 14,
            Margin = new Thickness(0, 0, 0, 12)
        });

        var listBox = new ListBox { Margin = new Thickness(0, 0, 0, 12) };
        foreach (var site in _sites)
        {
            listBox.Items.Add($"{site.Host} ({site.HostName})");
        }
        panel.Children.Add(listBox);

        var btnPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var cancelBtn = new Button { Content = "取消", Width = 80, Margin = new Thickness(8, 0, 0, 0) };
        var confirmBtn = new Button { Content = "确定", Width = 80, Margin = new Thickness(8, 0, 0, 0) };
        btnPanel.Children.Add(cancelBtn);
        btnPanel.Children.Add(confirmBtn);
        panel.Children.Add(btnPanel);

        selectWindow.Content = panel;

        cancelBtn.Click += (s, args) => { selectWindow.DialogResult = false; selectWindow.Close(); };
        confirmBtn.Click += (s, args) =>
        {
            if (listBox.SelectedIndex < 0)
            {
                MessageBox.Show("请选择一个站点。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            selectWindow.DialogResult = true;
            selectWindow.Close();
        };

        if (selectWindow.ShowDialog() != true || listBox.SelectedIndex < 0)
            return;

        var selectedSite = _sites[listBox.SelectedIndex];
        var (name, desc) = ShowTemplateNameDialog();
        if (name == null)
            return;

        if (_templateService.HasDuplicateName(name))
        {
            var overwrite = MessageBox.Show(
                $"模板 \"{name}\" 已存在，是否覆盖？",
                "名称重复",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (overwrite != MessageBoxResult.Yes)
                return;
        }

        var template = SiteTemplate.FromSite(selectedSite, name, desc ?? string.Empty);
        if (_templateService.SaveTemplate(template))
        {
            MessageBox.Show("模板保存成功！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
            LoadTemplates();
        }
        else
        {
            MessageBox.Show("模板保存失败。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnCreateTemplate_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Window
        {
            Title = "新建模板",
            Width = 400,
            Height = 380,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = Window.GetWindow(this),
            ResizeMode = ResizeMode.NoResize,
            Background = SystemColors.WindowBrush
        };

        var panel = new StackPanel { Margin = new Thickness(16) };
        panel.Children.Add(new TextBlock { Text = "模板名称：", FontSize = 13, Margin = new Thickness(0, 0, 0, 4) });
        var nameBox = new TextBox { Margin = new Thickness(0, 0, 0, 12) };
        panel.Children.Add(nameBox);

        panel.Children.Add(new TextBlock { Text = "描述：", FontSize = 13, Margin = new Thickness(0, 0, 0, 4) });
        var descBox = new TextBox { Margin = new Thickness(0, 0, 0, 12) };
        panel.Children.Add(descBox);

        panel.Children.Add(new TextBlock { Text = "服务器地址：", FontSize = 13, Margin = new Thickness(0, 0, 0, 4) });
        var hostNameBox = new TextBox { Margin = new Thickness(0, 0, 0, 12) };
        panel.Children.Add(hostNameBox);

        var portPanel = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
        panel.Children.Add(portPanel);

        panel.Children.Add(new TextBlock { Text = "用户名：", FontSize = 13, Margin = new Thickness(0, 0, 0, 4) });
        var userBox = new TextBox { Margin = new Thickness(0, 0, 0, 12) };
        panel.Children.Add(userBox);

        panel.Children.Add(new TextBlock { Text = "分组：", FontSize = 13, Margin = new Thickness(0, 0, 0, 4) });
        var groupBox = new TextBox { Margin = new Thickness(0, 0, 0, 16) };
        panel.Children.Add(groupBox);

        var btnPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        var cancelBtn = new Button { Content = "取消", Width = 80, Margin = new Thickness(8, 0, 0, 0) };
        var saveBtn = new Button { Content = "保存", Width = 80, Margin = new Thickness(8, 0, 0, 0) };
        btnPanel.Children.Add(cancelBtn);
        btnPanel.Children.Add(saveBtn);
        panel.Children.Add(btnPanel);

        dialog.Content = panel;

        cancelBtn.Click += (s, args) => { dialog.DialogResult = false; dialog.Close(); };
        saveBtn.Click += (s, args) =>
        {
            if (string.IsNullOrWhiteSpace(nameBox.Text))
            {
                MessageBox.Show("请输入模板名称。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                nameBox.Focus();
                return;
            }

            if (_templateService.HasDuplicateName(nameBox.Text.Trim()))
            {
                var overwrite = MessageBox.Show(
                    $"模板 \"{nameBox.Text.Trim()}\" 已存在，是否覆盖？",
                    "名称重复",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                if (overwrite != MessageBoxResult.Yes)
                    return;
            }

            var template = new SiteTemplate
            {
                Name = nameBox.Text.Trim(),
                Description = descBox.Text?.Trim() ?? string.Empty,
                HostName = hostNameBox.Text?.Trim() ?? string.Empty,
                Port = 22,
                User = userBox.Text?.Trim() ?? string.Empty,
                GroupName = string.IsNullOrWhiteSpace(groupBox.Text) ? null : groupBox.Text.Trim()
            };

            if (_templateService.SaveTemplate(template))
            {
                dialog.DialogResult = true;
                dialog.Close();
            }
            else
            {
                MessageBox.Show("模板保存失败。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        };

        if (dialog.ShowDialog() == true)
        {
            LoadTemplates();
        }
    }

    private void BtnDeleteTemplate_Click(object sender, RoutedEventArgs e)
    {
        if (TemplateListView.SelectedItem is not SiteTemplate selectedTemplate)
            return;

        var result = MessageBox.Show(
            $"确定要删除模板 \"{selectedTemplate.Name}\" 吗？\n\n此操作不可恢复。\n\n是否继续？",
            "确认删除",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
            return;

        var confirmResult = MessageBox.Show(
            $"二次确认：即将删除模板 \"{selectedTemplate.Name}\"，是否确认？",
            "二次确认删除",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirmResult != MessageBoxResult.Yes)
            return;

        if (_templateService.DeleteTemplate(selectedTemplate.Name))
        {
            LoadTemplates();
        }
        else
        {
            MessageBox.Show("模板删除失败。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnApplyTemplate_Click(object sender, RoutedEventArgs e)
    {
        if (TemplateListView.SelectedItem is not SiteTemplate selectedTemplate)
            return;

        var site = selectedTemplate.ToSite(selectedTemplate.Name);
        var dialog = new SiteEditDialog(site)
        {
            Owner = Window.GetWindow(this),
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        dialog.Title = "应用模板创建站点";

        if (dialog.ShowDialog() == true)
        {
            MessageBox.Show("站点已创建，请在站点管理页面保存配置。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        LoadData();
    }

    private void TemplateListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var hasSelection = TemplateListView.SelectedItem != null;
        BtnDeleteTemplate.IsEnabled = hasSelection;
        BtnApplyTemplate.IsEnabled = hasSelection;

        if (TemplateListView.SelectedItem is SiteTemplate selected)
        {
            UpdateDetailPanel(selected);
        }
        else
        {
            UpdateDetailPanel(null);
        }
    }

    private void UpdateDetailPanel(SiteTemplate? template)
    {
        if (template == null)
        {
            DetailPanel.Visibility = Visibility.Collapsed;
            DetailEmptyHint.Visibility = Visibility.Visible;
            return;
        }

        DetailPanel.Visibility = Visibility.Visible;
        DetailEmptyHint.Visibility = Visibility.Collapsed;

        DetailName.Text = template.Name;
        DetailHostName.Text = template.HostName;
        DetailPort.Text = template.Port.ToString();
        DetailUser.Text = template.User;
        DetailGroup.Text = template.GroupName ?? "-";
        DetailCreatedAt.Text = template.CreatedAt.ToString("yyyy-MM-dd HH:mm");
        DetailDescription.Text = template.Description ?? "-";
    }

    private (string? name, string? desc) ShowTemplateNameDialog()
    {
        var dialog = new Window
        {
            Title = "保存为模板",
            Width = 350,
            Height = 200,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = Window.GetWindow(this),
            ResizeMode = ResizeMode.NoResize,
            Background = SystemColors.WindowBrush
        };

        var panel = new StackPanel { Margin = new Thickness(16) };
        panel.Children.Add(new TextBlock { Text = "模板名称：", FontSize = 13, Margin = new Thickness(0, 0, 0, 4) });
        var nameBox = new TextBox { Margin = new Thickness(0, 0, 0, 12) };
        panel.Children.Add(nameBox);

        panel.Children.Add(new TextBlock { Text = "描述：", FontSize = 13, Margin = new Thickness(0, 0, 0, 4) });
        var descBox = new TextBox { Margin = new Thickness(0, 0, 0, 16) };
        panel.Children.Add(descBox);

        var btnPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        var cancelBtn = new Button { Content = "取消", Width = 80, Margin = new Thickness(8, 0, 0, 0) };
        var saveBtn = new Button { Content = "保存", Width = 80, Margin = new Thickness(8, 0, 0, 0) };
        btnPanel.Children.Add(cancelBtn);
        btnPanel.Children.Add(saveBtn);
        panel.Children.Add(btnPanel);

        dialog.Content = panel;

        string? templateName = null;
        string? templateDesc = null;

        cancelBtn.Click += (s, args) => { dialog.DialogResult = false; dialog.Close(); };
        saveBtn.Click += (s, args) =>
        {
            if (string.IsNullOrWhiteSpace(nameBox.Text))
            {
                MessageBox.Show("请输入模板名称。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                nameBox.Focus();
                return;
            }
            templateName = nameBox.Text.Trim();
            templateDesc = descBox.Text?.Trim() ?? string.Empty;
            dialog.DialogResult = true;
            dialog.Close();
        };

        return dialog.ShowDialog() == true ? (templateName, templateDesc) : (null, null);
    }
}
