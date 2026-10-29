namespace ApiPlayground.Application.Outbound;

public sealed record RequestHistorySnapshot(
    Guid Id,
    DateTimeOffset CreatedAt,
    string Method,
    string Url,
    IReadOnlyList<string> RedactedQueryParameters,
    IReadOnlyDictionary<string, string> Headers,
    IReadOnlyList<string> RedactedHeaders,
    string? Body,
    string Failure,
    int? StatusCode,
    double ElapsedMilliseconds);
