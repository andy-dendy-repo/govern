using System.Net;
using System.Net.Sockets;

namespace Govern.Utils.Networking;

/// <summary>
/// Builds rate-limiting partition keys from client IP addresses. One IPv6 subscriber usually owns a whole /64, so
/// partitioning by the full IPv6 address would give an attacker billions of separate limits.
/// </summary>
public static class IpPartitionKey
{
    /// <summary>The key used when the client address is unknown.</summary>
    public const string Unknown = "unknown";

    private const int Ipv6PrefixBytes = 8;

    /// <summary>
    /// Returns the partition key for <paramref name="address"/>: an IPv4 address as is (also when mapped to IPv6), an
    /// IPv6 address reduced to its /64 prefix, or <see cref="Unknown"/>.
    /// </summary>
    /// <param name="address">The client address, if known.</param>
    /// <returns>The partition key.</returns>
    public static string From(IPAddress? address)
    {
        if (address is null)
        {
            return Unknown;
        }

        if (address.IsIPv4MappedToIPv6)
        {
            return address.MapToIPv4().ToString();
        }

        if (address.AddressFamily != AddressFamily.InterNetworkV6)
        {
            return address.ToString();
        }

        Span<byte> bytes = stackalloc byte[16];
        address.TryWriteBytes(bytes, out _);
        bytes[Ipv6PrefixBytes..].Clear();
        return $"{new IPAddress(bytes)}/64";
    }
}
