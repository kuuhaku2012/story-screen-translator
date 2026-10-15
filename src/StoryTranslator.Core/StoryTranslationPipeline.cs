namespace StoryTranslator.Core;

/// <summary>Coordinates stabilized OCR lines through glossary protection, translation and OFF-gated presentation.</summary>
public sealed class StoryTranslationPipeline
{
    private readonly SemaphoreSlim _observationLock = new(1, 1);
    private readonly object _transitionGate = new();
    private readonly object _generationGate = new();
    private readonly TextStabilityFilter _stability;
    private readonly GlossaryProtector _glossary;
    private readonly ITranslationCoordinator _coordinator;
    private readonly TranslationPresentationGate _presentationGate;
    private long _sessionGeneration;
    private long _lineGeneration;

    public StoryTranslationPipeline(
        TextStabilityFilter stability,
        GlossaryProtector glossary,
        ITranslationCoordinator coordinator,
        TranslationPresentationGate presentationGate)
    {
        _stability = stability ?? throw new ArgumentNullException(nameof(stability));
        _glossary = glossary ?? throw new ArgumentNullException(nameof(glossary));
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _presentationGate = presentationGate ?? throw new ArgumentNullException(nameof(presentationGate));
    }

    public void Enable()
    {
        lock (_transitionGate)
        {
            lock (_generationGate)
            {
                _sessionGeneration++;
                _lineGeneration++;
            }
            _stability.Reset();
            _presentationGate.Activate();
        }
    }

    public void TurnOff()
    {
        lock (_transitionGate)
        {
            lock (_generationGate)
            {
                _sessionGeneration++;
                _lineGeneration++;
            }
            _stability.Reset();
            _presentationGate.TurnOff();
        }
    }

    /// <summary>Returns true when a newly stable line was translated or cached and its presentation was accepted or queued.</summary>
    public async Task<bool> ObserveAsync(string? recognizedText, string target = "vi", CancellationToken cancellationToken = default)
    {
        await _observationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        string? stableLine;
        TranslationTicket ticket;
        try
        {
            lock (_transitionGate)
            {
                if (!_presentationGate.IsActive) return false;

                var observation = _stability.ObserveDetailed(recognizedText);
                stableLine = observation.StableLine;
                if (observation.CandidateChanged)
                {
                    lock (_generationGate) _lineGeneration++;
                }
                if (stableLine is null)
                {
                    ticket = default;
                }
                else
                {
                    lock (_generationGate)
                    {
                        _lineGeneration++;
                        ticket = new TranslationTicket(_sessionGeneration, _lineGeneration);
                    }
                }
            }
        }
        finally
        {
            _observationLock.Release();
        }

        if (stableLine is null) return false;

        var protectedText = _glossary.Protect(stableLine);
        Task<string> translationTask;
        lock (_transitionGate)
        {
            if (!IsCurrent(ticket) || !_presentationGate.IsActive) return false;
            translationTask = _coordinator.SubmitAsync(protectedText.Text, target, cancellationToken);
        }

        var translated = await translationTask.ConfigureAwait(false);
        var restored = GlossaryProtector.Restore(translated, protectedText.TokenMap);
        return _presentationGate.TryShowTranslation(restored, () => IsCurrent(ticket));
    }

    private bool IsCurrent(TranslationTicket ticket)
    {
        lock (_generationGate)
        {
            return ticket.Session == _sessionGeneration && ticket.Line == _lineGeneration;
        }
    }

    private readonly record struct TranslationTicket(long Session, long Line);
}
