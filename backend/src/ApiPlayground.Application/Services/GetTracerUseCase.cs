namespace ApiPlayground.Application.Outbound;

public sealed record GetTracerResult(OutboundResult? Outbound, string? Error, bool HistorySaved = true)
{
    public bool Succeeded => Outbound?.Succeeded == true && Error is null;
}

public sealed class GetTracerUseCase(
    IOutboundRequestExecutor executor,
    IRequestHistoryRepository history)
{
    public async Task<GetTracerResult> ExecuteAsync(Guid ownerId, OutboundRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Uri is null || !request.Uri.IsAbsoluteUri || request.Uri.Scheme is not ("http" or "https"))
        {
            return new GetTracerResult(null, "Enter a valid absolute HTTP or HTTPS URL.");
        }

        var outbound = await executor.ExecuteAsync(request, cancellationToken);
        var historySaved = await TrySaveHistoryAsync(ownerId, request, outbound);

        if (outbound.Failure == OutboundFailure.PolicyRejected)
        {
            return new GetTracerResult(outbound, "Blocked by security policy. Request not sent.", historySaved);
        }

        if (!outbound.Succeeded)
        {
            return new GetTracerResult(outbound, outbound.Diagnostic ?? "The request could not be completed.", historySaved);
        }

        var error = historySaved ? null : "The request completed, but its History entry was not saved.";
        return new GetTracerResult(outbound, error, historySaved);
    }

    private async Task<bool> TrySaveHistoryAsync(Guid ownerId, OutboundRequest request, OutboundResult outbound)
    {
        if (!outbound.DispatchStarted)
        {
            return true;
        }

        try
        {
            await history.SaveAsync(ownerId, RequestHistoryRedaction.Create(request, outbound), CancellationToken.None);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
