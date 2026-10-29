using ApiPlayground.Api.Data;
using ApiPlayground.Application.Outbound;
using ApiPlayground.Domain.Requests;
using Microsoft.EntityFrameworkCore;

namespace ApiPlayground.Infrastructure.Outbound;

public sealed class PostgresRequestCollectionRepository(PlatformDbContext dbContext) : IRequestCollectionRepository
{
    public async Task<IReadOnlyList<RequestCollectionSnapshot>> ListAsync(CancellationToken cancellationToken = default)
    {
        var collections = await dbContext.RequestCollections.AsNoTracking()
            .Include(collection => collection.Requests)
            .OrderBy(collection => collection.Name)
            .ToListAsync(cancellationToken);
        return collections.Select(collection => new RequestCollectionSnapshot(collection.Id, collection.Name,
            collection.Requests.OrderByDescending(request => request.CreatedAt).ThenByDescending(request => request.Id)
                .Select(request => ToSnapshot(request, collection.Name)).ToArray())).ToArray();
    }

    public async Task<SavedRequestSnapshot> SaveAsync(SavedRequestSnapshot request, Guid? collectionId, string? newCollectionName,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        RequestCollectionEntity collection;
        if (collectionId is { } selectedId)
        {
            collection = await dbContext.RequestCollections.SingleOrDefaultAsync(item => item.Id == selectedId, cancellationToken)
                ?? throw new InvalidOperationException("The selected collection no longer exists.");
        }
        else
        {
            collection = new RequestCollectionEntity { Id = Guid.NewGuid(), Name = newCollectionName! };
            dbContext.RequestCollections.Add(collection);
        }

        var entity = new SavedRequestEntity
        {
            Id = request.Id,
            CreatedAt = request.CreatedAt,
            Collection = collection,
            CollectionId = collection.Id,
            Title = request.Title,
            Method = request.Method,
            Url = request.Url,
            Headers = new Dictionary<string, string>(request.Headers),
            RedactedHeaders = new List<string>(request.RedactedHeaders),
            Body = request.Body,
        };
        dbContext.SavedRequests.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToSnapshot(entity, collection.Name);
    }

    private static SavedRequestSnapshot ToSnapshot(SavedRequestEntity entity, string collectionName) => new(
        entity.Id, entity.CreatedAt, entity.CollectionId, collectionName, entity.Title, entity.Method, entity.Url,
        entity.Headers, entity.RedactedHeaders, entity.Body);
}