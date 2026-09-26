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
            .Where(static name => !Layers.AllowedReferences.ContainsKey(name!) && !Layers.Ignored.Contains(name))
            .ToList();

        unknown.ShouldBeEmpty("Register new src projects in Conventions/Layers.cs and .claude/rules/project-map.md");
    }

    [Theory]
    [MemberData(nameof(LayeredProjects))]
    public void Project_ReferencesOnlyAllowedProjects(string project)
    {
        var csproj = Path.Combine(SourceTree.SourceRoot, project, project + ".csproj");
        var references = XDocument.Load(csproj)
            .Descendants("ProjectReference")
            .Select(static reference => Path.GetFileNameWithoutExtension(reference.Attribute("Include")!.Value.Replace('\\', '/')))
            .ToList();

        var forbidden = references.Except(Layers.AllowedReferences[project]).ToList();

        forbidden.ShouldBeEmpty($"{project} may only reference: {string.Join(", ", Layers.AllowedReferences[project])}");
    }
}
