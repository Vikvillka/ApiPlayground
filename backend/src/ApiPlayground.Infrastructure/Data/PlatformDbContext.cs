using System.Text.Json;
using ApiPlayground.Domain.Requests;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ApiPlayground.Api.Data;

public sealed class PlatformDbContext(DbContextOptions<PlatformDbContext> options)
	: IdentityUserContext<IdentityUser<Guid>, Guid>(options)
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	public DbSet<RequestHistoryEntity> RequestHistory => Set<RequestHistoryEntity>();
	public DbSet<RequestCollectionEntity> RequestCollections => Set<RequestCollectionEntity>();
	public DbSet<SavedRequestEntity> SavedRequests => Set<SavedRequestEntity>();

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		base.OnModelCreating(modelBuilder);

		var history = modelBuilder.Entity<RequestHistoryEntity>();
		history.ToTable("request_history");
		history.HasKey(item => item.Id);
		history.Property(item => item.Id).HasColumnName("id");
		history.Property(item => item.CreatedAt).HasColumnName("created_at").IsRequired();
		history.Property(item => item.Method).HasColumnName("method").HasMaxLength(8).IsRequired();
		history.Property(item => item.Url).HasColumnName("url").HasMaxLength(2048).IsRequired();
		history.Property(item => item.RedactedQueryParameters).HasColumnName("redacted_query_parameters_json")
			.HasConversion(JsonConverter<List<string>>(), JsonComparer<List<string>>()).IsRequired();
		history.Property(item => item.Headers).HasColumnName("headers_json")
			.HasConversion(JsonConverter<Dictionary<string, string>>(), JsonComparer<Dictionary<string, string>>()).IsRequired();
		history.Property(item => item.RedactedHeaders).HasColumnName("redacted_headers_json")
			.HasConversion(JsonConverter<List<string>>(), JsonComparer<List<string>>()).IsRequired();
		history.Property(item => item.Body).HasColumnName("body");
		history.Property(item => item.Failure).HasColumnName("failure").HasMaxLength(40).IsRequired();
		history.Property(item => item.StatusCode).HasColumnName("status_code");
		history.Property(item => item.ElapsedMilliseconds).HasColumnName("elapsed_ms").IsRequired();
		history.Property(item => item.OwnerId).HasColumnName("owner_id").IsRequired();
		history.HasOne<IdentityUser<Guid>>().WithMany().HasForeignKey(item => item.OwnerId).OnDelete(DeleteBehavior.Cascade);
		history.HasIndex(item => new { item.CreatedAt, item.Id });

		var collection = modelBuilder.Entity<RequestCollectionEntity>();
		collection.ToTable("request_collections");
		collection.HasKey(item => item.Id);
		collection.Property(item => item.Id).HasColumnName("id");
		collection.Property(item => item.Name).HasColumnName("name").HasMaxLength(80).IsRequired();
		collection.Property(item => item.OwnerId).HasColumnName("owner_id").IsRequired();
		collection.HasOne<IdentityUser<Guid>>().WithMany().HasForeignKey(item => item.OwnerId).OnDelete(DeleteBehavior.Cascade);
		collection.HasIndex(item => new { item.OwnerId, item.Name }).IsUnique();
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
		savedRequest.Property(item => item.Headers).HasColumnName("headers_json")
			.HasConversion(JsonConverter<Dictionary<string, string>>(), JsonComparer<Dictionary<string, string>>()).IsRequired();
		savedRequest.Property(item => item.RedactedHeaders).HasColumnName("redacted_headers_json")
			.HasConversion(JsonConverter<List<string>>(), JsonComparer<List<string>>()).IsRequired();
		savedRequest.Property(item => item.Body).HasColumnName("body");
		savedRequest.Property(item => item.OwnerId).HasColumnName("owner_id").IsRequired();
		savedRequest.HasOne<IdentityUser<Guid>>().WithMany().HasForeignKey(item => item.OwnerId).OnDelete(DeleteBehavior.Cascade);
		savedRequest.HasIndex(item => new { item.CollectionId, item.CreatedAt, item.Id });
	}

	// Typed Domain fields are stored in the existing JSON text columns; the serialized form is unchanged.
	private static ValueConverter<T, string> JsonConverter<T>() where T : new() => new(
		value => JsonSerializer.Serialize(value, JsonOptions),
		json => JsonSerializer.Deserialize<T>(json, JsonOptions) ?? new T());

	private static ValueComparer<T> JsonComparer<T>() where T : new() => new(
		(left, right) => JsonSerializer.Serialize(left, JsonOptions) == JsonSerializer.Serialize(right, JsonOptions),
		value => JsonSerializer.Serialize(value, JsonOptions).GetHashCode(),
		value => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, JsonOptions), JsonOptions) ?? new T());
}
