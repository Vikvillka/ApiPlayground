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
        return new JsonResult(items.Select(item => item.ToResponse()).ToArray());
    }

    [HttpGet("history/{id:guid}")]
    public async Task<IActionResult> Find(Guid id, CancellationToken cancellationToken)
    {
        var item = await useCase.FindAsync(id, cancellationToken);
        return item is null ? NotFound() : new JsonResult(item.ToResponse());
    }
}