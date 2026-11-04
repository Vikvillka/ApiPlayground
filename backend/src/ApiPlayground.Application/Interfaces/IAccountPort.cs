namespace ApiPlayground.Application.Auth;

public sealed record AccountRegistration(Guid? UserId, bool EmailTaken);

public interface IAccountPort
{
    // Returns the new user id when the account is created. Otherwise UserId is null and EmailTaken says whether the email already has an account.
    Task<AccountRegistration> RegisterAsync(string email, string password, CancellationToken cancellationToken = default);

    // Returns the user id only when the credentials match an account and the session cookie is issued; otherwise null.
    Task<Guid?> SignInAsync(string email, string password, CancellationToken cancellationToken = default);

    Task SignOutAsync(CancellationToken cancellationToken = default);
}

public interface ICurrentUser
{
    Guid? UserId { get; }
}
