using System.Reflection;
using System.Text.RegularExpressions;

namespace Govern.ArchitectureTests;

/// <summary>
/// The client duplicates the Api contracts on purpose (it references no server project), so this guards against drift:
/// a client DTO in <c>Features/[Feature]/Models</c> named like a public <c>Govern.Application</c> type may only have
/// properties that the server type has, with the same shape.
/// </summary>
public partial class ClientContractTests
{
    private static readonly Assembly ClientAssembly = typeof(Govern.Client.DependencyInjection).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(Govern.Application.DependencyInjection).Assembly;

    // Client types whose names differ from the contract they mirror: the token pair and the refresh body in Auth/.
    public static TheoryData<Type, Type> ExplicitPairs => new()
    {
        { typeof(Govern.Client.Auth.TokenPair), typeof(Govern.Application.Users.Contracts.SignInResponse) },
        { typeof(Govern.Client.Auth.TokenPair), typeof(Govern.Application.Users.Contracts.RefreshTokenResponse) },
        {
            typeof(Govern.Client.Auth.AuthSession).GetNestedType("RefreshTokenRequest", BindingFlags.NonPublic)!,
            typeof(Govern.Application.Users.Contracts.RefreshTokenRequest)
        },
    };

    [Fact]
    public void ClientDtos_MatchTheServerContractsTheyAreNamedAfter()
    {
        var serverTypes = ApplicationAssembly.GetExportedTypes()
            .GroupBy(static type => type.Name)
            .ToDictionary(static group => group.Key, static group => group.First(), StringComparer.Ordinal);
        var pairs = ClientAssembly.GetExportedTypes()
            .Where(static type => ClientModelsNamespaceRegex().IsMatch(type.Namespace ?? string.Empty))
            .Where(type => serverTypes.ContainsKey(type.Name))
            .Select(type => (Client: type, Server: serverTypes[type.Name]))
            .ToList();

        var violations = pairs.SelectMany(static pair => Mismatches(pair.Client, pair.Server)).ToList();

        pairs.Select(static pair => pair.Client.Name).ShouldContain("SignInRequest", "the Users DTOs are checked");
        pairs.Select(static pair => pair.Client.Name).ShouldContain("GetGameResponse", "the Games DTOs are checked");
        violations.ShouldBeEmpty("a client DTO must mirror the Application contract it is named after (drifted DTOs break silently at runtime)");
    }

    [Theory]
    [MemberData(nameof(ExplicitPairs))]
    public void ClientAuthTypes_MatchTheServerContractsTheyMirror(Type client, Type server)
    {
        client.ShouldNotBeNull("the mirrored client type must exist");

        Mismatches(client, server).ShouldBeEmpty();
        PublicProperties(client).Select(static property => property.Name).Order(StringComparer.Ordinal)
            .ShouldBe(PublicProperties(server).Select(static property => property.Name).Order(StringComparer.Ordinal),
                "the auth types must carry every field of the contract (a missing token field breaks sign-in or refresh)");
    }

    private static IEnumerable<string> Mismatches(Type client, Type server)
    {
        if (client.IsEnum || server.IsEnum)
        {
            return client.IsEnum && server.IsEnum
                ? Enum.GetNames(client).Except(Enum.GetNames(server)).Select(name => $"{client.FullName}.{name}: no such member on {server.FullName}")
                : [$"{client.FullName}: only one side is an enum ({server.FullName})"];
        }

        var serverProperties = PublicProperties(server).ToDictionary(static property => property.Name, StringComparer.Ordinal);
        return PublicProperties(client).SelectMany(property => serverProperties.TryGetValue(property.Name, out var match)
            ? SameShape(property.PropertyType, match.PropertyType)
                ? []
                : [$"{client.FullName}.{property.Name}: {property.PropertyType.Name} vs {match.PropertyType.Name} on {server.FullName}"]
            : new[] { $"{client.FullName}.{property.Name}: no such property on {server.FullName}" });
    }

    // Framework types must be identical (nullable and collection wrappers unwrapped); DTO and enum types match by name,
    // and each of those is checked on its own when both sides define it.
    private static bool SameShape(Type client, Type server)
    {
        client = Nullable.GetUnderlyingType(client) ?? client;
        server = Nullable.GetUnderlyingType(server) ?? server;

        if (ElementType(client) is { } clientElement && ElementType(server) is { } serverElement)
        {
            return SameShape(clientElement, serverElement);
        }

        return IsGovernType(client) || IsGovernType(server) ? client.Name == server.Name : client == server;
    }

    private static Type? ElementType(Type type) =>
        type == typeof(string) ? null
        : type.IsArray ? type.GetElementType()
        : type.GetInterfaces().Append(type)
            .FirstOrDefault(static candidate => candidate.IsGenericType && candidate.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            ?.GetGenericArguments()[0];

    private static bool IsGovernType(Type type) => type.Namespace?.StartsWith("Govern.", StringComparison.Ordinal) == true;

    private static IEnumerable<PropertyInfo> PublicProperties(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(static property => property.GetIndexParameters().Length == 0);

    [GeneratedRegex(@"^Govern\.Client\.Features\.\w+\.Models$")]
    private static partial Regex ClientModelsNamespaceRegex();
}
