using ApiPlayground.Application.Outbound;

namespace ApiPlayground.Api;

public static class RequestHistoryMappers
{
    public static TracerHistoryResponse ToResponse(this RequestHistorySnapshot item) => new(
        item.Id,
        item.CreatedAt,
        item.Method,
        item.Url,
        item.RedactedQueryParameters,
        item.Headers.Select(header => new TracerHeaderResponse(
            header.Key,
            item.RedactedHeaders.Contains(header.Key, StringComparer.OrdinalIgnoreCase) ? string.Empty : header.Value,
            item.RedactedHeaders.Contains(header.Key, StringComparer.OrdinalIgnoreCase))).ToArray(),
        item.RedactedHeaders,
        item.Body,
        item.Failure,
        item.StatusCode,
        item.ElapsedMilliseconds);
}
