using System.Text;
using System.Text.Json;
using ApiPlayground.Application.Outbound;
using ApiPlayground.Infrastructure.Outbound;

namespace ApiPlayground.Api;

public static class TracerEndpoints
{
    public static void UseRequestGuard(this WebApplication app)
    {
        var allowedHosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "localhost:5000" };
        const string allowedOrigin = "http://127.0.0.1:5173";
        app.Use(async (context, next) =>
        {
            var isTracerRequest = context.GetEndpoint()?.Metadata.GetMetadata<TracerRequestGuardMetadata>() is not null
                || context.Request.Path.StartsWithSegments("/api/tracer", StringComparison.OrdinalIgnoreCase);
            if (isTracerRequest)
            {
                var bodyLimit = context.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>();
                if (bodyLimit is { IsReadOnly: false })
                    bodyLimit.MaxRequestBodySize = 4096;
                if (context.Request.ContentLength is > 4096)
                {
                    context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
                    return;
                }

                var host = context.Request.Host.Value;
                var origin = context.Request.Headers.Origin.ToString();
                var isSafeSameOriginRead = HttpMethods.IsGet(context.Request.Method) && string.IsNullOrEmpty(origin) &&
                    string.Equals(context.Request.Headers["Sec-Fetch-Site"].ToString(), "same-origin", StringComparison.Ordinal);
                var isSameOriginWrite = HttpMethods.IsPost(context.Request.Method) && string.IsNullOrEmpty(origin) &&
                    string.Equals(context.Request.Headers["Sec-Fetch-Site"].ToString(), "same-origin", StringComparison.Ordinal);
                var isAllowed = !string.IsNullOrEmpty(host) && allowedHosts.Contains(host) &&
                    (string.Equals(origin, allowedOrigin, StringComparison.Ordinal) || isSafeSameOriginRead || isSameOriginWrite) &&
                    (!HttpMethods.IsPost(context.Request.Method) || string.Equals(context.Request.ContentType?.Split(';', 2)[0].Trim(), "application/json", StringComparison.OrdinalIgnoreCase)) &&
                    context.Request.Headers.ContainsKey("X-ApiPlayground-Request");
                if (!isAllowed)
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await context.Response.WriteAsJsonAsync(new { error = "Request origin or headers are not allowed." });
                    return;
                }
            }

            await next();
        });
    }

    public static void MapTracerEndpoints(this WebApplication app)
    {
        app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
        app.MapGet("/api/tracer/history", async (RequestHistoryUseCase useCase, CancellationToken cancellationToken) =>
        {
            var items = await useCase.ListAsync(cancellationToken);
            return Results.Json(items.Select(ToHistoryDto));
        }).WithMetadata(new TracerRequestGuardMetadata());
        app.MapGet("/api/tracer/history/{id:guid}", async (Guid id, RequestHistoryUseCase useCase, CancellationToken cancellationToken) =>
        {
            var item = await useCase.FindAsync(id, cancellationToken);
            return item is null ? Results.NotFound() : Results.Json(ToHistoryDto(item));
        }).WithMetadata(new TracerRequestGuardMetadata());
        app.MapGet("/api/tracer/collections", async (SaveRequestUseCase useCase, CancellationToken cancellationToken) =>
        {
            try
            {
                var collections = await useCase.ListAsync(cancellationToken);
                return Results.Json(collections.Select(collection => new
                {
                    id = collection.Id,
                    name = collection.Name,
                    requests = collection.Requests.Select(ToSavedRequestDto),
                }));
            }
            catch (Exception)
            {
                return Results.Problem("Collections could not be loaded. Try again.", statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        }).WithMetadata(new TracerRequestGuardMetadata());
        app.MapPost("/api/tracer/collections/requests", async (SaveCollectionRequest request, SaveRequestUseCase useCase, CancellationToken cancellationToken) =>
        {
            try
            {
                var saved = await useCase.SaveAsync(new SaveRequestCommand(request.CollectionId, request.NewCollectionName,
                    request.Title, request.Method, request.Url, request.Headers ?? new Dictionary<string, string>(), request.Body), cancellationToken);
                return Results.Json(ToSavedRequestDto(saved), statusCode: StatusCodes.Status201Created);
            }
            catch (ArgumentException exception)
            {
                return Results.BadRequest(new { error = exception.Message });
            }
            catch (Exception)
            {
                return Results.Problem("The request could not be saved. Your request is still available to retry.", statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        }).WithMetadata(new TracerRequestGuardMetadata());
        app.MapPost("/api/tracer/get", async (TracerRequest request, GetTracerUseCase useCase, CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Url) || request.Url.Length > 2048)
                return Results.BadRequest(new { error = "Enter a URL to send a request." });
            if (request.Method is not ("GET" or "POST" or "PUT" or "PATCH" or "DELETE"))
                return Results.BadRequest(new { error = "Choose a supported HTTP method." });
            if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https") || uri.UserInfo.Length != 0 || uri.Fragment.Length != 0)
                return Results.BadRequest(new { error = "Enter a valid absolute HTTP or HTTPS URL without credentials or fragments." });
            if (request.Method is "GET" or "DELETE" && !string.IsNullOrWhiteSpace(request.Body))
                return Results.BadRequest(new { error = "JSON bodies are only supported for POST, PUT, and PATCH." });
            if (request.Body is { Length: > 0 } && !IsValidJson(request.Body))
                return Results.BadRequest(new { error = "Enter a valid JSON body." });
            if (request.Headers is not null && request.Headers.Keys.Distinct(StringComparer.OrdinalIgnoreCase).Count() != request.Headers.Count)
                return Results.BadRequest(new { error = "Enabled request header names must be unique." });
            if (request.Headers is not null && request.Headers.Any(header => string.IsNullOrWhiteSpace(header.Key) ||
                header.Key.Equals("Host", StringComparison.OrdinalIgnoreCase) ||
                header.Key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase) ||
                header.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase)))
                return Results.BadRequest(new { error = "Host, Content-Length, and Content-Type headers are managed by the request sender." });

            var headers = request.Headers ?? new Dictionary<string, string>();
            var contentType = string.IsNullOrWhiteSpace(request.Body) ? null : "application/json";
            var outboundRequest = new OutboundRequest(uri, request.Method, headers,
                string.IsNullOrWhiteSpace(request.Body) ? ReadOnlyMemory<byte>.Empty : Encoding.UTF8.GetBytes(request.Body), contentType);
            var result = await useCase.ExecuteAsync(outboundRequest, cancellationToken);
            if (result.Outbound is null)
                return Results.BadRequest(new { error = result.Error });
            if (!result.Outbound.Succeeded && result.Outbound.Failure != OutboundFailure.PolicyRejected)
                return Results.Json(new
                {
                    failure = result.Outbound.Failure.ToString(), statusCode = result.Outbound.StatusCode,
                    elapsedMilliseconds = result.Outbound.Elapsed.TotalMilliseconds, headers = result.Outbound.Headers,
                    body = Encoding.UTF8.GetString(result.Outbound.Body.Span), persistenceVerified = result.PersistenceVerified,
                    error = result.Error, requestSent = result.Outbound.DispatchStarted, historySaved = result.HistorySaved,
                }, statusCode: result.Outbound.Failure switch
                {
                    OutboundFailure.Timeout => StatusCodes.Status504GatewayTimeout,
                    OutboundFailure.Cancelled => 499,
                    _ => StatusCodes.Status502BadGateway,
                });

            var httpStatus = result.Outbound.Failure switch
            {
                OutboundFailure.None => StatusCodes.Status200OK,
                OutboundFailure.PolicyRejected => StatusCodes.Status403Forbidden,
                _ => StatusCodes.Status200OK,
            };
            return Results.Json(new
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
            }, statusCode: httpStatus);
        }).WithMetadata(new TracerRequestGuardMetadata());
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

    private static object ToHistoryDto(RequestHistorySnapshot item) => new
    {
        id = item.Id, createdAt = item.CreatedAt, method = item.Method, url = item.Url,
        redactedQueryParameters = item.RedactedQueryParameters,
        headers = item.Headers.Select(header => new
        {
            name = header.Key, value = item.RedactedHeaders.Contains(header.Key, StringComparer.OrdinalIgnoreCase) ? string.Empty : header.Value,
            redacted = item.RedactedHeaders.Contains(header.Key, StringComparer.OrdinalIgnoreCase),
        }),
        redactedHeaders = item.RedactedHeaders, body = item.Body, failure = item.Failure,
        statusCode = item.StatusCode, elapsedMilliseconds = item.ElapsedMilliseconds,
    };

    private static object ToSavedRequestDto(SavedRequestSnapshot item) => new
    {
        id = item.Id,
        createdAt = item.CreatedAt,
        collectionId = item.CollectionId,
        collectionName = item.CollectionName,
        title = item.Title,
        method = item.Method,
        url = item.Url,
        headers = item.Headers.Select(header => new
        {
            name = header.Key,
            value = item.RedactedHeaders.Contains(header.Key, StringComparer.OrdinalIgnoreCase) ? string.Empty : header.Value,
            redacted = item.RedactedHeaders.Contains(header.Key, StringComparer.OrdinalIgnoreCase),
        }),
        redactedHeaders = item.RedactedHeaders,
        body = item.Body,
    };
}

public sealed record TracerRequest(string Url, string Method = "GET", string? Body = null, Dictionary<string, string>? Headers = null);
public sealed record SaveCollectionRequest(Guid? CollectionId, string? NewCollectionName, string Title, string Method, string Url,
    Dictionary<string, string>? Headers = null, string? Body = null);
public sealed class TracerRequestGuardMetadata;