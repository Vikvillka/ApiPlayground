using ApiPlayground.Api.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ApiPlayground.Tests.LegacyMigrationFixture;

[DbContext(typeof(PlatformDbContext))]
[Migration("20261018000000_InitialPlatformBaseline")]
public sealed class LegacyStoryOneInitialPlatformBaseline : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
