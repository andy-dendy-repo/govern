namespace Govern.ArchitectureTests.Conventions;

/// <summary>
/// Single source of truth for the folder templates described in <c>.claude/rules/architecture.md</c>
/// (and, for the client, <c>.claude/rules/blazor-client.md</c>).
/// Folders are created on demand: specs whitelist what may exist, nothing is required.
/// </summary>
public static class Templates
{
    /// <summary>
    /// UF — utility folders, allowed in every templated project.
    /// </summary>
    public static readonly string[] UtilityFolders =
        ["Helpers", "Models", "Enums", "Options", "Exceptions", "Extensions", "Attributes", "Services"];

    /// <summary>
    /// AGFT — application general folders (UF + application specific).
    /// </summary>
    public static readonly string[] ApplicationGeneralFolders =
        [.. UtilityFolders, "Abstractions", "EventHandlers", "Repositories"];

    /// <summary>
    /// Root files allowed in AGFT-based projects.
    /// </summary>
    public static readonly string[] ApplicationGeneralRootFiles = ["DependencyInjection.cs", "GlobalUsings.cs"];

    /// <summary>
    /// Concept folders of the game engine (<c>Govern.Core</c>); see <c>.claude/rules/game-core.md</c>.
    /// </summary>
    public static readonly string[] CoreFolders =
        ["Engine", "State", "Map", "Units", "Cities", "Events", "Visibility", "Serialization", "Rulesets"];

    /// <summary>
    /// Folder specs per project. Projects that are not listed here are not validated.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, FolderSpec> Projects = new Dictionary<string, FolderSpec>
    {
        ["Govern.Application"] = Application(),
        ["Govern.Application.Common"] = ApplicationCommon(),
        ["Govern.Infrastructure"] = Infrastructure(),
        ["Govern.Domain"] = Domain(),
        ["Govern.Api"] = Api(),
        ["Govern.Client"] = Client(),
        ["Govern.Core"] = Core(),
    };

    // 3DFT: AGFT + feature folders, each feature folder is AGFT + Contracts.
    private static FolderSpec Application() => new()
    {
        Folders = FreeFormFolders(ApplicationGeneralFolders),
        AnyOtherFolder = new FolderSpec
        {
            Folders = new Dictionary<string, FolderSpec>(FreeFormFolders(ApplicationGeneralFolders))
            {
                ["Contracts"] = Contracts(),
            },
        },
        AnyOtherFolderDescription = "feature folder",
        IsAllowedFile = OneOf(ApplicationGeneralRootFiles),
        AllowedFilesDescription = string.Join(", ", ApplicationGeneralRootFiles),
    };

    // AGFT only: inherits, but does not extend.
    private static FolderSpec ApplicationCommon() => new()
    {
        Folders = FreeFormFolders(ApplicationGeneralFolders),
        IsAllowedFile = OneOf(ApplicationGeneralRootFiles),
        AllowedFilesDescription = string.Join(", ", ApplicationGeneralRootFiles),
    };

    // UF + one folder per infrastructure concern (each UF) + Background + Persistence.
    private static FolderSpec Infrastructure()
    {
        string[] persistenceFiles =
            ["ApplicationDbContext.cs", "ApplicationDbContextFactory.cs", "ApplicationDbContextTransaction.cs"];

        return new FolderSpec
        {
            Folders = new Dictionary<string, FolderSpec>(FreeFormFolders(UtilityFolders))
            {
                ["Background"] = FolderSpec.FreeForm,
                ["Persistence"] = new FolderSpec
                {
                    Folders = FreeFormFolders([.. UtilityFolders, "Configurations", "Interceptors", "Migrations"]),
                    IsAllowedFile = OneOf(persistenceFiles),
                    AllowedFilesDescription = string.Join(", ", persistenceFiles),
                },
            },
            AnyOtherFolder = new FolderSpec { Folders = FreeFormFolders(UtilityFolders) },
            AnyOtherFolderDescription = "infrastructure concern folder",
            IsAllowedFile = OneOf(ApplicationGeneralRootFiles),
            AllowedFilesDescription = string.Join(", ", ApplicationGeneralRootFiles),
        };
    }

    // UF + Entities, Events, ValueObjects, EntityBase.
    private static FolderSpec Domain()
    {
        string[] entityBaseFiles = ["BaseEntity.cs", "BaseAuditableEntity.cs", "BaseEvent.cs"];

        return new FolderSpec
        {
            Folders = new Dictionary<string, FolderSpec>(FreeFormFolders([.. UtilityFolders, "Entities", "Events", "ValueObjects"]))
            {
                ["EntityBase"] = new FolderSpec
                {
                    IsAllowedFile = OneOf(entityBaseFiles),
                    AllowedFilesDescription = string.Join(", ", entityBaseFiles),
                },
            },
            IsAllowedFile = OneOf(["GlobalUsings.cs"]),
            AllowedFilesDescription = "GlobalUsings.cs",
        };
    }

