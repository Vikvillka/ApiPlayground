namespace ApiPlayground.Application.Auth;

public sealed record RegisterAccountResult(Guid? UserId, string? Error)
{
    public bool Succeeded => UserId is not null && Error is null;
}

public sealed record SignInResult(bool Succeeded, string? Error, Guid? UserId = null);

public sealed class RegisterAccountUseCase(IAccountPort accounts)
{
    public async Task<RegisterAccountResult> ExecuteAsync(string? email, string? password, CancellationToken cancellationToken = default)
    {
        var emailError = AccountCredentialValidation.ValidateEmail(email);
        if (emailError is not null)
        {
            return new RegisterAccountResult(null, emailError);
        }

        var passwordError = AccountCredentialValidation.ValidatePassword(password);
        if (passwordError is not null)
        {
            return new RegisterAccountResult(null, passwordError);
        }

        var userId = await accounts.RegisterAsync(email!.Trim(), password!, cancellationToken);
        return userId is null
            ? new RegisterAccountResult(null, "The account could not be created.")
            : new RegisterAccountResult(userId, null);
    }
}

public sealed class SignInUseCase(IAccountPort accounts)
{
    // One message for unknown email and wrong password, so the response does not reveal which accounts exist.
    public const string InvalidCredentialsMessage = "Invalid email or password.";

    public async Task<SignInResult> ExecuteAsync(string? email, string? password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password))
        {
            return new SignInResult(false, "Enter an email and password.");
        }

        var userId = await accounts.SignInAsync(email.Trim(), password, cancellationToken);
        return userId is not null
            ? new SignInResult(true, null, userId)
            : new SignInResult(false, InvalidCredentialsMessage);
    }
}

public sealed class SignOutUseCase(IAccountPort accounts)
{
    public Task ExecuteAsync(CancellationToken cancellationToken = default) =>
        accounts.SignOutAsync(cancellationToken);
}

public sealed class CurrentUserUseCase(ICurrentUser currentUser)
{
    public Guid? Execute() => currentUser.UserId;
}
