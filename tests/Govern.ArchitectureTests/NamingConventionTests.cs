using System.Reflection;
using ArchUnitNET.Fluent.Syntax.Elements.Types;
using ArchUnitNET.xUnitV3;
using Govern.ArchitectureTests.Support;
using Govern.Domain.EntityBase;
using Govern.Utils.Mediator.Abstractions;
using Microsoft.AspNetCore.Components;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Govern.ArchitectureTests;

public class NamingConventionTests
{
    public static TheoryData<string, string> SuffixByFolder => new()
    {
        { "Options", "Options" },
        { "Exceptions", "Exception" },
        { "Extensions", "Extensions" },
        { "Attributes", "Attribute" },
        { "Services", "Service" },
        { "EventHandlers", "EventHandler" },
        { "Repositories", "Repository" },
        { "Middlewares", "Middleware" },
        { "Filters", "Filter" },
        { "HealthChecks", "HealthCheck" },
        { "Configurations", "Configuration" },
        { "Interceptors", "Interceptor" },
        { "Events", "Event" },
    };

    [Fact]
    public void Types_DoNotUseHelperSuffix()
    {
        ProductionTypes()
            .Should().NotHaveNameMatching(@"Helpers?(?:`\d+)?$")
            .Because("'Helper' says nothing; use a role suffix such as Resolver, Mapper, Calculator, Builder, Formatter, Parser or Provider")
            .WithoutRequiringPositiveResults()
            .Check(GovernArchitecture.Instance);
    }

    [Theory]
    [MemberData(nameof(SuffixByFolder))]
    public void TypesInFolder_HaveFolderSuffix(string folder, string suffix)
    {
        ProductionTypes().And().ResideInNamespaceMatching(InFolder(folder))
            .Should().HaveNameMatching($@"{suffix}(?:`\d+)?$")
            .Because($"types in {folder}/ must end with '{suffix}'")
            .WithoutRequiringPositiveResults()
            .Check(GovernArchitecture.Instance);
    }

    [Fact]
    public void ApiFeatureTypes_AreEndpoints()
    {
        ProductionTypes().And().ResideInNamespaceMatching(@"^Govern\.Api\.Features(?:\..*)?$")
            .Should().HaveNameEndingWith("Endpoint")
            .Because("Api/Features holds one endpoint per file")
            .WithoutRequiringPositiveResults()
            .Check(GovernArchitecture.Instance);
    }

    [Fact]
    public void ApiFeatureEndpoints_AreStaticClassesWithMapMethod()
    {
        // A static class compiles to 'abstract sealed'. Endpoints are discovered by EndpointExtensions (Api/Extensions)
        // and must expose 'public static void Map(IEndpointRouteBuilder app)'.
        Classes().That().ResideInNamespaceMatching(@"^Govern\.Api\.Features(?:\..*)?$")
            .And().AreNotNested().And().DoNotHaveNameContaining("<")
            .Should().BeAbstract().AndShould().BeSealed()
            .AndShould().HaveMethodMemberWithName("Map(Microsoft.AspNetCore.Routing.IEndpointRouteBuilder)")
            .Because("endpoints are static XxxEndpoint classes with Map(IEndpointRouteBuilder), discovered by convention")
            .WithoutRequiringPositiveResults()
            .Check(GovernArchitecture.Instance);
    }

    [Fact]
    public void ApiEndpointParameters_WithCoreGameTypes_AreBoundWithGameSerializer()
    {
        // The HTTP JSON defaults turn a polymorphic GameCommand without 'type' into a 500, so a body carrying Core types
        // must be an Api/Models/XxxBody with 'public static ValueTask<XxxBody?> BindAsync(HttpContext)' (api-endpoints.md).
        var violations = typeof(Govern.Api.DependencyInjection).Assembly.GetTypes()
            .Where(static type => type.Namespace?.StartsWith("Govern.Api.Features", StringComparison.Ordinal) == true)
            .SelectMany(static type => type.GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static)
                .Where(static method => method.Name == "HandleAsync")
                .SelectMany(method => method.GetParameters().Select(parameter => (Endpoint: type.Name, parameter.ParameterType))))
            .Where(static candidate => CarriesCoreTypes(candidate.ParameterType) && !HasBindAsync(candidate.ParameterType))
            .Select(static candidate => $"{candidate.Endpoint}: {candidate.ParameterType.Name}")
            .ToList();

