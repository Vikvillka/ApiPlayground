using ApiPlayground.Application.Outbound;

namespace ApiPlayground.Api;

public static class RequestCollectionMappers
{
    public static TracerSavedRequestResponse ToResponse(this SavedRequestSnapshot item) => new(
        item.Id,
        item.CreatedAt,
        item.CollectionId,
        item.CollectionName,
        item.Title,
        item.Method,
        item.Url,
        item.Headers.Select(header => new TracerHeaderResponse(
            header.Key,
            item.RedactedHeaders.Contains(header.Key, StringComparer.OrdinalIgnoreCase) ? string.Empty : header.Value,
            item.RedactedHeaders.Contains(header.Key, StringComparer.OrdinalIgnoreCase))).ToArray(),
        item.RedactedHeaders,
        item.Body);
}
