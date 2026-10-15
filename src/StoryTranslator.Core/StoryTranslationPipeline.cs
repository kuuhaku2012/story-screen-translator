namespace StoryTranslator.Core;

/// <summary>Coordinates stabilized OCR lines through glossary protection, translation and OFF-gated presentation.</summary>
public sealed class StoryTranslationPipeline
{
    private readonly SemaphoreSlim _observationLock = new(1, 1);
    private readonly TextStabilityFilter _stability;
    private readonly GlossaryProtector _glossary;
    private readonly TranslationCoordinator _coordinator;
    private readonly TranslationPresentationGate _presentationGate;

    public StoryTranslationPipeline(
        TextStabilityFilter stability,
        GlossaryProtector glossary,
        TranslationCoordinator coordinator,
        TranslationPresentationGate presentationGate)
    {
        _stability = stability ?? throw new ArgumentNullException(nameof(stability));
        _glossary = glossary ?? throw new ArgumentNullException(nameof(glossary));
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _presentationGate = presentationGate ?? throw new ArgumentNullException(nameof(presentationGate));
    }

    public void Enable() => _presentationGate.Activate();

    public void TurnOff() => _presentationGate.TurnOff();

    /// <summary>Returns true only when a newly stable line was translated or found cached and presented.</summary>
    public async Task<bool> ObserveAsync(string? recognizedText, string target = "vi", CancellationToken cancellationToken = default)
    {
        await _observationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        string? stableLine;
        try
        {
            if (!_presentationGate.IsActive) return false;

            stableLine = _stability.Observe(recognizedText);
        }
        finally
        {
            _observationLock.Release();
        }

        if (stableLine is null || !_presentationGate.IsActive) return false;

        var protectedText = _glossary.Protect(stableLine);
        var translated = await _coordinator.SubmitAsync(protectedText.Text, target, cancellationToken).ConfigureAwait(false);
        var restored = GlossaryProtector.Restore(translated, protectedText.TokenMap);
        return _presentationGate.TryShowTranslation(restored);
    }
}
