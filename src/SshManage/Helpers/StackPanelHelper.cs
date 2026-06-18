using System.Windows;
using System.Windows.Controls;

namespace SshManage.Helpers;

public static class StackPanelHelper
{
    public static readonly DependencyProperty SpacingProperty =
        DependencyProperty.RegisterAttached(
            "Spacing",
            typeof(double),
            typeof(StackPanelHelper),
            new PropertyMetadata(0.0, OnSpacingChanged));

    public static double GetSpacing(DependencyObject obj)
    {
        return (double)obj.GetValue(SpacingProperty);
    }

    public static void SetSpacing(DependencyObject obj, double value)
    {
        obj.SetValue(SpacingProperty, value);
    }

    private static void OnSpacingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Panel panel)
            return;

        panel.Loaded += Panel_Loaded;
        if (panel.IsLoaded)
        {
            UpdateSpacing(panel, (double)e.NewValue);
        }
    }

    private static void Panel_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is Panel panel)
        {
            var spacing = GetSpacing(panel);
            UpdateSpacing(panel, spacing);
        }
    }

    private static void UpdateSpacing(Panel panel, double spacing)
    {
        var isHorizontal = panel is StackPanel sp && sp.Orientation == Orientation.Horizontal;

        for (int i = 0; i < panel.Children.Count; i++)
        {
            if (panel.Children[i] is FrameworkElement element)
            {
                if (i == 0)
                {
                    element.Margin = new Thickness(0);
                }
                else
                {
                    if (isHorizontal)
                    {
                        element.Margin = new Thickness(spacing, 0, 0, 0);
                    }
                    else
                    {
                        element.Margin = new Thickness(0, spacing, 0, 0);
                    }
                }
            }
        }
    }
}
