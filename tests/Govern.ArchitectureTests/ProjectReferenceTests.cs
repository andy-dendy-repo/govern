using System.Xml.Linq;
using Govern.ArchitectureTests.Conventions;
using Govern.ArchitectureTests.Support;

namespace Govern.ArchitectureTests;

public class ProjectReferenceTests
{
    public static TheoryData<string> LayeredProjects => new(Layers.AllowedReferences.Keys);

    [Fact]
    public void SourceProjects_AreAllRegisteredInLayers()
    {
        var unknown = SourceTree.ProjectFolders()
            .Select(Path.GetFileName)
            .Where(static name => !Layers.AllowedReferences.ContainsKey(name!))
            .ToList();

        unknown.ShouldBeEmpty("Register new src projects in Conventions/Layers.cs and .claude/rules/project-map.md");
    }

    [Theory]
    [MemberData(nameof(LayeredProjects))]
    public void Project_ReferencesOnlyAllowedProjects(string project)
    {
        var csproj = Path.Combine(SourceTree.SourceRoot, project, project + ".csproj");
        var forbidden = ProjectReferences(csproj).Except(Layers.AllowedReferences[project]).ToList();

        forbidden.ShouldBeEmpty($"{project} may only reference: {string.Join(", ", Layers.AllowedReferences[project])}");
    }

    [Fact]
    public void Core_HasNoPackageReferences()
    {
        var csproj = Path.Combine(SourceTree.SourceRoot, "Govern.Core", "Govern.Core.csproj");
        var packages = XDocument.Load(csproj)
            .Descendants("PackageReference")
            .Select(static reference => reference.Attribute("Include")!.Value)
            .ToList();

        packages.ShouldBeEmpty("Govern.Core is the pure game engine shared with the browser; it uses the base class library only");
    }

    [Fact]
    public void UnitTests_ReferenceOnlyPureProjects()
    {
        var csproj = Path.Combine(SourceTree.RepositoryRoot, "tests", "Govern.UnitTests", "Govern.UnitTests.csproj");
        var forbidden = ProjectReferences(csproj).Except(Layers.UnitTestReferences).ToList();

        forbidden.ShouldBeEmpty($"Govern.UnitTests may only reference: {string.Join(", ", Layers.UnitTestReferences)}; test everything else with integration tests");
    }

    [Fact]
    public void ClientTests_ReferenceOnlyClient()
    {
        var csproj = Path.Combine(SourceTree.RepositoryRoot, "tests", "Govern.ClientTests", "Govern.ClientTests.csproj");
        var forbidden = ProjectReferences(csproj).Except(["Govern.Client"]).ToList();

        forbidden.ShouldBeEmpty("Govern.ClientTests may only reference Govern.Client; the client never shares code with the server");
    }

    private static List<string> ProjectReferences(string csproj) =>
        XDocument.Load(csproj)
            .Descendants("ProjectReference")
            .Select(static reference => Path.GetFileNameWithoutExtension(reference.Attribute("Include")!.Value.Replace('\\', '/')))
            .ToList();
}
