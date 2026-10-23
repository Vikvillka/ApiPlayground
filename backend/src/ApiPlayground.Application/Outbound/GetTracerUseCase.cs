namespace ApiPlayground.Application.Outbound;

public sealed record GetTracerResult(OutboundResult? Outbound, bool PersistenceVerified, string? Error, bool HistorySaved = true)
{
    public bool Succeeded => Outbound?.Succeeded == true && PersistenceVerified && Error is null;
}

public sealed class GetTracerUseCase(
    IOutboundRequestExecutor executor,
    ITracerPersistenceSmokeCheck persistence,
    IRequestHistoryStore history)
{
    public async Task<GetTracerResult> ExecuteAsync(OutboundRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Uri is null || !request.Uri.IsAbsoluteUri || request.Uri.Scheme is not ("http" or "https"))
            return new GetTracerResult(null, false, "Enter a valid absolute HTTP or HTTPS URL.");

        var outbound = await executor.ExecuteAsync(request, cancellationToken);
        var historySaved = true;
        if (outbound.DispatchStarted)
        {
            try
            {
                await history.SaveAsync(RequestHistoryRedaction.Create(request, outbound), CancellationToken.None);
            }
            catch (Exception)
            {
                historySaved = false;
            }
        }
        if (outbound.Failure == OutboundFailure.PolicyRejected)
            return new GetTracerResult(outbound, false, "Blocked by security policy. Request not sent.", historySaved);
        if (!outbound.Succeeded)
            return new GetTracerResult(outbound, false, outbound.Diagnostic ?? "The request could not be completed.", historySaved);

        bool persistenceVerified;
        try
        {
            persistenceVerified = await persistence.VerifyRoundTripAsync(cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            persistenceVerified = false;
        }
        if (!persistenceVerified)
            return new GetTracerResult(outbound, false, historySaved
                ? "The request completed, but the local persistence check failed."
                : "The request completed, but its History entry was not saved; the local persistence check also failed.", historySaved);

        return new GetTracerResult(outbound, true, historySaved ? null : "The request completed, but its History entry was not saved.", historySaved);
    }
}
