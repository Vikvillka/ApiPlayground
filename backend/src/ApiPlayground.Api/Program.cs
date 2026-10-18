using ApiPlayground.Api.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? "Host=localhost;Port=5432;Database=apiplayground;Username=apiplayground;Password=local-only-change-me";

builder.Services.AddDbContext<PlatformDbContext>(options => options.UseNpgsql(connectionString));

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.Run();

public partial class Program { }