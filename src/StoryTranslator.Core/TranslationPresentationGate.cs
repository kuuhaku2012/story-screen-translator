namespace StoryTranslator.Core;

/// <summary>Minimal visibility boundary for the independent translation overlay.</summary>
public interface ITranslationOverlayVisibility
{
    void Show();
    /// <summary>Returns true when presentation was accepted or queued; evaluates the guard when UI changes are applied.</summary>
    bool TryShowTranslation(string translation, Func<bool> isStillAllowed);
    void Hide();
}

/// <summary>Hard OFF gate for presentation and pending translation work.</summary>
public sealed class TranslationPresentationGate
{
    private readonly object _gate = new();
    private readonly ITranslationOverlayVisibility _overlay;
    private readonly Action _cancelPending;

    public TranslationPresentationGate(ITranslationOverlayVisibility overlay, Action cancelPending)
    {
        _overlay = overlay ?? throw new ArgumentNullException(nameof(overlay));
        _cancelPending = cancelPending ?? throw new ArgumentNullException(nameof(cancelPending));
    }

    public bool IsActive { get { lock (_gate) return _isActive; } }
    private bool _isActive;

    public void Activate()
    {
        lock (_gate) _isActive = true;
    }

    public bool TryShowOverlay()
    {
        lock (_gate)
        {
            if (!_isActive) return false;
            _overlay.Show();
            return true;
        }
    }

    /// <summary>Accepts or queues translated text while active; queued UI work must recheck both guards when applied.</summary>
    public bool TryShowTranslation(string translation) => TryShowTranslation(translation, () => true);

    public bool TryShowTranslation(string translation, Func<bool> isCurrent)
    {
        ArgumentNullException.ThrowIfNull(translation);
        ArgumentNullException.ThrowIfNull(isCurrent);
        lock (_gate)
        {
            return _isActive && isCurrent() && _overlay.TryShowTranslation(translation, () => IsActive && isCurrent());
        }
    }

    public void TurnOff()
    {
        lock (_gate)
        {
            _isActive = false;
            _cancelPending();
            _overlay.Hide();
        }
    }
}
