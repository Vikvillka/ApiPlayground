using System.Net;
using Xunit;
using static ApiPlayground.Tests.ProtectedRouteHarness;

namespace ApiPlayground.Tests;

public sealed class AuthEndpointTests
{
    private const string Email = "user@example.test";

    [Fact]
    public async Task Register_login_me_and_logout_follow_the_cookie_session()
    {
        await using var database = await CreateDatabaseAsync();
        await using var factory = CreateFactory(database);
        using var client = factory.CreateClient();

        var register = await client.SendAsync(CreateRequest(HttpMethod.Post, "/api/auth/register", Credentials(Email, Password)));
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        var anonymousMe = await client.SendAsync(CreateRequest(HttpMethod.Get, "/api/auth/me"));
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousMe.StatusCode);
        Assert.Equal("application/problem+json", anonymousMe.Content.Headers.ContentType?.MediaType);

        var login = await client.SendAsync(CreateRequest(HttpMethod.Post, "/api/auth/login", Credentials(Email, Password)));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var loginBody = await login.Content.ReadAsStringAsync();
        Assert.DoesNotContain("null", loginBody, StringComparison.Ordinal);
        var cookie = ReadSessionCookie(login);
        AssertSessionCookieAttributes(login);

        var me = await client.SendAsync(WithCookie(CreateRequest(HttpMethod.Get, "/api/auth/me"), cookie));
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var meBody = await me.Content.ReadAsStringAsync();
        Assert.Contains(Email, meBody, StringComparison.Ordinal);

        var logout = await client.SendAsync(WithCookie(CreateRequest(HttpMethod.Post, "/api/auth/logout", "{}"), cookie));
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.True(logout.Headers.TryGetValues("Set-Cookie", out var clearedValues), "Logout must clear the session cookie.");
        Assert.Contains(clearedValues!, value => value.StartsWith(CookieName + "=;", StringComparison.Ordinal));

        var afterLogout = await client.SendAsync(WithCookie(CreateRequest(HttpMethod.Get, "/api/auth/me"), cookie));
        Assert.Equal(HttpStatusCode.Unauthorized, afterLogout.StatusCode);
    }

    [Fact]
    public async Task Wrong_password_and_unknown_email_return_the_same_generic_message()
    {
        await using var database = await CreateDatabaseAsync();
        await using var factory = CreateFactory(database);
        using var client = factory.CreateClient();

        await client.SendAsync(CreateRequest(HttpMethod.Post, "/api/auth/register", Credentials(Email, Password)));

        var wrongPassword = await client.SendAsync(CreateRequest(HttpMethod.Post, "/api/auth/login", Credentials(Email, "wrong-password-value")));
        var unknownEmail = await client.SendAsync(CreateRequest(HttpMethod.Post, "/api/auth/login", Credentials("nobody@example.test", Password)));

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);
        var wrongBody = await wrongPassword.Content.ReadAsStringAsync();
        var unknownBody = await unknownEmail.Content.ReadAsStringAsync();
        Assert.Contains("Invalid email or password.", wrongBody, StringComparison.Ordinal);
        Assert.Contains("Invalid email or password.", unknownBody, StringComparison.Ordinal);
        Assert.False(wrongPassword.Headers.Contains("Set-Cookie"));
        Assert.False(unknownEmail.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task Register_rejects_invalid_input_and_duplicate_email()
    {
        await using var database = await CreateDatabaseAsync();
        await using var factory = CreateFactory(database);
        using var client = factory.CreateClient();

        var invalidEmail = await client.SendAsync(CreateRequest(HttpMethod.Post, "/api/auth/register", Credentials("not-an-email", Password)));
        var shortPassword = await client.SendAsync(CreateRequest(HttpMethod.Post, "/api/auth/register", Credentials(Email, "short")));
        Assert.Equal(HttpStatusCode.BadRequest, invalidEmail.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, shortPassword.StatusCode);

        var first = await client.SendAsync(CreateRequest(HttpMethod.Post, "/api/auth/register", Credentials(Email, Password)));
        var duplicate = await client.SendAsync(CreateRequest(HttpMethod.Post, "/api/auth/register", Credentials(Email, Password)));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        var duplicateBody = await duplicate.Content.ReadAsStringAsync();
        Assert.Contains("An account with this email already exists.", duplicateBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Protected_api_route_requires_a_session_cookie()
    {
        await using var database = await CreateDatabaseAsync();
        await using var factory = CreateFactory(database);
        using var client = factory.CreateClient();

        var anonymous = await client.SendAsync(CreateRequest(HttpMethod.Get, "/api/tracer/history"));
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        var health = await client.SendAsync(CreateRequest(HttpMethod.Get, "/health"));
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
    }

    [Fact]
    public async Task Api_request_with_foreign_origin_is_rejected_by_guard()
    {
        await using var database = await CreateDatabaseAsync();
        await using var factory = CreateFactory(database);
        using var client = factory.CreateClient();

        var request = CreateRequest(HttpMethod.Get, "/api/auth/me");
        request.Headers.Remove("Origin");
        request.Headers.Add("Origin", "http://evil.example.test");
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Api_request_without_guard_headers_is_rejected_before_auth()
    {
        await using var database = await CreateDatabaseAsync();
        await using var factory = CreateFactory(database);
        using var client = factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Host = "localhost:5000";
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static void AssertSessionCookieAttributes(HttpResponseMessage response)
    {
        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var values), "Login must set a session cookie.");
        var cookie = values!.Single(value => value.StartsWith(CookieName + "=", StringComparison.Ordinal));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
    }

}
