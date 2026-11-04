namespace ApiPlayground.Domain.Requests;

public sealed class RequestHistoryEntity
{
    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string Method { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public List<string> RedactedQueryParameters { get; set; } = [];
    public Dictionary<string, string> Headers { get; set; } = [];
    public List<string> RedactedHeaders { get; set; } = [];
    public string? Body { get; set; }
    public string Failure { get; set; } = string.Empty;
    public int? StatusCode { get; set; }
    public double ElapsedMilliseconds { get; set; }
}
