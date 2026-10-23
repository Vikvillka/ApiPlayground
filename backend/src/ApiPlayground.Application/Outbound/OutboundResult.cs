namespace ApiPlayground.Application.Outbound;

public enum OutboundFailure
{
    None,
    PolicyRejected,
    Timeout,
    TransportFailure,
    ResponseTooLarge,
    Cancelled
}

public sealed record OutboundResult(
    OutboundFailure Failure,
    int? StatusCode,
    TimeSpan Elapsed,
    IReadOnlyDictionary<string, IReadOnlyList<string>> Headers,
    ReadOnlyMemory<byte> Body,
    string? Diagnostic = null,
    bool DispatchStarted = false)
{
    public bool Succeeded => Failure == OutboundFailure.None;
}
