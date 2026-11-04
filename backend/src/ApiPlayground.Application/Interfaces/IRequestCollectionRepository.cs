namespace ApiPlayground.Application.Outbound;

public interface IRequestCollectionRepository
{
    Task<IReadOnlyList<RequestCollectionSnapshot>> ListAsync(Guid ownerId, CancellationToken cancellationToken = default);
    Task<SavedRequestSnapshot> SaveAsync(Guid ownerId, SavedRequestSnapshot request, Guid? collectionId, string? newCollectionName,
        CancellationToken cancellationToken = default);
}
