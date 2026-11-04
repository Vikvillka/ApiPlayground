using System.Text.Json;
using ApiPlayground.Application.Auth;
using ApiPlayground.Application.Outbound;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using HttpJsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

namespace ApiPlayground.Api;

[Route("api/tracer/collections")]
[TracerRequestGuardMetadata]
public sealed class TracerCollectionsController(
    SaveRequestUseCase useCase,
    ICurrentUser currentUser,
    IOptions<HttpJsonOptions> jsonOptions,
    ILogger<TracerCollectionsController> logger) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } ownerId)
        {
            return UnauthorizedProblem("Authentication is required.");
        }

        IReadOnlyList<RequestCollectionSnapshot> collections;
        try
        {
            collections = await useCase.ListAsync(ownerId, cancellationToken);
        }
        catch (Exception exception)
        {
            ApiLog.CollectionOperationFailed(logger, "Collection list", exception.GetType().Name, HttpContext.TraceIdentifier);
            return ServiceUnavailableProblem("Collections could not be loaded. Try again.");
        }

        return new JsonResult(collections.Select(collection => new TracerCollectionResponse(
            collection.Id,
            collection.Name,
            collection.Requests.Select(request => request.ToResponse()).ToArray())).ToArray());
    }

    [HttpPost("requests")]
    public async Task<IActionResult> Save(CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } ownerId)
        {
            return UnauthorizedProblem("Authentication is required.");
        }

        SaveCollectionRequest? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<SaveCollectionRequest>(
                Request.Body, jsonOptions.Value.SerializerOptions, cancellationToken);
        }
        catch (JsonException)
        {
            // Match Minimal API body binding: malformed JSON receives an empty 400 response.
            return BadRequest();
        }

        if (request is null)
        {
            return BadRequest();
        }

        try
        {
            var saved = await useCase.SaveAsync(ownerId, new SaveRequestCommand(
                request.CollectionId,
                request.NewCollectionName,
                request.Title,
                request.Method,
                request.Url,
                request.Headers ?? new Dictionary<string, string>(),
                request.Body), cancellationToken);
            return new JsonResult(saved.ToResponse()) { StatusCode = StatusCodes.Status201Created };
        }
        catch (ArgumentException exception)
        {
            return BadRequestProblem(exception.Message);
        }
        catch (Exception exception)
        {
            ApiLog.CollectionOperationFailed(logger, "Collection save", exception.GetType().Name, HttpContext.TraceIdentifier);
            return ServiceUnavailableProblem("The request could not be saved. Your request is still available to retry.");
        }
    }
}