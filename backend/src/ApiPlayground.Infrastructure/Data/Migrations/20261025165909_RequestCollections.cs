using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApiPlayground.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class RequestCollections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "request_collections",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_request_collections", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "saved_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    collection_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    method = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    headers_json = table.Column<string>(type: "text", nullable: false),
                    redacted_headers_json = table.Column<string>(type: "text", nullable: false),
                    body = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_saved_requests", x => x.id);
                    table.ForeignKey(
                        name: "FK_saved_requests_request_collections_collection_id",
                        column: x => x.collection_id,
                        principalTable: "request_collections",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_request_collections_name",
                table: "request_collections",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_saved_requests_collection_id_created_at_id",
                table: "saved_requests",
                columns: new[] { "collection_id", "created_at", "id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "saved_requests");

            migrationBuilder.DropTable(
                name: "request_collections");
        }
    }
}
