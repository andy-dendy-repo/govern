namespace Govern.ArchitectureTests.Conventions;

/// <summary>
/// Describes which sub-folders and which source files (<c>.cs</c>, <c>.razor</c>) may exist inside a folder.
/// </summary>
public sealed class FolderSpec
{
    /// <summary>
    /// A folder whose content is not restricted (any sub-folders and files).
    /// </summary>
    public static FolderSpec FreeForm { get; } = new() { IsFreeForm = true };

    /// <summary>
    /// When <see langword="true"/>, nothing below this folder is validated.
    /// </summary>
    public bool IsFreeForm { get; init; }

    /// <summary>
    /// Named sub-folders that are allowed, each with its own spec.
    /// </summary>
    public IReadOnlyDictionary<string, FolderSpec> Folders { get; init; } = new Dictionary<string, FolderSpec>();

    /// <summary>
    /// Spec applied to any other PascalCase sub-folder (feature or infrastructure concern folders);
    /// <see langword="null"/> means no other sub-folders are allowed.
    /// </summary>
    public FolderSpec? AnyOtherFolder { get; init; }

    /// <summary>
    /// Human-readable name of the wildcard folder, used in violation messages.
    /// </summary>
    public string AnyOtherFolderDescription { get; init; } = "folder";

    /// <summary>
    /// Decides whether a source file (<c>.cs</c>, <c>.razor</c>) with the given name may live directly in this folder.
    /// </summary>
    public Func<string, bool> IsAllowedFile { get; init; } = static _ => false;

    /// <summary>
    /// Human-readable description of <see cref="IsAllowedFile"/>, used in violation messages.
    /// </summary>
    public string AllowedFilesDescription { get; init; } = "no source files";
}
