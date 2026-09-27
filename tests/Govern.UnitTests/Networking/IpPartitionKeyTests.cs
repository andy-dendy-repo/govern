using System.Net;
using Govern.Utils.Networking;

namespace Govern.UnitTests.Networking;

public class IpPartitionKeyTests
{
    [Fact]
    public void From_Null_ReturnsUnknown()
    {
        IpPartitionKey.From(null).ShouldBe(IpPartitionKey.Unknown);
    }

    [Fact]
    public void From_IPv4_ReturnsAddress()
    {
        IpPartitionKey.From(IPAddress.Parse("203.0.113.7")).ShouldBe("203.0.113.7");
    }

    [Fact]
    public void From_IPv4MappedToIPv6_ReturnsIPv4Address()
    {
        IpPartitionKey.From(IPAddress.Parse("::ffff:203.0.113.7")).ShouldBe("203.0.113.7");
    }

    [Fact]
    public void From_IPv6_ReturnsSlash64Prefix()
    {
        IpPartitionKey.From(IPAddress.Parse("2001:db8:1:2:aaaa:bbbb:cccc:dddd")).ShouldBe("2001:db8:1:2::/64");
    }

    [Fact]
    public void From_IPv6InSameSlash64_ReturnsSameKey()
    {
        var first = IpPartitionKey.From(IPAddress.Parse("2001:db8:1:2::1"));
        var second = IpPartitionKey.From(IPAddress.Parse("2001:db8:1:2:ffff:ffff:ffff:ffff%3"));

        second.ShouldBe(first);
    }

    [Fact]
    public void From_IPv6InDifferentSlash64_ReturnsDifferentKeys()
    {
        var first = IpPartitionKey.From(IPAddress.Parse("2001:db8:1:2::1"));
        var second = IpPartitionKey.From(IPAddress.Parse("2001:db8:1:3::1"));

        second.ShouldNotBe(first);
    }
}
