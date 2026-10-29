using System.Text;
using System.Text.Json;
using ApiPlayground.Application.Outbound;
using ApiPlayground.Infrastructure.Outbound;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using HttpJsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

namespace ApiPlayground.Api;

[Route("api/tracer")]
[TracerRequestGuardMetadata]
public sealed class TracerRequestController(GetTracerUseCase useCase, IOptions<HttpJsonOptions> jsonOptions) : ControllerBase
{
    [HttpPost("get")]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        TracerRequest? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<TracerRequest>(Request.Body, jsonOptions.Value.SerializerOptions, cancellationToken);
        }
        catch (JsonException)
        {
            // Minimal API body binding returned an empty 400 for malformed JSON.
            return BadRequest();
        }

        if (request is null)
            return BadRequest();
        if (string.IsNullOrWhiteSpace(request.Url) || request.Url.Length > 2048)
            return BadRequest(new { error = "Enter a URL to send a request." });
        if (!RequestValidation.IsSupportedMethod(request.Method))
            return BadRequest(new { error = "Choose a supported HTTP method." });
        if (!RequestValidation.TryCreateHttpUri(request.Url, out var uri))
            return BadRequest(new { error = "Enter a valid absolute HTTP or HTTPS URL without credentials or fragments." });
        if (request.Method is "GET" or "DELETE" && !string.IsNullOrWhiteSpace(request.Body))
            return BadRequest(new { error = "JSON bodies are only supported for POST, PUT, and PATCH." });
        if (request.Body is { Length: > 0 } && !IsValidJson(request.Body))
            return BadRequest(new { error = "Enter a valid JSON body." });
        if (request.Headers is not null && RequestValidation.HasDuplicateHeaderNames(request.Headers.Keys))
            return BadRequest(new { error = "Enabled request header names must be unique." });
        if (request.Headers is not null && request.Headers.Any(header => string.IsNullOrWhiteSpace(header.Key) ||
            header.Key.Equals("Host", StringComparison.OrdinalIgnoreCase) ||
            header.Key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase) ||
            header.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase)))
            return BadRequest(new { error = "Host, Content-Length, and Content-Type headers are managed by the request sender." });

        var headers = request.Headers ?? new Dictionary<string, string>();
        var contentType = string.IsNullOrWhiteSpace(request.Body) ? null : "application/json";
        var outboundRequest = new OutboundRequest(uri, request.Method, headers,
            string.IsNullOrWhiteSpace(request.Body) ? ReadOnlyMemory<byte>.Empty : Encoding.UTF8.GetBytes(request.Body), contentType);
        var result = await useCase.ExecuteAsync(outboundRequest, cancellationToken);
        if (result.Outbound is null)
            return BadRequest(new { error = result.Error });
        if (!result.Outbound.Succeeded && result.Outbound.Failure != OutboundFailure.PolicyRejected)
            return new JsonResult(new
            {
                failure = result.Outbound.Failure.ToString(), statusCode = result.Outbound.StatusCode,
                elapsedMilliseconds = result.Outbound.Elapsed.TotalMilliseconds, headers = result.Outbound.Headers,
                body = Encoding.UTF8.GetString(result.Outbound.Body.Span), persistenceVerified = result.PersistenceVerified,
                error = result.Error, requestSent = result.Outbound.DispatchStarted, historySaved = result.HistorySaved,
            }) { StatusCode = result.Outbound.Failure switch
            {
                OutboundFailure.Timeout => StatusCodes.Status504GatewayTimeout,
                OutboundFailure.Cancelled => 499,
                _ => StatusCodes.Status502BadGateway,
            } };

        var httpStatus = result.Outbound.Failure switch
        {
            OutboundFailure.None => StatusCodes.Status200OK,
            OutboundFailure.PolicyRejected => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status200OK,
        };
        return new JsonResult(new
        {
            failure = result.Outbound.Failure.ToString(),
            statusCode = result.Outbound.StatusCode,
            elapsedMilliseconds = result.Outbound.Elapsed.TotalMilliseconds,
            headers = result.Outbound.Headers,
            body = Encoding.UTF8.GetString(result.Outbound.Body.Span),
            persistenceVerified = result.PersistenceVerified,
            error = result.Error,
            requestSent = result.Outbound.DispatchStarted,
            historySaved = result.HistorySaved,
        }) { StatusCode = httpStatus };
    }

    private static bool IsValidJson(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}

public sealed record TracerRequest(string Url, string Method = "GET", string? Body = null, Dictionary<string, string>? Headers = null);
