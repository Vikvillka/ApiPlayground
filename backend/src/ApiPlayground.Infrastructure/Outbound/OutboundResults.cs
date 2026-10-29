using System.Collections.ObjectModel;
using System.Diagnostics;
using ApiPlayground.Application.Outbound;

namespace ApiPlayground.Infrastructure.Outbound;

internal static class OutboundResults
{
    internal static OutboundResult Failed(
        OutboundFailure failure,
        Stopwatch stopwatch,
        string diagnostic,
        int? statusCode = null,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? headers = null,
        bool dispatchStarted = false)
    {
        stopwatch.Stop();
        return new OutboundResult(failure, statusCode, stopwatch.Elapsed,
            headers ?? new ReadOnlyDictionary<string, IReadOnlyList<string>>(new Dictionary<string, IReadOnlyList<string>>()),
            ReadOnlyMemory<byte>.Empty, diagnostic, dispatchStarted);
    }
}
