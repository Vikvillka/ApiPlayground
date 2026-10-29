namespace ApiPlayground.Domain.Requests;

public sealed class RequestCollectionEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<SavedRequestEntity> Requests { get; set; } = [];
}
