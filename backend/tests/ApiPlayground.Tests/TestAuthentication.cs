using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ApiPlayground.Tests;

// Test-only default scheme for tracer tests that exercise guard, outbound, and persistence behavior rather than login.
// It replaces the cookie session as the default scheme in those hosts only. Real cookie login is covered by AuthEndpointTests.
internal static class TestAuthentication
{
    internal const string Scheme = "TestAlwaysAuthenticated";

    internal static void Configure(IServiceCollection services)
    {
        services.AddAuthentication(options =>
            {
                options.DefaultScheme = Scheme;
                options.DefaultAuthenticateScheme = Scheme;
                options.DefaultChallengeScheme = Scheme;
                options.DefaultForbidScheme = Scheme;
            })
            .AddScheme<AuthenticationSchemeOptions, AlwaysAuthenticatedHandler>(Scheme, _ => { });
    }

    private sealed class AlwaysAuthenticatedHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var identity = new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "00000000-0000-0000-0000-000000000001")],
                TestAuthentication.Scheme);
            var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), TestAuthentication.Scheme);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
