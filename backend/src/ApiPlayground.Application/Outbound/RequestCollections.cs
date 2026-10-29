namespace ApiPlayground.Application.Outbound;

public sealed record SavedRequestSnapshot(
    Guid Id,
    DateTimeOffset CreatedAt,
    Guid CollectionId,
    string CollectionName,
    string Title,
    string Method,
    string Url,
    IReadOnlyDictionary<string, string> Headers,
    IReadOnlyList<string> RedactedHeaders,
    string? Body);

public sealed record RequestCollectionSnapshot(
    Guid Id,
    string Name,
    IReadOnlyList<SavedRequestSnapshot> Requests);

public sealed record SaveRequestCommand(
    Guid? CollectionId,
    string? NewCollectionName,
    string Title,
    string Method,
    string Url,
    IReadOnlyDictionary<string, string> Headers,
    string? Body);
