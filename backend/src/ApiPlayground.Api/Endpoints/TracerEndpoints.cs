using ApiPlayground.Application.Outbound;
using ApiPlayground.Infrastructure.Outbound;

namespace ApiPlayground.Api;

public static class TracerEndpoints
{
    private const int TracerMaxRequestBodyBytes = 4096;

    // The export body carries the response to export, so it may be as large as one outbound response plus JSON escaping.
    private static readonly int ExportMaxRequestBodyBytes = 2 * OutboundPolicy.MaximumResponseBytes + 64 * 1024;

    public static void UseRequestGuard(this WebApplication app)
    {
        var allowedHosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "localhost:5000" };
        const string allowedOrigin = "http://127.0.0.1:5173";
        app.Use(async (context, next) =>
        {
            var isTracerRequest = context.GetEndpoint()?.Metadata.GetMetadata<TracerRequestGuardMetadata>() is not null
                || context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase);
            if (isTracerRequest)
            {
                var maxBodyBytes = context.Request.Path.Equals("/api/tracer/export", StringComparison.OrdinalIgnoreCase)
                    ? ExportMaxRequestBodyBytes
                    : TracerMaxRequestBodyBytes;
                var bodyLimit = context.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>();
                if (bodyLimit is { IsReadOnly: false })
                    bodyLimit.MaxRequestBodySize = maxBodyBytes;
                if (context.Request.ContentLength is long contentLength && contentLength > maxBodyBytes)
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
        app.MapGet("/health", () => Results.Ok(new { status = "healthy" })).AllowAnonymous();
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class TracerRequestGuardMetadata : Attribute;