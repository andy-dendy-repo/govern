using ArchUnitNET.xUnitV3;
using Govern.ArchitectureTests.Conventions;
using Govern.ArchitectureTests.Support;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Govern.ArchitectureTests;

public class LayerDependencyTests
{
    public static TheoryData<string> LayersWithRestrictions => new(
        GovernArchitecture.AssemblyNames.Where(static project => ForbiddenProjects(project).Length > 0));

    [Theory]
    [MemberData(nameof(LayersWithRestrictions))]
    public void Layer_DoesNotDependOnForbiddenLayers(string project)
    {
        var forbidden = ForbiddenProjects(project);

        Types().That().ResideInNamespaceMatching(Layers.NamespacePattern(project))
            .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(AnyOf(forbidden.Select(Layers.NamespacePattern)))
            .Because($"{project} may only depend on: {string.Join(", ", Layers.AllowedReferences[project])}")
            .WithoutRequiringPositiveResults()
            .Check(GovernArchitecture.Instance);
    }

    [Fact]
    public void Domain_DoesNotDependOnPersistenceOrWebFrameworks()
    {
        Types().That().ResideInNamespaceMatching(Layers.NamespacePattern("Govern.Domain"))
            .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(@"^Microsoft\.(?:EntityFrameworkCore|AspNetCore)(?:\..*)?$")
            .Because("the domain model is persistence and transport agnostic")
            .WithoutRequiringPositiveResults()
            .Check(GovernArchitecture.Instance);
    }

    [Fact]
    public void Application_DoesNotDependOnAspNetCore()
    {
        Types().That().ResideInNamespaceMatching(Layers.NamespacePattern("Govern.Application"))
            .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(@"^Microsoft\.AspNetCore(?:\..*)?$")
            .Because("use cases must not know about HTTP; the Api maps results to responses")
            .WithoutRequiringPositiveResults()
            .Check(GovernArchitecture.Instance);
    }

    [Fact]
    public void ApiFeatures_DoNotDependOnInfrastructureOrDomain()
    {
        Types().That().ResideInNamespaceMatching(@"^Govern\.Api\.Features(?:\..*)?$")
            .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(
                AnyOf([Layers.NamespacePattern("Govern.Infrastructure"), Layers.NamespacePattern("Govern.Domain")]))
            .Because("endpoints talk to Application contracts only; Infrastructure is wired in DependencyInjection.cs")
            .WithoutRequiringPositiveResults()
            .Check(GovernArchitecture.Instance);
    }

    private static string[] ForbiddenProjects(string project) =>
        GovernArchitecture.AssemblyNames
            .Where(other => other != project && !Layers.AllowedReferences[project].Contains(other))
            .ToArray();

    private static string AnyOf(IEnumerable<string> patterns) => string.Join('|', patterns.Select(static p => $"(?:{p})"));
}
