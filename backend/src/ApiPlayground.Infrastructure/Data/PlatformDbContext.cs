using Microsoft.EntityFrameworkCore;

namespace ApiPlayground.Api.Data;

public sealed class PlatformDbContext(DbContextOptions<PlatformDbContext> options) : DbContext(options)
{
	public DbSet<RequestHistoryEntity> RequestHistory => Set<RequestHistoryEntity>();

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
	}
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
