namespace ApiPlayground.Application.Outbound;

public interface IRequestHistoryRepository
{
    Task SaveAsync(RequestHistorySnapshot snapshot, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RequestHistorySnapshot>> ListAsync(CancellationToken cancellationToken = default);
    Task<RequestHistorySnapshot?> FindAsync(Guid id, CancellationToken cancellationToken = default);
}
