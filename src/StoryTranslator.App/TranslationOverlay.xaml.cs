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
        _ = TryShowTranslation(translation);
    }

    /// <summary>Returns true when shown immediately or queued; a queued update may be discarded by its guard.</summary>
    public bool TryShowTranslation(string translation, Func<bool>? isStillAllowed = null)
    {
        ArgumentNullException.ThrowIfNull(translation);
        if (!Dispatcher.CheckAccess())
        {
            _ = Dispatcher.BeginInvoke(() => TryShowTranslation(translation, isStillAllowed));
            return true;
        }

        if (isStillAllowed is not null && !isStillAllowed()) return false;
        TranslationText.Text = translation;
        if (!IsVisible) Show();
        Activate();
        return true;
    }

    public void HideOverlay() => Hide();

    void ITranslationOverlayVisibility.Show() => Show();
    bool ITranslationOverlayVisibility.TryShowTranslation(string translation, Func<bool> isStillAllowed) => TryShowTranslation(translation, isStillAllowed);
    void ITranslationOverlayVisibility.Hide() => Hide();

    private void Hide_Click(object sender, RoutedEventArgs e) => HideOverlay();
    private void Close_Click(object sender, RoutedEventArgs e) => HideOverlay();
}
