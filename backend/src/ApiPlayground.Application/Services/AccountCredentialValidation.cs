namespace ApiPlayground.Application.Auth;

public static class AccountCredentialValidation
{
    public const int MinimumPasswordLength = 8;
    public const int MaximumPasswordLength = 256;
    public const int MaximumEmailLength = 256;

    public static string? ValidateEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Trim().Length > MaximumEmailLength)
        {
            return "Enter a valid email address.";
        }

        var value = email.Trim();
        var at = value.IndexOf('@');
        if (at <= 0 || at == value.Length - 1 || value.IndexOf('@', at + 1) >= 0)
        {
            return "Enter a valid email address.";
        }

        return null;
    }

    public static string? ValidatePassword(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < MinimumPasswordLength)
        {
            return $"Password must be at least {MinimumPasswordLength} characters.";
        }

        if (password.Length > MaximumPasswordLength)
        {
            return $"Password must be at most {MaximumPasswordLength} characters.";
        }

        return null;
    }
}
