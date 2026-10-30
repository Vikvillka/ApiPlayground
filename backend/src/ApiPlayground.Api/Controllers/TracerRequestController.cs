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
public sealed class TracerRequestController(GetTracerUseCase useCase, IOptions<HttpJsonOptions> jsonOptions) : ApiControllerBase
{
    [HttpPost("get")]
    public Task<IActionResult> Get(CancellationToken cancellationToken) => ExecuteAsync(cancellationToken);

    // Exports the response the client already holds. The target request is not sent again.
    [HttpPost("export")]
    public async Task<IActionResult> Export(CancellationToken cancellationToken)
    {
        ExportBody? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<ExportBody>(Request.Body, jsonOptions.Value.SerializerOptions, cancellationToken);
        }
        catch (JsonException)
        {
            return BadRequest();
        }

        if (body?.Request is null || body.Response is null)
        {
            return BadRequest();
        }

        var validationError = Validate(body.Request, out _);
        if (validationError is not null)
        {
            return BadRequestProblem(validationError);
        }

        if (string.IsNullOrEmpty(body.Response.Failure) ||
            !Enum.TryParse<OutboundFailure>(body.Response.Failure, out var failure) ||
            !Enum.IsDefined(failure))
        {
            return BadRequestProblem("The export response has an unknown failure value.");
        }

        return ToExportResponse(body.Request, body.Response, failure);
    }

    private async Task<IActionResult> ExecuteAsync(CancellationToken cancellationToken)
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
        {
            return BadRequest();
        }

        var validationError = Validate(request, out var uri);
        if (validationError is not null)
        {
            return BadRequestProblem(validationError);
        }

        var outboundRequest = ToOutboundRequest(request, uri!);
        var result = await useCase.ExecuteAsync(outboundRequest, cancellationToken);
        if (result.Outbound is null)
        {
            return BadRequestProblem(result.Error ?? "The request could not be completed.");
        }

        var outbound = result.Outbound;
        if (!outbound.Succeeded && outbound.Failure != OutboundFailure.PolicyRejected)
        {
            return new JsonResult(ToPayload(result)) { StatusCode = FailureStatusCode(outbound.Failure) };
        }

        return new JsonResult(ToPayload(result)) { StatusCode = SuccessStatusCode(outbound.Failure) };
    }

    private IActionResult ToExportResponse(TracerRequest request, ExportedResponse response, OutboundFailure failure)
    {
        Response.Headers.ContentDisposition = "attachment; filename=\"apiplayground-export.json\"";
        // The status matches the send endpoint for the same outcome, so an exported failure is not reported as 200.
        var statusCode = failure switch
        {
            OutboundFailure.None => StatusCodes.Status200OK,
            OutboundFailure.PolicyRejected => StatusCodes.Status403Forbidden,
            _ => FailureStatusCode(failure),
        };
        return new JsonResult(new
        {
            format = "apiplayground.export.v1",
            request = new
            {
                url = request.Url,
                method = request.Method,
                headers = request.Headers ?? new Dictionary<string, string>(),
                body = request.Body,
            },
            response = new
            {
                failure = failure.ToString(),
                statusCode = response.StatusCode,
                elapsedMilliseconds = response.ElapsedMilliseconds,
                headers = response.Headers ?? new Dictionary<string, IReadOnlyList<string>>(),
                body = response.Body ?? string.Empty,
                error = response.Error,
                requestSent = response.RequestSent,
                historySaved = response.HistorySaved,
            },
        }) { StatusCode = statusCode };
    }

    private static string? Validate(TracerRequest request, out Uri? uri)
    {
        uri = null;
        if (string.IsNullOrWhiteSpace(request.Url) || request.Url.Length > 2048)
        {
            return "Enter a URL to send a request.";
        }

        if (!RequestValidation.IsSupportedMethod(request.Method))
        {
            return "Choose a supported HTTP method.";
        }

        if (!RequestValidation.TryCreateHttpUri(request.Url, out var parsed))
        {
            return "Enter a valid absolute HTTP or HTTPS URL without credentials or fragments.";
        }

        uri = parsed;
        if (request.Method is "GET" or "DELETE" && !string.IsNullOrWhiteSpace(request.Body))
        {
            return "JSON bodies are only supported for POST, PUT, and PATCH.";
        }

        if (request.Body is { Length: > 0 } && !IsValidJson(request.Body))
        {
            return "Enter a valid JSON body.";
        }

        if (request.Headers is not null && RequestValidation.HasDuplicateHeaderNames(request.Headers.Keys))
        {
            return "Enabled request header names must be unique.";
        }

        if (request.Headers is not null && request.Headers.Any(IsManagedOrEmptyHeader))
        {
            return "Host, Content-Length, and Content-Type headers are managed by the request sender.";
        }

        return null;
    }

    private static bool IsManagedOrEmptyHeader(KeyValuePair<string, string> header) =>
        string.IsNullOrWhiteSpace(header.Key) ||
        header.Key.Equals("Host", StringComparison.OrdinalIgnoreCase) ||
        header.Key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase) ||
        header.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase);

    private static OutboundRequest ToOutboundRequest(TracerRequest request, Uri uri)
    {
        var hasBody = !string.IsNullOrWhiteSpace(request.Body);
        var body = hasBody ? Encoding.UTF8.GetBytes(request.Body!) : ReadOnlyMemory<byte>.Empty;
        var contentType = hasBody ? "application/json" : null;
        var headers = request.Headers ?? new Dictionary<string, string>();
        return new OutboundRequest(uri, request.Method, headers, body, contentType);
    }

    private static object ToPayload(GetTracerResult result) => new
    {
        failure = result.Outbound!.Failure.ToString(),
        statusCode = result.Outbound.StatusCode,
        elapsedMilliseconds = result.Outbound.Elapsed.TotalMilliseconds,
        headers = result.Outbound.Headers,
        body = Encoding.UTF8.GetString(result.Outbound.Body.Span),
        error = result.Error,
        requestSent = result.Outbound.DispatchStarted,
        historySaved = result.HistorySaved,
    };

    private static int FailureStatusCode(OutboundFailure failure) => failure switch
    {
        OutboundFailure.Timeout => StatusCodes.Status504GatewayTimeout,
        OutboundFailure.Cancelled => 499,
        _ => StatusCodes.Status502BadGateway,
    };

    private static int SuccessStatusCode(OutboundFailure failure) => failure switch
    {
        OutboundFailure.PolicyRejected => StatusCodes.Status403Forbidden,
        _ => StatusCodes.Status200OK,
    };

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

public sealed record ExportBody(TracerRequest? Request, ExportedResponse? Response);

public sealed record ExportedResponse(
    string? Failure,
    int? StatusCode,
    double ElapsedMilliseconds,
    Dictionary<string, IReadOnlyList<string>>? Headers,
    string? Body,
    string? Error,
    bool RequestSent,
    bool? HistorySaved);
