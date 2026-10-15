using System.Drawing;
using System.Windows;

namespace StoryTranslator.App;

public partial class MainWindow : Window
{
    private Rectangle? _selectedRegion;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void SelectRegion_Click(object sender, RoutedEventArgs e)
    {
        var selector = new RegionSelectorWindow { Owner = this };
        if (selector.ShowDialog() == true && selector.SelectedRegion is { } region)
        {
            _selectedRegion = region;
            RegionText.Text = $"{region.Width} × {region.Height} px";
        }
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
