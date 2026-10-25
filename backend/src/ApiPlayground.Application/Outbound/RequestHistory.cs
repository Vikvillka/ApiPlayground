namespace ApiPlayground.Application.Outbound;

public sealed record RequestHistorySnapshot(
    Guid Id,
    DateTimeOffset CreatedAt,
    string Method,
    string Url,
    IReadOnlyList<string> RedactedQueryParameters,
    IReadOnlyDictionary<string, string> Headers,
    IReadOnlyList<string> RedactedHeaders,
    string? Body,
    string Failure,
    int? StatusCode,
    double ElapsedMilliseconds);

public interface IRequestHistoryStore
{
    Task SaveAsync(RequestHistorySnapshot snapshot, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RequestHistorySnapshot>> ListAsync(CancellationToken cancellationToken = default);
    Task<RequestHistorySnapshot?> FindAsync(Guid id, CancellationToken cancellationToken = default);
}

public static class RequestHistoryRedaction
{
    public static bool IsSensitiveHeader(string name) =>
        name.Equals("Authorization", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("Proxy-Authorization", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("Cookie", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("Set-Cookie", StringComparison.OrdinalIgnoreCase) ||
        ContainsAny(name, "api-key", "apikey", "token");

    public static bool IsSensitiveQueryName(string name) =>
        ContainsAny(name, "token", "api-key", "apikey", "secret", "password");

    public static string SanitizeUrl(string url, out IReadOnlyList<string> redactedNames)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            redactedNames = [];
            return url;
        }

        return SanitizeUrl(uri, out redactedNames);
    }

    public static RequestHistorySnapshot Create(OutboundRequest request, OutboundResult result, DateTimeOffset? now = null)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var redacted = new List<string>();
        foreach (var (name, value) in request.Headers ?? new Dictionary<string, string>())
        {
            if (IsSensitiveHeader(name))
            {
                headers[name] = string.Empty;
                redacted.Add(name);
            }
            else headers[name] = value;
        }

        return new RequestHistorySnapshot(Guid.NewGuid(), now ?? DateTimeOffset.UtcNow, request.Method,
            SanitizeUrl(request.Uri, out var redactedQueryParameters), redactedQueryParameters, headers, redacted,
            request.Body.IsEmpty ? null : System.Text.Encoding.UTF8.GetString(request.Body.Span),
            result.Failure.ToString(), result.StatusCode, result.Elapsed.TotalMilliseconds);
    }

    private static string SanitizeUrl(Uri uri, out IReadOnlyList<string> redactedNames)
    {
        var result = new List<string>();
        var parts = uri.Query.TrimStart('?').Split('&', StringSplitOptions.None);
        for (var index = 0; index < parts.Length; index++)
        {
            var part = parts[index];
            var separator = part.IndexOf('=');
            var encodedName = separator < 0 ? part : part[..separator];
            string name;
            try { name = Uri.UnescapeDataString(encodedName.Replace('+', ' ')); }
            catch (UriFormatException) { name = encodedName; }
            if (IsSensitiveQueryName(name))
            {
                result.Add(name);
                parts[index] = encodedName + "=";
            }
        }
        redactedNames = result;
        if (parts.Length == 1 && parts[0].Length == 0) return uri.GetLeftPart(UriPartial.Path);
        var builder = new UriBuilder(uri) { Query = string.Join('&', parts) };
        return builder.Uri.AbsoluteUri;
    }

    private static bool ContainsAny(string value, params string[] fragments) =>
        fragments.Any(fragment => value.Contains(fragment, StringComparison.OrdinalIgnoreCase));
}

public sealed class RequestHistoryUseCase(IRequestHistoryStore store)
{
    public Task<IReadOnlyList<RequestHistorySnapshot>> ListAsync(CancellationToken cancellationToken = default) =>
        store.ListAsync(cancellationToken);

    public Task<RequestHistorySnapshot?> FindAsync(Guid id, CancellationToken cancellationToken = default) =>
        store.FindAsync(id, cancellationToken);
}