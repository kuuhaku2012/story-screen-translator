using System.Drawing;
using StoryTranslator.Core;
using System.Windows;

namespace StoryTranslator.App;

public partial class MainWindow : Window
{
    private readonly TranslationOverlay _overlay;
    private readonly AppRuntimeServices _services;
    private readonly TranslationPresentationGate _presentationGate;
    private Rectangle? _selectedRegion;

    public MainWindow() : this(new TranslationOverlay(), AppComposition.Create()) { }

    internal MainWindow(TranslationOverlay overlay, AppRuntimeServices services)
    {
        InitializeComponent();
        _overlay = overlay;
        _services = services;
        _presentationGate = new TranslationPresentationGate(overlay, _services.Coordinator.CancelPending);
        var settings = _services.SettingsStore.Load();
        _selectedRegion = settings.CaptureRegion;
        OverlayButton.IsEnabled = false;
        if (_selectedRegion is { } region) RegionText.Text = $"{region.Width} × {region.Height} px";
        Closing += (_, _) => TurnOff();
    }

    private void SelectRegion_Click(object sender, RoutedEventArgs e)
    {
        var selector = new RegionSelectorWindow { Owner = this };
        if (selector.ShowDialog() == true && selector.SelectedRegion is { } region)
        {
            _selectedRegion = region;
            RegionText.Text = $"{region.Width} × {region.Height} px";
            var settings = _services.SettingsStore.Load();
            _services.SettingsStore.Save(settings with { CaptureRegion = region });
        }
    }

    private void TurnOff()
    {
        StoryModeCheckBox.IsChecked = false;
        _presentationGate.TurnOff();
        OverlayButton.IsEnabled = false;
        StatusText.Text = "ĐANG TẮT — Story Mode chỉ được bật sau khi OCR runtime và nguồn dịch được cấu hình, xác nhận sẵn sàng.";
    }

    private void ShowOverlay_Click(object sender, RoutedEventArgs e)
    {
        if (_presentationGate.TryShowOverlay()) return;
        OverlayButton.IsEnabled = false;
    }

    private void Exit_Click(object sender, RoutedEventArgs e) => Close();
}
