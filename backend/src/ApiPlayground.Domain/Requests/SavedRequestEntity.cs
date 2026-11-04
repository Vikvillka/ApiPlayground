namespace ApiPlayground.Domain.Requests;

public sealed class SavedRequestEntity
{
    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid CollectionId { get; set; }
    public RequestCollectionEntity Collection { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public Dictionary<string, string> Headers { get; set; } = [];
    public List<string> RedactedHeaders { get; set; } = [];
    public string? Body { get; set; }
}
