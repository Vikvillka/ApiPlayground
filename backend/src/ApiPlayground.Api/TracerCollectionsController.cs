using System.Text.Json;
using ApiPlayground.Application.Outbound;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using HttpJsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

namespace ApiPlayground.Api;

[Route("api/tracer/collections")]
[TracerRequestGuardMetadata]
public sealed class TracerCollectionsController(
    SaveRequestUseCase useCase,
    IOptions<HttpJsonOptions> jsonOptions) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        IReadOnlyList<RequestCollectionSnapshot> collections;
        try
        {
            collections = await useCase.ListAsync(cancellationToken);
        }
        catch (Exception)
        {
            return Problem("Collections could not be loaded. Try again.", statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        return new JsonResult(collections.Select(collection => new TracerCollectionResponse(
            collection.Id,
            collection.Name,
            collection.Requests.Select(ToResponse).ToArray())).ToArray());
    }

    [HttpPost("requests")]
    public async Task<IActionResult> Save(CancellationToken cancellationToken)
    {
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
            return BadRequest();

        try
        {
            var saved = await useCase.SaveAsync(new SaveRequestCommand(
                request.CollectionId,
                request.NewCollectionName,
                request.Title,
                request.Method,
                request.Url,
                request.Headers ?? new Dictionary<string, string>(),
                request.Body), cancellationToken);
            return new JsonResult(ToResponse(saved)) { StatusCode = StatusCodes.Status201Created };
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
        catch (Exception)
        {
            return Problem("The request could not be saved. Your request is still available to retry.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    private static TracerSavedRequestResponse ToResponse(SavedRequestSnapshot item) => new(
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