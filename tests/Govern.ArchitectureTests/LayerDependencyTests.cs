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
    public void Core_DependsOnlyOnBaseClassLibrary()
    {
        Types().That().ResideInNamespaceMatching(Layers.NamespacePattern("Govern.Core"))
            // Code coverage (CI runs `dotnet test --coverage`) injects Microsoft.CodeCoverage tracker types into every assembly.
            .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(@"^Microsoft(?!\.CodeCoverage(?:\.|$))(?:\..*)?$")
            .Because("the game engine is pure and shared with the browser: base class library only, no frameworks or infrastructure")
            .WithoutRequiringPositiveResults()
            .Check(GovernArchitecture.Instance);
    }

    [Fact]
    public void Core_DoesNotUseNondeterministicOrIoApis()
    {
        Types().That().ResideInNamespaceMatching(Layers.NamespacePattern("Govern.Core"))
            .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(@"^System\.(?:IO|Net|Threading\.Tasks)(?:\..*)?$")
            .AndShould().NotDependOnAnyTypesThat().HaveFullNameMatching(@"^System\.(?:Random|DateTime|DateTimeOffset|TimeProvider)$")
            .Because("the engine must give the same result for the same state and command: randomness comes from DeterministicRandom in the state, and there is no clock, I/O or async")
            .WithoutRequiringPositiveResults()
            .Check(GovernArchitecture.Instance);
    }

    [Fact]
    public void Core_DoesNotCreateIdentifiersOrUseHashCodes()
    {
        string[] banned = ["Guid.NewGuid", "Guid.CreateVersion7", "GetHashCode("];
        var violations = SourceTree.CSharpFiles(Path.Combine(SourceTree.SourceRoot, "Govern.Core"))
            .SelectMany(file => banned
                .Where(token => File.ReadAllText(file).Contains(token, StringComparison.Ordinal))
                .Select(token => $"{SourceTree.Relative(file)}: {token}"))
            .ToList();

        violations.ShouldBeEmpty("Core ids are drawn from the game's DeterministicRandom (GameState.RngState) and hash codes can differ between processes; both would break replay determinism");
    }

    [Fact]
    public void Client_UsesCoreOnlyForViewsAndPreviews()
    {
        Types().That().ResideInNamespaceMatching(Layers.NamespacePattern("Govern.Client"))
            .Should().NotDependOnAnyTypesThat().HaveFullNameMatching(
                @"^Govern\.Core\.(?:State\.(?:GameState|GameFactory|GameSetup)|Engine\.(?:CommandResult|GameUpdate|DeterministicRandom)|Serialization\.GameSerializer)$")
            .Because("the server is authoritative: the client reads its PlayerView and the view-based previews, never the full state, GameEngine.Execute or the storage format")
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

    [Fact]
    public void Types_DoNotUseMappingLibraries()
    {
        Types().That().ResideInNamespaceMatching(GovernArchitecture.GovernNamespace)
            .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(@"^(?:AutoMapper|Mapster|MapsterMapper|Riok\.Mapperly)(?:\..*)?$")
            .Because("mapping is explicit: EF Select projections and static Projection/From members (see .claude/rules/mapping.md)")
            .WithoutRequiringPositiveResults()
            .Check(GovernArchitecture.Instance);
    }

    [Fact]
    public void Client_UsesHttpOnlyInApiClientsAndAuth()
    {
        // The root DependencyInjection (and its compiler-generated closures) registers typed clients and handlers;
        // Extensions/HttpClientExtensions sends the typed clients' requests. Reading server-sent events counts as HTTP.
        Types().That().ResideInNamespaceMatching(@"^Govern\.Client(?:\.(?!Auth(?:\.|$)|Features\.\w+\.Clients$).*)?$")
            .And().DoNotHaveFullNameMatching(@"^Govern\.Client\.(?:DependencyInjection|Extensions\.HttpClientExtensions)(?:[+/].*)?$")
            .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(@"^System\.Net\.(?:Http|ServerSentEvents)(?:\..*)?$")
            .Because("components never call HttpClient; Api calls go through typed Features/[Feature]/Clients (auth plumbing lives in Auth/, registration in DependencyInjection, the shared send in Extensions/HttpClientExtensions)")
            .WithoutRequiringPositiveResults()
            .Check(GovernArchitecture.Instance);
    }

    [Fact]
    public void Client_UsesJavaScriptInteropOnlyInAuthAndServices()
    {
        Types().That().ResideInNamespaceMatching(@"^Govern\.Client(?:\.(?!Auth(?:\.|$)|Services(?:\.|$)|Features\.\w+\.Services(?:\.|$)).*)?$")
            .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(@"^Microsoft\.JSInterop(?:\..*)?$")
            .Because("JS interop reaches the DOM and localStorage; keeping it in Auth/ and Services/ (root or feature) keeps the XSS-relevant surface small")
            .WithoutRequiringPositiveResults()
            .Check(GovernArchitecture.Instance);
    }

    private static string[] ForbiddenProjects(string project) =>
        GovernArchitecture.AssemblyNames
            .Where(other => other != project && !Layers.AllowedReferences[project].Contains(other))
            .ToArray();

    private static string AnyOf(IEnumerable<string> patterns) => string.Join('|', patterns.Select(static p => $"(?:{p})"));
}
