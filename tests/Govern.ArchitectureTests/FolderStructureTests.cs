using System.Text.RegularExpressions;
using Govern.ArchitectureTests.Conventions;
using Govern.ArchitectureTests.Support;

namespace Govern.ArchitectureTests;

public partial class FolderStructureTests
{
    public static TheoryData<string> TemplatedProjects => new(Templates.Projects.Keys);

    [Theory]
    [MemberData(nameof(TemplatedProjects))]
    public void Project_ContainsOnlyTemplateFoldersAndFiles(string project)
    {
        var violations = new List<string>();

        Validate(Path.Combine(SourceTree.SourceRoot, project), Templates.Projects[project], violations);

        violations.ShouldBeEmpty("See the folder templates in .claude/rules/architecture.md");
    }

    [Fact]
    public void ContractFiles_DeclareOnlyPrefixedRequestResponseValidatorHandlerTypes()
    {
        var violations = new List<string>();

        foreach (var file in ContractFiles())
        {
            var prefix = Path.GetFileNameWithoutExtension(file)[..^"Contract".Length];
            var allowed = new Regex($"^{Regex.Escape(prefix)}(?:Request|Response|Validator|Handler)$");

            violations.AddRange(TypeDeclarations.NonFileScopedTopLevelTypes(File.ReadAllText(file))
                .Where(name => !allowed.IsMatch(name))
                .Select(name => $"{SourceTree.Relative(file)}: '{name}' must be {prefix}Request/Response/Validator/Handler or a 'file' type"));
        }

        violations.ShouldBeEmpty();
    }

    [Fact]
    public void ActionFiles_DeclareExactlyThePrefixedCommandAndRule()
    {
        var violations = new List<string>();

        foreach (var file in ActionFiles())
        {
            var prefix = Path.GetFileNameWithoutExtension(file)[..^"Action".Length];
            string[] expected = [$"{prefix}Command", $"{prefix}Rule"];
            var declared = TypeDeclarations.NonFileScopedTopLevelTypes(File.ReadAllText(file));

            if (!declared.Order(StringComparer.Ordinal).SequenceEqual(expected.Order(StringComparer.Ordinal)))
            {
                violations.Add($"{SourceTree.Relative(file)}: expected exactly [{string.Join(", ", expected)}] (plus 'file' types), found [{string.Join(", ", declared)}]");
            }
        }

        violations.ShouldBeEmpty("A game action file holds its command and the rule that applies it; see .claude/rules/game-core.md");
    }

    [Fact]
    public void EndpointFiles_DeclareExactlyOneEndpointNamedAfterTheFile()
    {
        var violations = new List<string>();

        foreach (var file in EndpointFiles())
        {
            var expected = Path.GetFileNameWithoutExtension(file);
            var declared = TypeDeclarations.NonFileScopedTopLevelTypes(File.ReadAllText(file));

            if (declared.Count != 1 || declared[0] != expected)
            {
                violations.Add($"{SourceTree.Relative(file)}: expected exactly one type '{expected}', found [{string.Join(", ", declared)}]");
            }
        }

        violations.ShouldBeEmpty();
    }

    private static void Validate(string folder, FolderSpec spec, List<string> violations)
    {
        if (spec.IsFreeForm)
        {
            return;
        }

        foreach (var file in SourceTree.SourceFilesIn(folder))
        {
            if (!spec.IsAllowedFile(Path.GetFileName(file)))
            {
                violations.Add($"{SourceTree.Relative(file)}: file not allowed here (allowed: {spec.AllowedFilesDescription})");
            }
        }

        foreach (var subFolder in SourceTree.SourceSubFolders(folder))
        {
            var name = Path.GetFileName(subFolder);

            if (spec.Folders.TryGetValue(name, out var namedSpec))
            {
                Validate(subFolder, namedSpec, violations);
            }
            else if (spec.AnyOtherFolder is not null && PascalCaseRegex().IsMatch(name))
            {
                Validate(subFolder, spec.AnyOtherFolder, violations);
            }
            else
            {
                var allowed = string.Join(", ", spec.Folders.Keys.Order(StringComparer.Ordinal));
                var wildcard = spec.AnyOtherFolder is null ? string.Empty : $", or any PascalCase {spec.AnyOtherFolderDescription}";
                violations.Add($"{SourceTree.Relative(subFolder)}: folder not allowed here (allowed: {allowed}{wildcard})");
            }
        }
    }

    private static IEnumerable<string> ContractFiles() =>
        SourceTree.CSharpFiles(Path.Combine(SourceTree.SourceRoot, "Govern.Application"))
            .Where(static file => Path.GetFileName(Path.GetDirectoryName(file)) == "Contracts");

    private static IEnumerable<string> ActionFiles()
    {
        var actions = Path.Combine(SourceTree.SourceRoot, "Govern.Core", "Actions");
        return Directory.Exists(actions) ? SourceTree.CSharpFiles(actions) : [];
    }

    private static IEnumerable<string> EndpointFiles()
    {
        var features = Path.Combine(SourceTree.SourceRoot, "Govern.Api", "Features");
        return Directory.Exists(features) ? SourceTree.CSharpFiles(features) : [];
    }

    [GeneratedRegex("^[A-Z][A-Za-z0-9]*$")]
    private static partial Regex PascalCaseRegex();
}
