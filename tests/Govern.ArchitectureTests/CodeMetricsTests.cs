using Govern.ArchitectureTests.Conventions;
using Govern.ArchitectureTests.Support;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Govern.ArchitectureTests;

public class CodeMetricsTests
{
    private static readonly Lazy<List<SourceFile>> SourceTrees = new(LoadSourceTrees);

    [Fact]
    public void Files_DoNotExceedMaxLines()
    {
        var violations = SourceTrees.Value
            .Select(static file => (tree: file.Tree, lines: file.Tree.GetText(TestContext.Current.CancellationToken).Lines.Count))
            .Where(static file => file.lines > CodeLimits.MaxFileLines)
            .Select(static file => $"{Location(file.tree, 0)}: {file.lines} lines (max {CodeLimits.MaxFileLines})")
            .ToList();

        violations.ShouldBeEmpty("Split the file by responsibility");
    }

    [Fact]
    public void MemberBodies_DoNotExceedMaxLines()
    {
        var violations = new List<string>();

        foreach (var (tree, root) in SourceTrees.Value)
        {
            foreach (var (name, body) in MemberBodies(root))
            {
                var span = tree.GetLineSpan(body.Span, TestContext.Current.CancellationToken);
                var lines = span.EndLinePosition.Line - span.StartLinePosition.Line + 1;
                if (lines > CodeLimits.MaxMemberBodyLines)
                {
                    violations.Add($"{Location(tree, body.SpanStart)}: '{name}' body has {lines} lines (max {CodeLimits.MaxMemberBodyLines})");
                }
            }
        }

        violations.ShouldBeEmpty("Extract well-named methods or types instead of compressing code");
    }

    [Fact]
    public void Methods_DoNotExceedMaxParameters()
    {
        var violations = new List<string>();

        foreach (var (tree, root) in SourceTrees.Value)
        {
            foreach (var node in root.DescendantNodes())
            {
                var (name, parameters) = node switch
                {
                    MethodDeclarationSyntax method => (method.Identifier.Text, method.ParameterList),
                    LocalFunctionStatementSyntax local => (local.Identifier.Text, local.ParameterList),
                    DelegateDeclarationSyntax @delegate => (@delegate.Identifier.Text, @delegate.ParameterList),
                    _ => (string.Empty, null),
                };

                var count = CountParameters(parameters);
                if (count > CodeLimits.MaxMethodParameters)
                {
                    violations.Add($"{Location(tree, node.SpanStart)}: '{name}' has {count} parameters (max {CodeLimits.MaxMethodParameters})");
                }
            }
        }

        violations.ShouldBeEmpty("Introduce a parameter object (record) or split the method");
    }

    [Fact]
    public void Constructors_DoNotExceedMaxParameters()
    {
        var violations = new List<string>();

        foreach (var (tree, root) in SourceTrees.Value)
        {
            foreach (var node in root.DescendantNodes())
            {
                var (name, parameters) = node switch
                {
                    ConstructorDeclarationSyntax constructor => (constructor.Identifier.Text, constructor.ParameterList),
                    ClassDeclarationSyntax type => (type.Identifier.Text, type.ParameterList),
                    StructDeclarationSyntax type => (type.Identifier.Text, type.ParameterList),
                    _ => (string.Empty, null), // positional records are data shapes and exempt
                };

                var count = CountParameters(parameters);
                if (count > CodeLimits.MaxConstructorParameters)
                {
                    violations.Add($"{Location(tree, node.SpanStart)}: '{name}' constructor has {count} parameters (max {CodeLimits.MaxConstructorParameters})");
                }
            }
        }

        violations.ShouldBeEmpty("Too many dependencies usually means too many responsibilities; split the type");
    }

    private static IEnumerable<(string Name, SyntaxNode Body)> MemberBodies(SyntaxNode root)
    {
        foreach (var node in root.DescendantNodes())
        {
            switch (node)
            {
                case BaseMethodDeclarationSyntax method when ((SyntaxNode?)method.Body ?? method.ExpressionBody) is { } body:
                    yield return (MemberName(method), body);
                    break;
                case LocalFunctionStatementSyntax local when ((SyntaxNode?)local.Body ?? local.ExpressionBody) is { } body:
                    yield return (local.Identifier.Text, body);
                    break;
                case AccessorDeclarationSyntax accessor when ((SyntaxNode?)accessor.Body ?? accessor.ExpressionBody) is { } body:
                    yield return (accessor.Keyword.Text, body);
                    break;
            }
        }
    }

    private static string MemberName(BaseMethodDeclarationSyntax method) => method switch
    {
        MethodDeclarationSyntax m => m.Identifier.Text,
        ConstructorDeclarationSyntax c => c.Identifier.Text,
        _ => method.Kind().ToString(),
    };

    private static int CountParameters(ParameterListSyntax? parameters) =>
        parameters?.Parameters.Count(static p => p.Type?.ToString() is not ("CancellationToken" or "System.Threading.CancellationToken")) ?? 0;

    private static string Location(SyntaxTree tree, int position) =>
        $"{SourceTree.Relative(tree.FilePath)}:{tree.GetLineSpan(new Microsoft.CodeAnalysis.Text.TextSpan(position, 0)).StartLinePosition.Line + 1}";

    private static List<SourceFile> LoadSourceTrees() =>
        SourceTree.ProjectFolders()
            .Where(static folder => !Layers.Ignored.Contains(Path.GetFileName(folder)))
            .SelectMany(SourceTree.CSharpFiles)
            .Where(static file => !IsGenerated(file))
            .Select(static file => CSharpSyntaxTree.ParseText(File.ReadAllText(file), path: file))
            .Select(static tree => new SourceFile(tree, tree.GetRoot()))
            .Where(static file => !file.Root.GetLeadingTrivia().ToFullString().Contains("<auto-generated", StringComparison.Ordinal))
            .ToList();

    private sealed record SourceFile(SyntaxTree Tree, SyntaxNode Root);

    private static bool IsGenerated(string file)
    {
        var normalized = file.Replace('\\', '/');
        return CodeLimits.GeneratedPathMarkers.Any(marker => normalized.Contains(marker, StringComparison.Ordinal));
    }
}
