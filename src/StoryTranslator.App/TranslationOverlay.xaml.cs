using System.Windows;
using StoryTranslator.Core;

namespace StoryTranslator.App;

/// <summary>Independent, topmost window displaying only the latest translated line.</summary>
public partial class TranslationOverlay : Window, ITranslationOverlayVisibility
{
    public TranslationOverlay()
    {
        InitializeComponent();
        Hide();
    }

    public void ShowTranslation(string translation)
    {
        ArgumentNullException.ThrowIfNull(translation);
        TranslationText.Text = translation;
        if (!IsVisible) Show();
        Activate();
    }

    public void HideOverlay() => Hide();

    void ITranslationOverlayVisibility.Show() => Show();
    void ITranslationOverlayVisibility.Hide() => Hide();

    private void Hide_Click(object sender, RoutedEventArgs e) => HideOverlay();
    private void Close_Click(object sender, RoutedEventArgs e) => HideOverlay();
}
