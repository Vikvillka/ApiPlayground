using System.Net;

namespace ApiPlayground.Infrastructure.Outbound;

public sealed class OutboundPolicy
{
    private readonly IPEndPoint? _testEndpoint;
    private readonly string? _testHost;

    private OutboundPolicy(IPEndPoint? testEndpoint, string? testHost = null)
    {
        _testEndpoint = testEndpoint;
        _testHost = testHost;
    }

    internal bool IsValid => _testEndpoint is null ||
        IPAddress.IsLoopback(_testEndpoint.Address) && _testEndpoint.Port is > 32767 and <= 65535;
    internal IPAddress? TestAddress => _testEndpoint?.Address;

    public static TimeSpan ConnectTimeout { get; } = TimeSpan.FromSeconds(5);
    public static TimeSpan TotalTimeout { get; } = TimeSpan.FromSeconds(30);
    public static int MaximumResponseBytes { get; } = 1_048_576;

    public static OutboundPolicy PublicInternetOnly() => new(null);

    /// <summary>Creates a test-only policy for one exact ephemeral loopback HTTP endpoint.</summary>
    internal static OutboundPolicy ForTestOnlyLoopbackEndpoint(Uri endpoint, string? testHostname = null)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        if (!endpoint.IsAbsoluteUri || endpoint.Scheme != Uri.UriSchemeHttp ||
            !IPAddress.TryParse(endpoint.Host, out var address) || !IPAddress.IsLoopback(address) ||
            endpoint.Port is <= 32767 or > 65535 || endpoint.AbsolutePath != "/" || endpoint.UserInfo.Length != 0 || endpoint.Query.Length != 0 || endpoint.Fragment.Length != 0)
        {
            throw new ArgumentException("The test policy requires an exact HTTP loopback origin on a dynamic/private port with no path or credentials.", nameof(endpoint));
        }

        if (testHostname is not null && (Uri.CheckHostName(testHostname) != UriHostNameType.Dns ||
            !testHostname.Contains('.', StringComparison.Ordinal) ||
            string.Equals(testHostname, "localhost", StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("A test hostname must be one explicit non-local DNS name.", nameof(testHostname));

        return new OutboundPolicy(new IPEndPoint(address, endpoint.Port), testHostname);
    }

    internal bool ClaimsTestHost(Uri uri) => _testEndpoint is not null && _testHost is not null &&
        string.Equals(uri.DnsSafeHost, _testHost, StringComparison.OrdinalIgnoreCase);

    internal bool IsTestEndpoint(Uri uri) => _testEndpoint is not null &&
        uri.Scheme == Uri.UriSchemeHttp && uri.Port == _testEndpoint.Port &&
        (IPAddress.TryParse(uri.Host, out var address) && address.Equals(_testEndpoint.Address) && _testHost is null ||
         ClaimsTestHost(uri));

    internal bool AllowsAddress(IPAddress address) =>
        _testEndpoint is not null
            ? address.Equals(_testEndpoint.Address)
            : NetworkAddressClassifier.IsPublic(address);

    internal bool AllowsAddress(Uri uri, IPAddress address) =>
        _testEndpoint is not null
            ? IsTestEndpoint(uri) && address.Equals(_testEndpoint.Address)
            : NetworkAddressClassifier.IsPublic(address);
}
