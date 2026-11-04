using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApiPlayground.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class RequestOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Ownerless rows cannot be attributed to a user: clear data tables before adding required owner foreign keys.
            migrationBuilder.Sql("DELETE FROM saved_requests;");
            migrationBuilder.Sql("DELETE FROM request_collections;");
            migrationBuilder.Sql("DELETE FROM request_history;");

            migrationBuilder.DropIndex(
                name: "IX_request_collections_name",
                table: "request_collections");

            migrationBuilder.AddColumn<Guid>(
                name: "owner_id",
                table: "saved_requests",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "owner_id",
                table: "request_history",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "owner_id",
                table: "request_collections",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_saved_requests_owner_id",
                table: "saved_requests",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "IX_request_history_owner_id",
                table: "request_history",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "IX_request_collections_owner_id_name",
                table: "request_collections",
                columns: new[] { "owner_id", "name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_request_collections_AspNetUsers_owner_id",
                table: "request_collections",
                column: "owner_id",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_request_history_AspNetUsers_owner_id",
                table: "request_history",
                column: "owner_id",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_saved_requests_AspNetUsers_owner_id",
                table: "saved_requests",
                column: "owner_id",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_request_collections_AspNetUsers_owner_id",
                table: "request_collections");

            migrationBuilder.DropForeignKey(
                name: "FK_request_history_AspNetUsers_owner_id",
                table: "request_history");

            migrationBuilder.DropForeignKey(
                name: "FK_saved_requests_AspNetUsers_owner_id",
                table: "saved_requests");

            migrationBuilder.DropIndex(
                name: "IX_saved_requests_owner_id",
                table: "saved_requests");

            migrationBuilder.DropIndex(
                name: "IX_request_history_owner_id",
                table: "request_history");

            migrationBuilder.DropIndex(
                name: "IX_request_collections_owner_id_name",
                table: "request_collections");

            migrationBuilder.DropColumn(
                name: "owner_id",
                table: "saved_requests");

            migrationBuilder.DropColumn(
                name: "owner_id",
                table: "request_history");

            migrationBuilder.DropColumn(
                name: "owner_id",
                table: "request_collections");

            migrationBuilder.CreateIndex(
                name: "IX_request_collections_name",
                table: "request_collections",
                column: "name",
                unique: true);
        }
    }
}
