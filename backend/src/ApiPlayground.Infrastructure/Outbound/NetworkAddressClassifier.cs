using System.Net;
using System.Net.Sockets;

namespace ApiPlayground.Infrastructure.Outbound;

internal static class NetworkAddressClassifier
{
    internal static bool IsPublic(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
            return IsPublic(address.MapToIPv4());

        var bytes = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetworkV6)
            return IsPublicIPv6(bytes);

        if (address.AddressFamily != AddressFamily.InterNetwork)
            return false;

        return IsPublicIPv4(bytes);
    }

    private static bool IsPublicIPv6(byte[] bytes)
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
        {
            return false;
        }

        return true;
    }

    private static bool IsPublicIPv4(byte[] bytes)
    {
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
