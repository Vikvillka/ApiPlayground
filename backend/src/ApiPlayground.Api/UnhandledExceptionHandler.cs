using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ApiPlayground.Api;

// Converts otherwise unhandled exceptions into a generic RFC 9457 Problem Details body. The exception message,
// stack trace, and provider details are logged by type only and never returned to the client.
internal sealed class UnhandledExceptionHandler(
    ILogger<UnhandledExceptionHandler> logger,
    IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        // A client that disconnected cancelled the request; keep the framework's existing cancellation handling.
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
            return false;

        // Client-side request errors (for example a body over the server limit) keep the framework's own status handling.
        if (exception is BadHttpRequestException)
            return false;

        ApiLog.UnhandledException(logger, exception.GetType().Name, httpContext.Request.Method,
            httpContext.Request.Path, httpContext.TraceIdentifier);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An unexpected error occurred.",
                Detail = "The request could not be completed. Try again later.",
                Extensions = { ["traceId"] = httpContext.TraceIdentifier },
            },
        });
    }
}
