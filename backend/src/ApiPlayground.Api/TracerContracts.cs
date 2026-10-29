namespace ApiPlayground.Api;

public sealed record TracerHeaderResponse(string Name, string Value, bool Redacted);

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

public sealed record TracerCollectionResponse(
    Guid Id,
    string Name,
    IReadOnlyList<TracerSavedRequestResponse> Requests);

public sealed record TracerSavedRequestResponse(
    Guid Id,
    DateTimeOffset CreatedAt,
    Guid CollectionId,
    string CollectionName,
    string Title,
    string Method,
    string Url,
    IReadOnlyList<TracerHeaderResponse> Headers,
    IReadOnlyList<string> RedactedHeaders,
    string? Body);

public sealed record SaveCollectionRequest(
    Guid? CollectionId,
    string? NewCollectionName,
    string Title,
    string Method,
    string Url,
    Dictionary<string, string>? Headers = null,
    string? Body = null);