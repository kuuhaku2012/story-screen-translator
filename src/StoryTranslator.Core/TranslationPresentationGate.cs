namespace StoryTranslator.Core;

/// <summary>Minimal visibility boundary for the independent translation overlay.</summary>
public interface ITranslationOverlayVisibility
{
    void Show();
    void Hide();
}

/// <summary>Hard OFF gate for presentation and pending translation work.</summary>
public sealed class TranslationPresentationGate
{
    private readonly ITranslationOverlayVisibility _overlay;
    private readonly Action _cancelPending;

    public TranslationPresentationGate(ITranslationOverlayVisibility overlay, Action cancelPending)
    {
        _overlay = overlay ?? throw new ArgumentNullException(nameof(overlay));
        _cancelPending = cancelPending ?? throw new ArgumentNullException(nameof(cancelPending));
    }

    public bool IsActive { get; private set; }

    public void Activate() => IsActive = true;

    public bool TryShowOverlay()
    {
        if (!IsActive) return false;
        _overlay.Show();
        return true;
    }

    public void TurnOff()
    {
        IsActive = false;
        _cancelPending();
        _overlay.Hide();
    }
}
