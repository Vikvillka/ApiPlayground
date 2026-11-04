using ApiPlayground.Api.Data;
using ApiPlayground.Application.Outbound;
using ApiPlayground.Domain.Requests;
using Microsoft.EntityFrameworkCore;

namespace ApiPlayground.Infrastructure.Outbound;

public sealed class PostgresRequestHistoryRepository(PlatformDbContext dbContext) : IRequestHistoryRepository
{
    public async Task SaveAsync(Guid ownerId, RequestHistorySnapshot snapshot, CancellationToken cancellationToken = default)
    {
        dbContext.RequestHistory.Add(ToEntity(ownerId, snapshot));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RequestHistorySnapshot>> ListAsync(Guid ownerId, CancellationToken cancellationToken = default)
    {
        var rows = await dbContext.RequestHistory.AsNoTracking()
            .Where(row => row.OwnerId == ownerId)
            .OrderByDescending(row => row.CreatedAt).ThenByDescending(row => row.Id)
            .ToListAsync(cancellationToken);
        return rows.Select(ToSnapshot).ToArray();
    }

    public async Task<RequestHistorySnapshot?> FindAsync(Guid ownerId, Guid id, CancellationToken cancellationToken = default)
    {
        var row = await dbContext.RequestHistory.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id && item.OwnerId == ownerId, cancellationToken);
        return row is null ? null : ToSnapshot(row);
    }

    private static RequestHistoryEntity ToEntity(Guid ownerId, RequestHistorySnapshot snapshot) => new()
    {
        Id = snapshot.Id, OwnerId = ownerId, CreatedAt = snapshot.CreatedAt, Method = snapshot.Method, Url = snapshot.Url,
        Headers = new Dictionary<string, string>(snapshot.Headers),
        RedactedQueryParameters = new List<string>(snapshot.RedactedQueryParameters),
        RedactedHeaders = new List<string>(snapshot.RedactedHeaders),
        Body = snapshot.Body, Failure = snapshot.Failure, StatusCode = snapshot.StatusCode,
        ElapsedMilliseconds = snapshot.ElapsedMilliseconds,
    };

    private static RequestHistorySnapshot ToSnapshot(RequestHistoryEntity entity) => new(
        entity.Id, entity.CreatedAt, entity.Method, entity.Url,
        entity.RedactedQueryParameters, entity.Headers, entity.RedactedHeaders,
        entity.Body, entity.Failure, entity.StatusCode, entity.ElapsedMilliseconds);
}