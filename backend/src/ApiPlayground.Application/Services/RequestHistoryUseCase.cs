namespace ApiPlayground.Application.Outbound;

public sealed class RequestHistoryUseCase(IRequestHistoryRepository store)
{
    public Task<IReadOnlyList<RequestHistorySnapshot>> ListAsync(CancellationToken cancellationToken = default) =>
        store.ListAsync(cancellationToken);

    public Task<RequestHistorySnapshot?> FindAsync(Guid id, CancellationToken cancellationToken = default) =>
        store.FindAsync(id, cancellationToken);
}
