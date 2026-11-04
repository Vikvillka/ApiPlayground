namespace ApiPlayground.Application.Auth;

public sealed record AccountRegistration(Guid? UserId, bool EmailTaken);

public sealed record AccountSession(Guid UserId, string Email);

public interface IAccountPort
{
    // Returns the new user id when the account is created. Otherwise UserId is null and EmailTaken says whether the email already has an account.
    Task<AccountRegistration> RegisterAsync(string email, string password, CancellationToken cancellationToken = default);

    // Returns the account only when the credentials match and the session cookie is issued; otherwise null.
    Task<AccountSession?> SignInAsync(string email, string password, CancellationToken cancellationToken = default);

    // Returns the account email for an existing user id; otherwise null.
    Task<string?> FindEmailAsync(Guid userId, CancellationToken cancellationToken = default);

    Task SignOutAsync(CancellationToken cancellationToken = default);
}

public interface ICurrentUser
{
    Guid? UserId { get; }
}
