using System.Diagnostics.CodeAnalysis;

namespace ApiPlayground.Application.Outbound;

public static class RequestValidation
{
    private static readonly HashSet<string> SupportedMethods = new(StringComparer.Ordinal)
        { "GET", "POST", "PUT", "PATCH", "DELETE" };

    public static bool IsSupportedMethod(string? method) =>
        method is not null && SupportedMethods.Contains(method);

    public static bool TryCreateHttpUri(string? value, [NotNullWhen(true)] out Uri? uri)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out uri) ||
            uri.Scheme is not ("http" or "https") || uri.UserInfo.Length != 0 || uri.Fragment.Length != 0)
        {
            uri = null;
            return false;
        }

        return true;
    }

    public static bool HasDuplicateHeaderNames(IEnumerable<string> names)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return names.Any(name => !seen.Add(name));
    }
}