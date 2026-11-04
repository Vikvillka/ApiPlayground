using System.Net;
using ApiPlayground.Api.Data;
using ApiPlayground.Application.Outbound;
using ApiPlayground.Infrastructure.Outbound;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Xunit;
using static ApiPlayground.Tests.ProtectedRouteHarness;

namespace ApiPlayground.Tests;

// Two-user isolation over HTTP: each user signs in with their own session cookie against the real pipeline.
public sealed class TwoUserHttpIsolationTests
{
    [Fact]
    public async Task User_b_cannot_list_or_save_into_user_a_collections_over_http()
    {
        await using var database = await CreateDatabaseAsync();
        await using var factory = CreateFactory(database);
        // The cookie jar is off so each request carries only the session cookie set for that user.
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var alice = await RegisterAndLogInAsync(client, "alice-collections@example.test");
        var bob = await RegisterAndLogInAsync(client, "bob-collections@example.test");

        var saved = await client.SendAsync(WithCookie(
            CreateRequest(HttpMethod.Post, "/api/tracer/collections/requests", SaveBody(null, "Alpha")), alice.Cookie));
        Assert.Equal(HttpStatusCode.Created, saved.StatusCode);
        var collectionId = Field(await ReadJsonAsync(saved), "collectionId").GetGuid();

        var bobList = await client.SendAsync(WithCookie(CreateRequest(HttpMethod.Get, "/api/tracer/collections"), bob.Cookie));
        Assert.Equal(HttpStatusCode.OK, bobList.StatusCode);
        Assert.Equal(0, (await ReadJsonAsync(bobList)).GetArrayLength());

        var intrusion = await client.SendAsync(WithCookie(
            CreateRequest(HttpMethod.Post, "/api/tracer/collections/requests", SaveBody(collectionId, null)), bob.Cookie));
        Assert.Equal(HttpStatusCode.NotFound, intrusion.StatusCode);

        var aliceList = await client.SendAsync(WithCookie(CreateRequest(HttpMethod.Get, "/api/tracer/collections"), alice.Cookie));
        var collections = await ReadJsonAsync(aliceList);
        Assert.Equal(1, collections.GetArrayLength());
        Assert.Equal(1, Field(collections.EnumerateArray().First(), "requests").GetArrayLength());
    }

    [Fact]
    public async Task User_b_cannot_list_or_read_user_a_history_over_http()
    {
        await using var database = await CreateDatabaseAsync();
        await using var factory = CreateFactory(database);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var alice = await RegisterAndLogInAsync(client, "alice-history@example.test");
        var bob = await RegisterAndLogInAsync(client, "bob-history@example.test");

        // History rows are written through the repository, as the existing isolation tests do, because
        // dispatching a real request would need an outbound target.
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseNpgsql(database.AppConnectionString, postgres => postgres.MigrationsAssembly("ApiPlayground.Infrastructure"))
            .Options;
        var entry = NewHistorySnapshot();
        await new PostgresRequestHistoryRepository(new PlatformDbContext(options)).SaveAsync(alice.Id, entry);

        var aliceList = await client.SendAsync(WithCookie(CreateRequest(HttpMethod.Get, "/api/tracer/history"), alice.Cookie));
        Assert.Equal(1, (await ReadJsonAsync(aliceList)).GetArrayLength());

        var aliceRead = await client.SendAsync(WithCookie(
            CreateRequest(HttpMethod.Get, $"/api/tracer/history/{entry.Id}"), alice.Cookie));
        Assert.Equal(HttpStatusCode.OK, aliceRead.StatusCode);

        var bobList = await client.SendAsync(WithCookie(CreateRequest(HttpMethod.Get, "/api/tracer/history"), bob.Cookie));
        Assert.Equal(HttpStatusCode.OK, bobList.StatusCode);
        Assert.Equal(0, (await ReadJsonAsync(bobList)).GetArrayLength());

        var bobRead = await client.SendAsync(WithCookie(
            CreateRequest(HttpMethod.Get, $"/api/tracer/history/{entry.Id}"), bob.Cookie));
        Assert.Equal(HttpStatusCode.NotFound, bobRead.StatusCode);
    }

    private static RequestHistorySnapshot NewHistorySnapshot() => RequestHistoryRedaction.Create(
        new OutboundRequest(new Uri("http://127.0.0.1:49152/history"), "GET"),
        new OutboundResult(OutboundFailure.None, 200, TimeSpan.FromMilliseconds(1),
            new Dictionary<string, IReadOnlyList<string>>(), "ok"u8.ToArray(), DispatchStarted: true));
}
