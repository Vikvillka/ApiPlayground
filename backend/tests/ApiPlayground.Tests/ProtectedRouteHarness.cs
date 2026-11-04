using System.Net;
using System.Text;
using System.Text.Json;
using ApiPlayground.Api.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ApiPlayground.Tests;

// Shared PostgreSQL-backed HTTP harness for the anonymous-rejection and two-user isolation suites.
// Every entry point throws when APIPLAYGROUND_TEST_ADMIN_CONNECTION is missing; nothing here skips.
internal static class ProtectedRouteHarness
{
    internal const string CookieName = "ApiPlayground.Session";
    internal const string Password = "correct-horse-battery";

    internal sealed record SignedInUser(Guid Id, string Cookie);

    internal static async Task<Story9DisposableDatabase> CreateDatabaseAsync()
    {
        var adminConnection = Story9DisposableDatabase.GetAdminConnectionString()
            ?? throw new InvalidOperationException("APIPLAYGROUND_TEST_ADMIN_CONNECTION is required for protected-route tests; tests never skip.");
        var database = await Story9DisposableDatabase.CreateAsync(adminConnection);
        try
        {
            var options = new DbContextOptionsBuilder<PlatformDbContext>()
                .UseNpgsql(database.AppConnectionString, postgres => postgres.MigrationsAssembly("ApiPlayground.Infrastructure"))
                .Options;
            await using var context = new PlatformDbContext(options);
            await context.Database.MigrateAsync();
            return database;
        }
        catch
        {
            await database.DisposeAsync();
            throw;
        }
    }

    internal static WebApplicationFactory<Program> CreateFactory(Story9DisposableDatabase database) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseSetting("ConnectionStrings:Postgres", database.AppConnectionString));

    internal static HttpRequestMessage CreateRequest(HttpMethod method, string path, string? json = null)
    {
        var request = new HttpRequestMessage(method, path)
        {
            Content = json is null ? null : new StringContent(json, Encoding.UTF8, "application/json"),
        };
        request.Headers.Host = "localhost:5000";
        request.Headers.Add("Origin", "http://127.0.0.1:5173");
        request.Headers.Add("X-ApiPlayground-Request", "1");
        return request;
    }

    internal static HttpRequestMessage WithCookie(HttpRequestMessage request, string cookie)
    {
        request.Headers.Add("Cookie", cookie);
        return request;
    }

    internal static string Credentials(string email, string password) =>
        JsonSerializer.Serialize(new { email, password });

    internal static async Task<SignedInUser> RegisterAndLogInAsync(HttpClient client, string email)
    {
        var credentials = Credentials(email, Password);

        var register = await client.SendAsync(CreateRequest(HttpMethod.Post, "/api/auth/register", credentials));
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        var login = await client.SendAsync(CreateRequest(HttpMethod.Post, "/api/auth/login", credentials));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var id = Field(await ReadJsonAsync(login), "id").GetGuid();
        return new SignedInUser(id, ReadSessionCookie(login));
    }

    internal static string SaveBody(Guid? collectionId, string? newCollectionName) => JsonSerializer.Serialize(new
    {
        collectionId,
        newCollectionName,
        title = "GET /items",
        method = "GET",
        url = "https://example.test/items",
        headers = new Dictionary<string, string>(),
    });

    internal static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    // Response field names are matched case-insensitively so the suite does not depend on the serializer's naming policy.
    internal static JsonElement Field(JsonElement element, string name)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                return property.Value;
        }

        throw new InvalidOperationException($"The response has no '{name}' field.");
    }

    internal static string ReadSessionCookie(HttpResponseMessage response)
    {
        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var values), "Login must set a session cookie.");
        var cookie = values!.FirstOrDefault(value => value.StartsWith(CookieName + "=", StringComparison.Ordinal));
        Assert.NotNull(cookie);
        return cookie!.Split(';')[0];
    }
}
