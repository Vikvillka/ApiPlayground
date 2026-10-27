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
            : IsPublicAddress(address);

    internal bool AllowsAddress(Uri uri, IPAddress address) =>
        _testEndpoint is not null
            ? IsTestEndpoint(uri) && address.Equals(_testEndpoint.Address)
            : IsPublicAddress(address);

    internal static bool IsPublicAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
            return IsPublicAddress(address.MapToIPv4());

        var bytes = address.GetAddressBytes();
        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
        {
            // Public IPv6 unicast is currently allocated from 2000::/3. Exclude conservative
            // special-use ranges, including transition and documentation prefixes.
            if ((bytes[0] & 0xE0) != 0x20)
                return false;
            if (bytes[0] == 0x20 && bytes[1] == 0x01 && bytes[2] <= 0x01 || // 2001::/23 special-purpose allocation block
                bytes[0] == 0x20 && bytes[1] == 0x01 && bytes[2] == 0x00 && bytes[3] == 0x02 || // 2001:2::/48 benchmarking
                bytes[0] == 0x20 && bytes[1] == 0x01 && bytes[2] == 0x00 && bytes[3] == 0x03 || // 2001:3::/32 AMT
                bytes[0] == 0x20 && bytes[1] == 0x01 && bytes[2] == 0x00 && bytes[3] == 0x04 && bytes[4] == 0x01 && bytes[5] == 0x12 || // 2001:4:112::/48 AS112
                bytes[0] == 0x20 && bytes[1] == 0x01 && bytes[2] == 0x00 && (bytes[3] & 0xF0) == 0x10 || // 2001:10::/28 ORCHIDv1
                bytes[0] == 0x20 && bytes[1] == 0x01 && bytes[2] == 0x20 && (bytes[3] & 0xF0) == 0 || // 2001:20::/28 ORCHIDv2
                bytes[0] == 0x20 && bytes[1] == 0x01 && bytes[2] == 0x30 && (bytes[3] & 0xF0) == 0 || // 2001:30::/28 special-purpose
                bytes[0] == 0x20 && bytes[1] == 0x02 || // 2002::/16 6to4
                bytes[0] == 0x20 && bytes[1] == 0x01 && bytes[2] == 0x0D && bytes[3] == 0xB8 || // 2001:db8::/32 documentation
                bytes[0] == 0x3F && bytes[1] == 0xFF && (bytes[2] & 0xF0) == 0) // 3fff::/20 documentation
                return false;
            return true;
        }

        if (address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            return false;

        var a = bytes[0];
        var b = bytes[1];
        var c = bytes[2];
        return !(a == 0 || a == 10 || a == 127 ||
            a == 100 && b is >= 64 and <= 127 ||
            a == 169 && b == 254 ||
            a == 172 && b is >= 16 and <= 31 ||
            a == 192 && (b == 0 || b == 168) ||
            a == 192 && b == 31 && c == 196 ||
            a == 192 && b == 52 && c == 193 ||
            a == 192 && b == 88 && c == 99 ||
            a == 192 && b == 175 && c == 48 ||
            a == 198 && (b is 18 or 19 || b == 51 && c == 100) ||
            a == 203 && b == 0 && c == 113 ||
            a >= 224);
    }
}
