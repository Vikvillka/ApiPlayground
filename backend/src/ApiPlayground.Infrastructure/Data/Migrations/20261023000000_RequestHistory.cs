using ApiPlayground.Api.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore;

namespace ApiPlayground.Api.Data.Migrations;

[DbContext(typeof(PlatformDbContext))]
[Migration("20261023000000_RequestHistory")]
public sealed class RequestHistory : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "request_history",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                method = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                redacted_query_parameters_json = table.Column<string>(type: "text", nullable: false),
                headers_json = table.Column<string>(type: "text", nullable: false),
                redacted_headers_json = table.Column<string>(type: "text", nullable: false),
                body = table.Column<string>(type: "text", nullable: true),
                failure = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                status_code = table.Column<int>(type: "integer", nullable: true),
                elapsed_ms = table.Column<double>(type: "double precision", nullable: false),
            }, constraints: table => table.PrimaryKey("pk_request_history", item => item.id));
        migrationBuilder.CreateIndex(name: "ix_request_history_created_at_id", table: "request_history", columns: new[] { "created_at", "id" });
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable(name: "request_history");

    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "10.0.0");
        // String-based entity name keeps this historical model independent of the current CLR types.
        modelBuilder.Entity("ApiPlayground.Api.Data.RequestHistoryEntity", entity =>
        {
            entity.ToTable("request_history");
            entity.Property<Guid>("Id").HasColumnName("id");
            entity.Property<DateTimeOffset>("CreatedAt").HasColumnName("created_at").IsRequired();
            entity.Property<string>("Method").HasColumnName("method").HasMaxLength(8).IsRequired();
            entity.Property<string>("Url").HasColumnName("url").HasMaxLength(2048).IsRequired();
            entity.Property<string>("RedactedQueryParametersJson").HasColumnName("redacted_query_parameters_json").IsRequired();
            entity.Property<string>("HeadersJson").HasColumnName("headers_json").IsRequired();
            entity.Property<string>("RedactedHeadersJson").HasColumnName("redacted_headers_json").IsRequired();
            entity.Property<string?>("Body").HasColumnName("body");
            entity.Property<string>("Failure").HasColumnName("failure").HasMaxLength(40).IsRequired();
            entity.Property<int?>("StatusCode").HasColumnName("status_code");
            entity.Property<double>("ElapsedMilliseconds").HasColumnName("elapsed_ms").IsRequired();
            entity.HasKey("Id");
            entity.HasIndex("CreatedAt", "Id");
        });
    }
}