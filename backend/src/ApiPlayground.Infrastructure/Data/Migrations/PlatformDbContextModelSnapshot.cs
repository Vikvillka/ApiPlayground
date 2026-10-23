using ApiPlayground.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace ApiPlayground.Api.Data.Migrations;

[DbContext(typeof(PlatformDbContext))]
public sealed class PlatformDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "10.0.0");
        modelBuilder.Entity<RequestHistoryEntity>(entity =>
        {
            entity.ToTable("request_history");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.CreatedAt, item.Id });
            entity.Property(item => item.Id).HasColumnName("id");
            entity.Property(item => item.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(item => item.Method).HasColumnName("method").HasMaxLength(8).IsRequired();
            entity.Property(item => item.Url).HasColumnName("url").HasMaxLength(2048).IsRequired();
            entity.Property(item => item.RedactedQueryParametersJson).HasColumnName("redacted_query_parameters_json").IsRequired();
            entity.Property(item => item.HeadersJson).HasColumnName("headers_json").IsRequired();
            entity.Property(item => item.RedactedHeadersJson).HasColumnName("redacted_headers_json").IsRequired();
            entity.Property(item => item.Body).HasColumnName("body");
            entity.Property(item => item.Failure).HasColumnName("failure").HasMaxLength(40).IsRequired();
            entity.Property(item => item.StatusCode).HasColumnName("status_code");
            entity.Property(item => item.ElapsedMilliseconds).HasColumnName("elapsed_ms").IsRequired();
        });
    }
}
