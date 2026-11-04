using ApiPlayground.Application.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;

namespace ApiPlayground.Infrastructure.Auth;

internal sealed class IdentityAccountPort(
    UserManager<IdentityUser<Guid>> users,
    SignInManager<IdentityUser<Guid>> signIn) : IAccountPort
{
    public async Task<Guid?> RegisterAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        var user = new IdentityUser<Guid> { Id = Guid.NewGuid(), UserName = email, Email = email };
        var result = await users.CreateAsync(user, password);
        return result.Succeeded ? user.Id : null;
    }

    public async Task<Guid?> SignInAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        var user = await users.FindByEmailAsync(email);
        if (user is null)
        {
            // Hash anyway so an unknown email does not answer noticeably faster than a wrong password.
            _ = users.PasswordHasher.HashPassword(new IdentityUser<Guid>(), password);
            return null;
        }

        var result = await signIn.PasswordSignInAsync(user, password, isPersistent: true, lockoutOnFailure: false);
        return result.Succeeded ? user.Id : null;
    }

    public async Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        // Rotating the stamp revokes every copy of the session cookie, not only the one held by this browser.
        var user = await users.GetUserAsync(signIn.Context.User);
        if (user is not null)
        {
            await users.UpdateSecurityStampAsync(user);
        }

        await signIn.SignOutAsync();
    }
}

internal sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public Guid? UserId
    {
        get
        {
            var principal = accessor.HttpContext?.User;
            if (principal?.Identity?.IsAuthenticated != true)
            {
                return null;
            }

            var value = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(value, out var id) ? id : null;
        }
    }
}
