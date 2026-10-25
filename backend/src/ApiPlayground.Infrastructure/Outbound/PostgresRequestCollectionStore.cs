using System.Text.Json;
using ApiPlayground.Api.Data;
using ApiPlayground.Application.Outbound;
using Microsoft.EntityFrameworkCore;

namespace ApiPlayground.Infrastructure.Outbound;

public sealed class PostgresRequestCollectionStore(PlatformDbContext dbContext) : IRequestCollectionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

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
            HeadersJson = JsonSerializer.Serialize(request.Headers, JsonOptions),
            RedactedHeadersJson = JsonSerializer.Serialize(request.RedactedHeaders, JsonOptions),
            Body = request.Body,
        };
        dbContext.SavedRequests.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToSnapshot(entity, collection.Name);
    }

    private static SavedRequestSnapshot ToSnapshot(SavedRequestEntity entity, string collectionName) => new(
        entity.Id, entity.CreatedAt, entity.CollectionId, collectionName, entity.Title, entity.Method, entity.Url,
        JsonSerializer.Deserialize<Dictionary<string, string>>(entity.HeadersJson, JsonOptions) ?? new(),
        JsonSerializer.Deserialize<List<string>>(entity.RedactedHeadersJson, JsonOptions) ?? new(), entity.Body);
}