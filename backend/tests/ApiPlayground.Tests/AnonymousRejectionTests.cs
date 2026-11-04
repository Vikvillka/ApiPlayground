using System.Net;
using Xunit;
using static ApiPlayground.Tests.ProtectedRouteHarness;

namespace ApiPlayground.Tests;

// Every protected route must reject requests without a valid session before any controller or outbound code runs.
public sealed class AnonymousRejectionTests
{
    private const string HistoryEntryPath = "/api/tracer/history/00000000-0000-0000-0000-000000000001";

    public static TheoryData<string, string, string?> ProtectedRoutes => new()
    {
        { "GET", "/api/auth/me", null },
        { "POST", "/api/auth/logout", "{}" },
        { "GET", "/api/tracer/collections", null },
        { "POST", "/api/tracer/collections/requests", SaveBody(null, "Alpha") },
        { "GET", "/api/tracer/history", null },
        { "GET", HistoryEntryPath, null },
        { "POST", "/api/tracer/get", "{\"url\":\"https://example.test/\",\"method\":\"GET\"}" },
        { "POST", "/api/tracer/export", "{}" },
    };

    [Theory]
    [MemberData(nameof(ProtectedRoutes))]
    public async Task Anonymous_request_is_rejected_with_401_problem(string method, string path, string? json)
    {
        await using var database = await CreateDatabaseAsync();
        await using var factory = CreateFactory(database);
        using var client = factory.CreateClient();

        var response = await client.SendAsync(CreateRequest(new HttpMethod(method), path, json));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Forged_session_cookie_is_rejected_on_collections_and_history()
    {
        await using var database = await CreateDatabaseAsync();
        await using var factory = CreateFactory(database);
        using var client = factory.CreateClient();
        var forged = $"{CookieName}=not-a-real-session";

        var collections = await client.SendAsync(WithCookie(CreateRequest(HttpMethod.Get, "/api/tracer/collections"), forged));
        var history = await client.SendAsync(WithCookie(CreateRequest(HttpMethod.Get, "/api/tracer/history"), forged));

        Assert.Equal(HttpStatusCode.Unauthorized, collections.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, history.StatusCode);
    }
}
