using ApiPlayground.Application.Outbound;

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
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class TracerRequestGuardMetadata : Attribute;