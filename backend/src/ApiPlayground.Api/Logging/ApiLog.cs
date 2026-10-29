using Microsoft.Extensions.Logging;

namespace ApiPlayground.Api;

// Structured API log messages. Parameters are exception type names and request routing metadata only;
// exception messages, request bodies, headers, and query strings are never logged here.
internal static partial class ApiLog
{
    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled {ExceptionType} for {Method} {Path}; trace {TraceId}.")]
    public static partial void UnhandledException(ILogger logger, string exceptionType, string method, string path, string traceId);

    [LoggerMessage(Level = LogLevel.Error, Message = "{Operation} failed with {ExceptionType}; trace {TraceId}.")]
    public static partial void CollectionOperationFailed(ILogger logger, string operation, string exceptionType, string traceId);
}
