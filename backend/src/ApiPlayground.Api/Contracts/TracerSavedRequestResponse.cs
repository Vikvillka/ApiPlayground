namespace ApiPlayground.Api;

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
