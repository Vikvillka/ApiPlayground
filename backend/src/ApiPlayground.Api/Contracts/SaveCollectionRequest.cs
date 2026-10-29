namespace ApiPlayground.Api;

public sealed record SaveCollectionRequest(
    Guid? CollectionId,
    string? NewCollectionName,
    string Title,
    string Method,
    string Url,
    Dictionary<string, string>? Headers = null,
    string? Body = null);