        violations.ShouldBeEmpty("bodies with Core game types must be Api/Models/*Body records with a static BindAsync(HttpContext)");
    }

    [Fact]
    public void ApiModelBodies_DeclareBindAsync()
    {
        var violations = typeof(Govern.Api.DependencyInjection).Assembly.GetTypes()
            .Where(static type => type.Namespace == "Govern.Api.Models" && type.Name.EndsWith("Body", StringComparison.Ordinal))
            .Where(static type => !HasBindAsync(type))
            .Select(static type => type.Name)
            .ToList();

        violations.ShouldBeEmpty("Api/Models/*Body types exist to be bound by their own static BindAsync(HttpContext)");
    }

    [Fact]
    public void ContractTypes_AreRequestResponseValidatorOrHandler()
    {
        ProductionTypes().And().ResideInNamespaceMatching(@"^Govern\.Application\.\w+\.Contracts$")
            .Should().HaveNameMatching("(?:Request|Response|Validator|Handler)$")
            .Because("a XxxContract.cs file contains only XxxRequest, XxxResponse, XxxValidator, XxxHandler and 'file' types")
            .WithoutRequiringPositiveResults()
            .Check(GovernArchitecture.Instance);
    }

    [Fact]
    public void ContractHandlers_AreSealedRequestHandlers()
    {
        Classes().That().ResideInNamespaceMatching(@"^Govern\.Application\.\w+\.Contracts$")
            .And().HaveNameEndingWith("Handler")
            .Should().BeSealed()
            .AndShould().ImplementInterface(typeof(IRequestHandler<,>))
            .Because("handlers are sealed IRequestHandler implementations dispatched by the mediator")
            .WithoutRequiringPositiveResults()
            .Check(GovernArchitecture.Instance);
    }

    [Fact]
    public void DomainEntities_DeriveFromBaseEntity()
    {
        ProductionTypes().And().ResideInNamespaceMatching(@"^Govern\.Domain\.Entities(?:\..*)?$")
            .Should().BeAssignableTo(typeof(BaseEntity))
            .Because("entities get a Guid v7 id and domain events from BaseEntity")
            .WithoutRequiringPositiveResults()
            .Check(GovernArchitecture.Instance);
    }

    [Fact]
    public void DomainEvents_DeriveFromBaseEvent()
    {
        ProductionTypes().And().ResideInNamespaceMatching(@"^Govern\.Domain\.Events(?:\..*)?$")
            .Should().BeAssignableTo(typeof(BaseEvent))
            .Because("domain events are dispatched through the mediator as BaseEvent notifications")
            .WithoutRequiringPositiveResults()
            .Check(GovernArchitecture.Instance);
    }

    [Fact]
    public void DomainValueObjects_AreSealedImmutableAndNotEntities()
    {
        Classes().That().ResideInNamespaceMatching(@"^Govern\.Domain\.ValueObjects(?:\..*)?$")
            .And().AreNotNested().And().DoNotHaveNameContaining("<")
            .Should().BeSealed()
            .AndShould().BeImmutable()
            .AndShould().NotBeAssignableTo(typeof(BaseEntity))
            .Because("value objects are sealed records compared by value; they have no identity and never change")
            .WithoutRequiringPositiveResults()
            .Check(GovernArchitecture.Instance);
    }

    [Fact]
    public void EventHandlers_AreSealedNotificationHandlers()
    {
        Classes().That().ResideInNamespaceMatching(InFolder("EventHandlers"))
            .And().AreNotNested().And().DoNotHaveNameContaining("<")
            .Should().BeSealed()
            .AndShould().ImplementInterface(typeof(INotificationHandler<>))
            .Because("event handlers are discovered by the mediator as INotificationHandler<TEvent>")
            .WithoutRequiringPositiveResults()
            .Check(GovernArchitecture.Instance);
    }

    [Fact]
    public void Interfaces_StartWithI()
    {
        Interfaces().That().ResideInNamespaceMatching(GovernArchitecture.GovernNamespace)
            .Should().HaveNameStartingWith("I")
            .WithoutRequiringPositiveResults()
            .Check(GovernArchitecture.Instance);
    }

    [Fact]
    public void AbstractionsFolder_ContainsOnlyAbstractTypes()
    {
        Classes().That().ResideInNamespaceMatching(InFolder("Abstractions"))
            .And().AreNotNested().And().DoNotHaveNameContaining("<")
            .Should().BeAbstract()
            .Because("Abstractions/ holds interfaces and abstract base classes only")
            .WithoutRequiringPositiveResults()
            .Check(GovernArchitecture.Instance);
    }

    [Fact]
    public void ExtensionsFolder_ContainsOnlyStaticClasses()
    {
        // A static class compiles to 'abstract sealed'.
        Classes().That().ResideInNamespaceMatching(InFolder("Extensions"))
            .And().AreNotNested().And().DoNotHaveNameContaining("<")
            .Should().BeAbstract().AndShould().BeSealed()
            .Because("extension methods live in static *Extensions classes")
            .WithoutRequiringPositiveResults()
            .Check(GovernArchitecture.Instance);
    }

    [Fact]
    public void EnumsFolder_ContainsOnlyEnums()
    {
        ProductionTypes().And().ResideInNamespaceMatching(InFolder("Enums"))
            .Should().BeEnums()
            .WithoutRequiringPositiveResults()
            .Check(GovernArchitecture.Instance);
    }

    [Fact]
    public void ClientPages_AreComponentsEndingWithPage()
    {
        ProductionTypes().And().ResideInNamespaceMatching(@"^Govern\.Client\.Features\.\w+\.Pages$")
            .Should().HaveNameEndingWith("Page")
            .AndShould().BeAssignableTo(typeof(ComponentBase))
            .Because("Features/[Feature]/Pages holds only routable components named XxxPage")
            .WithoutRequiringPositiveResults()
            .Check(GovernArchitecture.Instance);
    }

    [Fact]
    public void ClientLayoutAndComponents_AreComponents()
    {
        ProductionTypes().And().ResideInNamespaceMatching(@"^Govern\.Client\.(?:Layout|Components|Features\.\w+\.Components)(?:\..*)?$")
            .Should().BeAssignableTo(typeof(ComponentBase))
            .Because("Layout/ and Components/ hold Razor components only; logic goes to Services or a typed client")
            .WithoutRequiringPositiveResults()
            .Check(GovernArchitecture.Instance);
    }

    [Fact]
    public void ClientApiClients_AreSealedAndEndWithClient()
    {
        Classes().That().ResideInNamespaceMatching(@"^Govern\.Client\.Features\.\w+\.Clients$")
            .And().AreNotNested().And().DoNotHaveNameContaining("<")
            .Should().BeSealed()
            .AndShould().HaveNameEndingWith("Client")
            .Because("typed Api clients are sealed XxxClient classes, one per feature")
            .WithoutRequiringPositiveResults()
            .Check(GovernArchitecture.Instance);
    }

    // Top-level, hand-written Govern types (excludes nested types such as LoggerMessage 'Log' classes and compiler-generated/file types).
    private static GivenTypesConjunction ProductionTypes() =>
        Types().That().ResideInNamespaceMatching(GovernArchitecture.GovernNamespace)
            .And().AreNotNested()
            .And().DoNotHaveNameContaining("<");

    private static string InFolder(string folder) => $@"^Govern(?:\.\w+)*\.{folder}(?:\.\w+)*$";

    private static bool IsCoreType(Type type) => type.Namespace?.StartsWith("Govern.Core", StringComparison.Ordinal) == true;

    private static bool CarriesCoreTypes(Type type) =>
        IsCoreType(type) || type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Any(static property => IsCoreType(property.PropertyType));

    private static bool HasBindAsync(Type type) =>
        type.GetMethod("BindAsync", BindingFlags.Public | BindingFlags.Static, [typeof(Microsoft.AspNetCore.Http.HttpContext)]) is { } method
        && method.ReturnType.IsGenericType
        && method.ReturnType.GetGenericTypeDefinition() == typeof(ValueTask<>);
}
