using System.Drawing;
using StoryTranslator.Core;
using System.Windows;

namespace StoryTranslator.App;

public partial class MainWindow : Window
{
    private readonly TranslationOverlay _overlay;
    private readonly SettingsStore _settingsStore;
    private readonly TranslationCoordinator _coordinator;
    private Rectangle? _selectedRegion;

    public MainWindow() : this(new TranslationOverlay(), new SettingsStore(), new TranslationCoordinator(new UnavailableTranslator())) { }

    internal MainWindow(TranslationOverlay overlay, SettingsStore settingsStore, TranslationCoordinator coordinator)
    {
        InitializeComponent();
        _overlay = overlay;
        _settingsStore = settingsStore;
        _coordinator = coordinator;
        var settings = _settingsStore.Load();
        _selectedRegion = settings.CaptureRegion;
        OverlayButton.Visibility = Visibility.Visible;
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
            var settings = _settingsStore.Load();
            _settingsStore.Save(settings with { CaptureRegion = region });
        }
    }

    private void TurnOff()
    {
        StoryModeCheckBox.IsChecked = false;
        _coordinator.CancelPending();
        _overlay.HideOverlay();
        StatusText.Text = "ĐANG TẮT — Story Mode chỉ được bật sau khi OCR runtime và nguồn dịch được cấu hình, xác nhận sẵn sàng.";
    }

    private void ShowOverlay_Click(object sender, RoutedEventArgs e) => _overlay.Show();

    private void Exit_Click(object sender, RoutedEventArgs e) => Close();

    private sealed class UnavailableTranslator : ITextTranslator
    {
        public Task<string> TranslateAsync(string source, string target, CancellationToken cancellationToken) =>
            Task.FromException<string>(new InvalidOperationException("Chưa cấu hình bộ dịch."));
    }
}