    // Game engine: concept folders + flat Actions/XxxAction.cs (command and rule together); no root files.
    private static FolderSpec Core() => new()
    {
        Folders = new Dictionary<string, FolderSpec>(FreeFormFolders(CoreFolders))
        {
            ["Actions"] = new FolderSpec
            {
                IsAllowedFile = static file => file.EndsWith("Action.cs", StringComparison.Ordinal),
                AllowedFilesDescription = "*Action.cs (XxxCommand + XxxRule)",
            },
        },
    };

    // UF + Middlewares, Filters, HealthChecks, Features/[Feature]/XxxEndpoint.cs.
    private static FolderSpec Api()
    {
        string[] rootFiles = ["DependencyInjection.cs", "GlobalUsings.cs", "Program.cs"];

        return new FolderSpec
        {
            Folders = new Dictionary<string, FolderSpec>(FreeFormFolders([.. UtilityFolders, "Middlewares", "Filters", "HealthChecks"]))
            {
                ["Features"] = new FolderSpec
                {
                    AnyOtherFolder = new FolderSpec
                    {
                        IsAllowedFile = static file => file.EndsWith("Endpoint.cs", StringComparison.Ordinal),
                        AllowedFilesDescription = "*Endpoint.cs (one endpoint per file)",
                    },
                    AnyOtherFolderDescription = "feature folder",
                },
            },
            IsAllowedFile = OneOf(rootFiles),
            AllowedFilesDescription = string.Join(", ", rootFiles),
        };
    }

    // CFT: UF + Layout, Components, Auth, Features/[Feature]/ (UF + Pages, Components, Clients).
    private static FolderSpec Client()
    {
        string[] rootFiles = ["App.razor", "DependencyInjection.cs", "GlobalUsings.cs", "Program.cs", "_Imports.razor"];
        var components = new FolderSpec
        {
            AnyOtherFolder = new FolderSpec { IsAllowedFile = IsComponentFile, AllowedFilesDescription = ComponentFiles },
            AnyOtherFolderDescription = "component group folder",
            IsAllowedFile = IsComponentFile,
            AllowedFilesDescription = ComponentFiles,
        };

        return new FolderSpec
        {
            Folders = new Dictionary<string, FolderSpec>(FreeFormFolders(UtilityFolders))
            {
                ["Layout"] = new FolderSpec { IsAllowedFile = IsComponentFile, AllowedFilesDescription = ComponentFiles },
                ["Components"] = components,
                ["Auth"] = FolderSpec.FreeForm,
                ["Features"] = new FolderSpec
                {
                    AnyOtherFolder = new FolderSpec
                    {
                        Folders = new Dictionary<string, FolderSpec>(FreeFormFolders(UtilityFolders))
                        {
                            ["Pages"] = new FolderSpec
                            {
                                IsAllowedFile = static file => file.EndsWith("Page.razor", StringComparison.Ordinal)
                                    || file.EndsWith("Page.razor.cs", StringComparison.Ordinal),
                                AllowedFilesDescription = "*Page.razor, *Page.razor.cs (routable components)",
                            },
                            ["Components"] = components,
                            ["Clients"] = new FolderSpec
                            {
                                IsAllowedFile = static file => file.EndsWith("Client.cs", StringComparison.Ordinal),
                                AllowedFilesDescription = "*Client.cs (typed Api clients)",
                            },
                        },
                    },
                    AnyOtherFolderDescription = "feature folder",
                },
            },
            IsAllowedFile = OneOf(rootFiles),
            AllowedFilesDescription = string.Join(", ", rootFiles),
        };
    }

    private const string ComponentFiles = "*.razor, *.razor.cs (components and their code-behind)";

    private static bool IsComponentFile(string file) =>
        file.EndsWith(".razor", StringComparison.Ordinal) || file.EndsWith(".razor.cs", StringComparison.Ordinal);

    private static FolderSpec Contracts() => new()
    {
        IsAllowedFile = static file => file.EndsWith("Contract.cs", StringComparison.Ordinal),
        AllowedFilesDescription = "*Contract.cs",
    };

    private static Dictionary<string, FolderSpec> FreeFormFolders(IEnumerable<string> names) =>
        names.ToDictionary(static name => name, static _ => FolderSpec.FreeForm, StringComparer.Ordinal);

    private static Func<string, bool> OneOf(string[] files) =>
        file => files.Contains(file, StringComparer.Ordinal);
}
