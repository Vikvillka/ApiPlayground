using ApiPlayground.Application.Auth;
using ApiPlayground.Application.Outbound;
using Microsoft.AspNetCore.Mvc;

namespace ApiPlayground.Api;

[Route("api/tracer")]
[TracerRequestGuardMetadata]
public sealed class TracerHistoryController(RequestHistoryUseCase useCase, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet("history")]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } ownerId)
        {
            return Unauthorized();
        }

        var items = await useCase.ListAsync(ownerId, cancellationToken);
        return new JsonResult(items.Select(item => item.ToResponse()).ToArray());
    }

    [HttpGet("history/{id:guid}")]
    public async Task<IActionResult> Find(Guid id, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } ownerId)
        {
            return Unauthorized();
        }

        var item = await useCase.FindAsync(ownerId, id, cancellationToken);
        return item is null ? NotFound() : new JsonResult(item.ToResponse());
    }
}