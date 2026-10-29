namespace ApiPlayground.Api;

public sealed record TracerHistoryResponse(
    Guid Id,
    DateTimeOffset CreatedAt,
    string Method,
    string Url,
    IReadOnlyList<string> RedactedQueryParameters,
    IReadOnlyList<TracerHeaderResponse> Headers,
    IReadOnlyList<string> RedactedHeaders,
    string? Body,
    string Failure,
    int? StatusCode,
    double ElapsedMilliseconds);
