using ApiPlayground.Application.Outbound;
using Microsoft.AspNetCore.Mvc;

namespace ApiPlayground.Api;

[Route("api/tracer")]
[TracerRequestGuardMetadata]
public sealed class TracerHistoryController(RequestHistoryUseCase useCase) : ControllerBase
{
    [HttpGet("history")]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var items = await useCase.ListAsync(cancellationToken);
        return new JsonResult(items.Select(ToResponse).ToArray());
    }

    [HttpGet("history/{id:guid}")]
    public async Task<IActionResult> Find(Guid id, CancellationToken cancellationToken)
    {
        var item = await useCase.FindAsync(id, cancellationToken);
        return item is null ? NotFound() : new JsonResult(ToResponse(item));
    }

    private static TracerHistoryResponse ToResponse(RequestHistorySnapshot item) => new(
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