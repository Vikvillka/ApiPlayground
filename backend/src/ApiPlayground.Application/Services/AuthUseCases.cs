namespace ApiPlayground.Application.Auth;

public sealed record RegisterAccountResult(Guid? UserId, string? Error)
{
    public bool Succeeded => UserId is not null && Error is null;
}

public sealed record SignInResult(bool Succeeded, string? Error, Guid? UserId = null, string? Email = null);

public sealed class RegisterAccountUseCase(IAccountPort accounts)
{
    public const string DuplicateEmailMessage = "An account with this email already exists.";

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

        var registration = await accounts.RegisterAsync(email!.Trim(), password!, cancellationToken);
        if (registration.UserId is not null)
        {
            return new RegisterAccountResult(registration.UserId, null);
        }

        return registration.EmailTaken
            ? new RegisterAccountResult(null, DuplicateEmailMessage)
            : new RegisterAccountResult(null, "The account could not be created.");
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

        var session = await accounts.SignInAsync(email.Trim(), password, cancellationToken);
        return session is not null
            ? new SignInResult(true, null, session.UserId, session.Email)
            : new SignInResult(false, InvalidCredentialsMessage);
    }
}

public sealed class SignOutUseCase(IAccountPort accounts)
{
    public Task ExecuteAsync(CancellationToken cancellationToken = default) =>
        accounts.SignOutAsync(cancellationToken);
}

public sealed class CurrentUserUseCase(ICurrentUser currentUser, IAccountPort accounts)
{
    public async Task<AccountSession?> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        if (currentUser.UserId is not { } userId)
        {
            return null;
        }

        var email = await accounts.FindEmailAsync(userId, cancellationToken);
        return email is null ? null : new AccountSession(userId, email);
    }
}
