namespace ApiPlayground.Application.Outbound;

public sealed record OutboundRequest(
    Uri Uri,
    string Method,
    IReadOnlyDictionary<string, string>? Headers = null,
    ReadOnlyMemory<byte> Body = default,
    string? ContentType = null);
