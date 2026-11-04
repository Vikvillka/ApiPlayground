namespace ApiPlayground.Application.Auth;

public interface IAccountPort
{
    // Returns the new user id, or null when the account could not be created (for example, a duplicate email).
    Task<Guid?> RegisterAsync(string email, string password, CancellationToken cancellationToken = default);

    // Returns the user id only when the credentials match an account and the session cookie is issued; otherwise null.
    Task<Guid?> SignInAsync(string email, string password, CancellationToken cancellationToken = default);

    Task SignOutAsync(CancellationToken cancellationToken = default);
}

public interface ICurrentUser
{
    Guid? UserId { get; }
}
