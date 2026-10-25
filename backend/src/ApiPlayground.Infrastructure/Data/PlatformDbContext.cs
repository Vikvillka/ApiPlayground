using Microsoft.EntityFrameworkCore;

namespace ApiPlayground.Api.Data;

public sealed class PlatformDbContext(DbContextOptions<PlatformDbContext> options) : DbContext(options)
{
	public DbSet<RequestHistoryEntity> RequestHistory => Set<RequestHistoryEntity>();
	public DbSet<RequestCollectionEntity> RequestCollections => Set<RequestCollectionEntity>();
	public DbSet<SavedRequestEntity> SavedRequests => Set<SavedRequestEntity>();

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		var history = modelBuilder.Entity<RequestHistoryEntity>();
		history.ToTable("request_history");
		history.HasKey(item => item.Id);
		history.Property(item => item.Id).HasColumnName("id");
		history.Property(item => item.CreatedAt).HasColumnName("created_at").IsRequired();
		history.Property(item => item.Method).HasColumnName("method").HasMaxLength(8).IsRequired();
		history.Property(item => item.Url).HasColumnName("url").HasMaxLength(2048).IsRequired();
		history.Property(item => item.RedactedQueryParametersJson).HasColumnName("redacted_query_parameters_json").IsRequired();
		history.Property(item => item.HeadersJson).HasColumnName("headers_json").IsRequired();
		history.Property(item => item.RedactedHeadersJson).HasColumnName("redacted_headers_json").IsRequired();
		history.Property(item => item.Body).HasColumnName("body");
		history.Property(item => item.Failure).HasColumnName("failure").HasMaxLength(40).IsRequired();
		history.Property(item => item.StatusCode).HasColumnName("status_code");
		history.Property(item => item.ElapsedMilliseconds).HasColumnName("elapsed_ms").IsRequired();
		history.HasIndex(item => new { item.CreatedAt, item.Id });

		var collection = modelBuilder.Entity<RequestCollectionEntity>();
		collection.ToTable("request_collections");
		collection.HasKey(item => item.Id);
		collection.Property(item => item.Id).HasColumnName("id");
		collection.Property(item => item.Name).HasColumnName("name").HasMaxLength(80).IsRequired();
		collection.HasIndex(item => item.Name).IsUnique();
		collection.HasMany(item => item.Requests).WithOne(item => item.Collection)
			.HasForeignKey(item => item.CollectionId).OnDelete(DeleteBehavior.Cascade);

		var savedRequest = modelBuilder.Entity<SavedRequestEntity>();
		savedRequest.ToTable("saved_requests");
		savedRequest.HasKey(item => item.Id);
		savedRequest.Property(item => item.Id).HasColumnName("id");
		savedRequest.Property(item => item.CreatedAt).HasColumnName("created_at").IsRequired();
		savedRequest.Property(item => item.CollectionId).HasColumnName("collection_id");
		savedRequest.Property(item => item.Title).HasColumnName("title").HasMaxLength(120).IsRequired();
		savedRequest.Property(item => item.Method).HasColumnName("method").HasMaxLength(8).IsRequired();
		savedRequest.Property(item => item.Url).HasColumnName("url").HasMaxLength(2048).IsRequired();
		savedRequest.Property(item => item.HeadersJson).HasColumnName("headers_json").IsRequired();
		savedRequest.Property(item => item.RedactedHeadersJson).HasColumnName("redacted_headers_json").IsRequired();
		savedRequest.Property(item => item.Body).HasColumnName("body");
		savedRequest.HasIndex(item => new { item.CollectionId, item.CreatedAt, item.Id });
	}
}

public sealed class RequestCollectionEntity
{
	public Guid Id { get; set; }
	public string Name { get; set; } = string.Empty;
	public List<SavedRequestEntity> Requests { get; set; } = [];
}

public sealed class SavedRequestEntity
{
	public Guid Id { get; set; }
	public DateTimeOffset CreatedAt { get; set; }
	public Guid CollectionId { get; set; }
	public RequestCollectionEntity Collection { get; set; } = null!;
	public string Title { get; set; } = string.Empty;
	public string Method { get; set; } = string.Empty;
	public string Url { get; set; } = string.Empty;
	public string HeadersJson { get; set; } = "{}";
	public string RedactedHeadersJson { get; set; } = "[]";
	public string? Body { get; set; }
}

public sealed class RequestHistoryEntity
{
	public Guid Id { get; set; }
	public DateTimeOffset CreatedAt { get; set; }
	public string Method { get; set; } = string.Empty;
	public string Url { get; set; } = string.Empty;
	public string RedactedQueryParametersJson { get; set; } = "[]";
	public string HeadersJson { get; set; } = "{}";
	public string RedactedHeadersJson { get; set; } = "[]";
	public string? Body { get; set; }
	public string Failure { get; set; } = string.Empty;
	public int? StatusCode { get; set; }
	public double ElapsedMilliseconds { get; set; }
}
