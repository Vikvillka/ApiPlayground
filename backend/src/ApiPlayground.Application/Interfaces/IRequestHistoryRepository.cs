namespace ApiPlayground.Application.Outbound;

public interface IRequestHistoryRepository
{
    Task SaveAsync(Guid ownerId, RequestHistorySnapshot snapshot, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RequestHistorySnapshot>> ListAsync(Guid ownerId, CancellationToken cancellationToken = default);
    Task<RequestHistorySnapshot?> FindAsync(Guid ownerId, Guid id, CancellationToken cancellationToken = default);
}
