namespace ApiPlayground.Application.Outbound;

public sealed class RequestHistoryUseCase(IRequestHistoryRepository store)
{
    public Task<IReadOnlyList<RequestHistorySnapshot>> ListAsync(Guid ownerId, CancellationToken cancellationToken = default) =>
        store.ListAsync(ownerId, cancellationToken);

    public Task<RequestHistorySnapshot?> FindAsync(Guid ownerId, Guid id, CancellationToken cancellationToken = default) =>
        store.FindAsync(ownerId, id, cancellationToken);
}
