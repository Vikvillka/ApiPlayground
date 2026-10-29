namespace ApiPlayground.Application.Outbound;

public interface IRequestCollectionRepository
{
    Task<IReadOnlyList<RequestCollectionSnapshot>> ListAsync(CancellationToken cancellationToken = default);
    Task<SavedRequestSnapshot> SaveAsync(SavedRequestSnapshot request, Guid? collectionId, string? newCollectionName,
        CancellationToken cancellationToken = default);
}
