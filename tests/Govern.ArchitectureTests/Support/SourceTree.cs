namespace Govern.ArchitectureTests.Support;

/// <summary>
/// Locates the repository and enumerates source folders and files, skipping build output and tooling folders.
/// </summary>
public static class SourceTree
{
    private static readonly string[] IgnoredFolders = ["bin", "obj", "Properties", "wwwroot", "node_modules"];

    /// <summary>
    /// Absolute path of the repository root (the folder that contains <c>Govern.slnx</c>).
    /// </summary>
    public static string RepositoryRoot { get; } = FindRepositoryRoot();

    /// <summary>
    /// Absolute path of the <c>src</c> folder.
    /// </summary>
    public static string SourceRoot => Path.Combine(RepositoryRoot, "src");

    /// <summary>
    /// Absolute paths of all source project folders (folders under <c>src</c> that contain a <c>.csproj</c>).
    /// </summary>
    /// <returns>Project folders ordered by name.</returns>
    public static IEnumerable<string> ProjectFolders() =>
        Directory.EnumerateDirectories(SourceRoot)
            .Where(static dir => Directory.EnumerateFiles(dir, "*.csproj").Any())
            .Order(StringComparer.Ordinal);

    /// <summary>
    /// Sub-folders of <paramref name="folder"/> that are part of the source tree.
    /// </summary>
    /// <param name="folder">Absolute folder path.</param>
    /// <returns>Absolute paths of the source sub-folders.</returns>
    public static IEnumerable<string> SourceSubFolders(string folder) =>
        Directory.EnumerateDirectories(folder)
            .Where(static dir =>
            {
                var name = Path.GetFileName(dir);
                return !name.StartsWith('.') && !IgnoredFolders.Contains(name, StringComparer.Ordinal);
            });

    /// <summary>
    /// All <c>.cs</c> files below <paramref name="folder"/> that are part of the source tree.
    /// </summary>
    /// <param name="folder">Absolute folder path.</param>
    /// <returns>Absolute file paths.</returns>
    public static IEnumerable<string> CSharpFiles(string folder) => FilesBelow(folder, "*.cs");

    /// <summary>
    /// All Razor component files (<c>.razor</c>) below <paramref name="folder"/> that are part of the source tree.
    /// </summary>
    /// <param name="folder">Absolute folder path.</param>
    /// <returns>Absolute file paths.</returns>
    public static IEnumerable<string> RazorFiles(string folder) => FilesBelow(folder, "*.razor");

    /// <summary>
    /// Source files (<c>.cs</c> and <c>.razor</c>) directly in <paramref name="folder"/>, not in its sub-folders.
    /// </summary>
    /// <param name="folder">Absolute folder path.</param>
    /// <returns>Absolute file paths.</returns>
    public static IEnumerable<string> SourceFilesIn(string folder) =>
        Directory.EnumerateFiles(folder, "*.cs").Concat(Directory.EnumerateFiles(folder, "*.razor"));

    private static IEnumerable<string> FilesBelow(string folder, string pattern) =>
        Directory.EnumerateFiles(folder, pattern)
            .Concat(SourceSubFolders(folder).SelectMany(sub => FilesBelow(sub, pattern)));

    /// <summary>
    /// Path of <paramref name="path"/> relative to the repository root, using forward slashes.
    /// </summary>
    /// <param name="path">Absolute path.</param>
    /// <returns>Repository-relative path.</returns>
    public static string Relative(string path) =>
        Path.GetRelativePath(RepositoryRoot, path).Replace('\\', '/');

    private static string FindRepositoryRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Govern.slnx")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException("Could not locate Govern.slnx above " + AppContext.BaseDirectory);
    }
}
