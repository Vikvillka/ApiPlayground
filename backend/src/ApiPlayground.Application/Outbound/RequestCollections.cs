namespace ApiPlayground.Application.Outbound;

public sealed record SavedRequestSnapshot(
    Guid Id,
    DateTimeOffset CreatedAt,
    Guid CollectionId,
    string CollectionName,
    string Title,
    string Method,
    string Url,
    IReadOnlyDictionary<string, string> Headers,
    IReadOnlyList<string> RedactedHeaders,
    string? Body);

public sealed record RequestCollectionSnapshot(
    Guid Id,
    string Name,
    IReadOnlyList<SavedRequestSnapshot> Requests);

public sealed record SaveRequestCommand(
    Guid? CollectionId,
    string? NewCollectionName,
    string Title,
    string Method,
    string Url,
    IReadOnlyDictionary<string, string> Headers,
    string? Body);

public interface IRequestCollectionStore
{
    Task<IReadOnlyList<RequestCollectionSnapshot>> ListAsync(CancellationToken cancellationToken = default);
    Task<SavedRequestSnapshot> SaveAsync(SavedRequestSnapshot request, Guid? collectionId, string? newCollectionName,
        CancellationToken cancellationToken = default);
}

public sealed class SaveRequestUseCase(IRequestCollectionStore store)
{
    public Task<IReadOnlyList<RequestCollectionSnapshot>> ListAsync(CancellationToken cancellationToken = default) =>
        store.ListAsync(cancellationToken);

    public async Task<SavedRequestSnapshot> SaveAsync(SaveRequestCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if ((command.CollectionId is null) == string.IsNullOrWhiteSpace(command.NewCollectionName))
            throw new ArgumentException("Choose an existing collection or enter a new collection name.");
        if (string.IsNullOrWhiteSpace(command.Title) || command.Title.Length > 120)
            throw new ArgumentException("Enter a request title of 1 to 120 characters.");
        if (command.NewCollectionName is { } collectionName &&
            (string.IsNullOrWhiteSpace(collectionName) || collectionName.Length > 80))
            throw new ArgumentException("Enter a collection name of 1 to 80 characters.");
        if (!RequestValidation.IsSupportedMethod(command.Method))
            throw new ArgumentException("Choose a supported HTTP method.");
        if (string.IsNullOrWhiteSpace(command.Url) || command.Url.Length > 2048 ||
            !RequestValidation.TryCreateHttpUri(command.Url, out _))
            throw new ArgumentException("Enter a valid absolute HTTP or HTTPS URL within the 2048 character limit.");
        if (command.Body is { Length: > 0 } && command.Method is ("GET" or "DELETE"))
            throw new ArgumentException("JSON bodies are only supported for POST, PUT, and PATCH.");
        if (!string.IsNullOrWhiteSpace(command.Body))
        {
            try { using var _ = System.Text.Json.JsonDocument.Parse(command.Body); }
            catch (System.Text.Json.JsonException) { throw new ArgumentException("Enter a valid JSON body."); }
        }
        if (command.Headers.Keys.Any(string.IsNullOrWhiteSpace) ||
            RequestValidation.HasDuplicateHeaderNames(command.Headers.Keys))
            throw new ArgumentException("Enabled request header names must be unique and non-empty.");

        var (headers, redactedHeaders) = RequestHistoryRedaction.SanitizeHeaders(command.Headers);

        var sanitizedUrl = RequestHistoryRedaction.SanitizeUrl(command.Url, out _);
        var snapshot = new SavedRequestSnapshot(Guid.NewGuid(), DateTimeOffset.UtcNow, command.CollectionId ?? Guid.Empty,
            string.Empty, command.Title.Trim(), command.Method, sanitizedUrl, headers, redactedHeaders,
            string.IsNullOrWhiteSpace(command.Body) ? null : command.Body);
        return await store.SaveAsync(snapshot, command.CollectionId, command.NewCollectionName?.Trim(), cancellationToken);
    }
}