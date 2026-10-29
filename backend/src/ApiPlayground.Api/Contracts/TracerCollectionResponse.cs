namespace ApiPlayground.Api;

public sealed record TracerCollectionResponse(
    Guid Id,
    string Name,
    IReadOnlyList<TracerSavedRequestResponse> Requests);
