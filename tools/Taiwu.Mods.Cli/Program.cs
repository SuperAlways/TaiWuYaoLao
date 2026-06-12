using System.ComponentModel;
using System.CommandLine;
using CliWrap.Exceptions;
using Microsoft.CodeAnalysis.CSharp;

namespace Taiwu.Mods.Cli;

internal static class Program
{
    private const string DefaultModVersion = "0.0.0";
    private const string DefaultModTemplateRelativePath = "templates/mod";
    private const string DefaultSharedTemplateRelativePath = "templates/shared";
    private const string DefaultModsRelativePath = "mods";
    private const string DefaultSharedRelativePath = "shared";
    private const string SolutionFileName = "Taiwu.Mods.slnx";
    private const string ModsSolutionFolderName = "mods";
    private const string SharedSolutionFolderName = "shared";

    public static async Task<int> Main(string[] args)
    {
        try
        {
            Command command = CommandLineOptions.CreateCommand(RunAsync);
            return await command.Parse(args)
                .InvokeAsync(CreateInvocationConfiguration());
        }
        catch (OperationCanceledException)
        {
            return 130;
        }
        catch (Exception ex) when (ShouldReportError(ex))
        {
            return await ReportErrorAsync(ex);
        }
    }

    private static InvocationConfiguration CreateInvocationConfiguration()
    {
        return new InvocationConfiguration
        {
            EnableDefaultExceptionHandler = false,
        };
    }

    private static async Task<int> ReportErrorAsync(Exception ex)
    {
        await Console.Error.WriteLineAsync($"错误：{ex.Message}");
        return 1;
    }

    private static bool ShouldReportError(Exception ex)
    {
        return ex is ArgumentException
            or IOException
            or UnauthorizedAccessException
            or InvalidOperationException
            or CommandExecutionException
            or Win32Exception;
    }

    private static Task RunAsync(CommandLineOptions options, CancellationToken cancellationToken)
    {
        return options.Operation switch
        {
            CliOperation.CreateMod => CreateModAsync(options, cancellationToken),
            CliOperation.RemoveMod => RemoveModAsync(options, cancellationToken),
            CliOperation.PackMod => PackModAsync(options, cancellationToken),
            CliOperation.CreateShared => CreateSharedProjectAsync(options, cancellationToken),
            CliOperation.RemoveShared => RemoveSharedProjectAsync(options, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(options)),
        };
    }

    private static async Task CreateModAsync(CommandLineOptions options, CancellationToken cancellationToken)
    {
        ValidateNamespaceStyleIdentifier(options.Name, "ModName");

        string repoRoot = Path.GetFullPath(options.RepoRoot);
        string templateRoot = Path.Combine(repoRoot, DefaultModTemplateRelativePath);
        string modsRoot = Path.GetFullPath(options.ModsRoot ?? Path.Combine(repoRoot, DefaultModsRelativePath));
        string modRoot = Path.Combine(modsRoot, options.Name);

        if (Directory.Exists(modRoot) && !options.Force)
        {
            throw new InvalidOperationException($"Mod 目录已存在：{modRoot}。如需覆盖模板文件，请传入 --force。");
        }

        TemplateDirectory.Create(templateRoot, TemplateRenderer.ForMod(options.Name, DefaultModVersion)).CopyTo(modRoot, options.Force);

        if (!options.SkipSolution && IsUnderDirectory(modRoot, repoRoot))
        {
            await AddProjectsToSolutionAsync(
                repoRoot,
                ModsSolutionFolderName,
                GetModProjectFullPaths(modsRoot, options.Name),
                cancellationToken);
        }

        Console.WriteLine($"已创建 mod '{options.Name}'：{modRoot}");
    }

    private static async Task CreateSharedProjectAsync(CommandLineOptions options, CancellationToken cancellationToken)
    {
        ValidateNamespaceStyleIdentifier(options.Name, "ProjectName");
        SharedProjectSide side = ParseSharedProjectSide(options.SharedSide);

        string repoRoot = Path.GetFullPath(options.RepoRoot);
        string templateRoot = Path.Combine(repoRoot, DefaultSharedTemplateRelativePath);
        string sharedRoot = Path.GetFullPath(options.SharedRoot ?? Path.Combine(repoRoot, DefaultSharedRelativePath));
        string projectRoot = Path.Combine(sharedRoot, options.Name);

        if (Directory.Exists(projectRoot) && !options.Force)
        {
            throw new InvalidOperationException($"内部共享项目目录已存在：{projectRoot}。如需覆盖模板文件，请传入 --force。");
        }

        TemplateDirectory.Create(templateRoot, TemplateRenderer.ForSharedProject(options.Name, side, GetDefaultSharedProjectTargetFramework(side))).CopyTo(projectRoot, options.Force);

        if (!options.SkipSolution && IsUnderDirectory(projectRoot, repoRoot))
        {
            await AddProjectsToSolutionAsync(
                repoRoot,
                SharedSolutionFolderName,
                [GetSharedProjectFullPath(sharedRoot, options.Name)],
                cancellationToken);
        }

        Console.WriteLine($"已创建内部共享项目 '{options.Name}'：{projectRoot}");
    }

    private static Task AddProjectsToSolutionAsync(
        string repoRoot,
        string solutionFolderName,
        IEnumerable<string> fullProjectPaths,
        CancellationToken cancellationToken)
    {
        string[] projectPaths =
        [
            .. fullProjectPaths.Select(fullProjectPath => GetRepoRelativePath(repoRoot, fullProjectPath)),
        ];

        return ProcessRunner.RunAsync(
            "dotnet",
            repoRoot,
            [
                "sln",
                SolutionFileName,
                "add",
                "--solution-folder",
                solutionFolderName,
                .. projectPaths,
                "--include-references",
                "false",
            ],
            cancellationToken);
    }

    private static Task RemoveModAsync(CommandLineOptions options, CancellationToken cancellationToken)
    {
        ValidateNamespaceStyleIdentifier(options.Name, "ModName");

        string repoRoot = Path.GetFullPath(options.RepoRoot);
        string modsRoot = Path.GetFullPath(options.ModsRoot ?? Path.Combine(repoRoot, DefaultModsRelativePath));
        return RemoveProjectsFromSolutionAsync(
            repoRoot,
            GetModProjectFullPaths(modsRoot, options.Name),
            cancellationToken);
    }

    private static Task RemoveSharedProjectAsync(CommandLineOptions options, CancellationToken cancellationToken)
    {
        ValidateNamespaceStyleIdentifier(options.Name, "ProjectName");

        string repoRoot = Path.GetFullPath(options.RepoRoot);
        string sharedRoot = Path.GetFullPath(options.SharedRoot ?? Path.Combine(repoRoot, DefaultSharedRelativePath));
        return RemoveProjectsFromSolutionAsync(
            repoRoot,
            [GetSharedProjectFullPath(sharedRoot, options.Name)],
            cancellationToken);
    }

    private static async Task RemoveProjectsFromSolutionAsync(
        string repoRoot,
        IEnumerable<string> fullProjectPaths,
        CancellationToken cancellationToken)
    {
        string[] projectPaths =
        [
            .. fullProjectPaths.Select(fullProjectPath => GetRepoRelativePath(repoRoot, fullProjectPath)),
        ];

        await ProcessRunner.RunAsync(
            "dotnet",
            repoRoot,
            ["sln", SolutionFileName, "remove", .. projectPaths],
            cancellationToken);
    }

    private static async Task PackModAsync(CommandLineOptions options, CancellationToken cancellationToken)
    {
        ValidateNamespaceStyleIdentifier(options.Name, "ModName");

        string repoRoot = Path.GetFullPath(options.RepoRoot);
        string modsRoot = Path.GetFullPath(options.ModsRoot ?? Path.Combine(repoRoot, DefaultModsRelativePath));
        string artifactsRoot = Path.GetFullPath(options.ArtifactsRoot ?? Path.Combine(repoRoot, "artifacts", "mods"));

        ModPacker packer = new(repoRoot, modsRoot, artifactsRoot, options.Configuration);
        await packer.PackAsync(options.Name, cancellationToken);
    }

    private static string[] GetModProjectFullPaths(string modsRoot, string modName)
    {
        string modRoot = Path.Combine(modsRoot, modName);
        if (!Directory.Exists(modRoot))
        {
            throw new InvalidOperationException($"Mod 目录不存在：{modRoot}");
        }

        return
        [
            .. Directory.EnumerateFiles(modRoot, "*.csproj", SearchOption.AllDirectories)
                .Where(static projectPath => !IsBuildOutputPath(projectPath))
                .Order(StringComparer.OrdinalIgnoreCase),
        ];
    }

    private static bool IsBuildOutputPath(string path)
    {
        string normalizedPath = path.Replace(Path.DirectorySeparatorChar, '/');
        return normalizedPath.Contains("/bin/", StringComparison.Ordinal)
            || normalizedPath.Contains("/obj/", StringComparison.Ordinal);
    }

    private static string GetSharedProjectFullPath(string sharedRoot, string projectName)
    {
        return Path.Combine(sharedRoot, projectName, $"{projectName}.csproj");
    }

    private static void ValidateNamespaceStyleIdentifier(string value, string valueName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{valueName} 不能为空。");
        }

        foreach (string segment in value.Split('.'))
        {
            if (!SyntaxFacts.IsValidIdentifier(segment) || SyntaxFacts.GetKeywordKind(segment) != SyntaxKind.None)
            {
                throw new ArgumentException($"{valueName} 必须是 C# 命名空间风格的标识符，例如 MyMod 或 MyCompany.MyMod。");
            }
        }
    }

    private static SharedProjectSide ParseSharedProjectSide(string value)
    {
        foreach (SharedProjectSide side in Enum.GetValues<SharedProjectSide>())
        {
            if (string.Equals(value, side.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return side;
            }
        }

        throw new ArgumentException("--side 必须是 Shared、Frontend 或 Backend。");
    }

    private static string GetDefaultSharedProjectTargetFramework(SharedProjectSide side)
    {
        return side switch
        {
            SharedProjectSide.Shared => "netstandard2.1",
            SharedProjectSide.Frontend => "netstandard2.1",
            SharedProjectSide.Backend => "net8.0",
            _ => throw new ArgumentOutOfRangeException(nameof(side)),
        };
    }

    private static string GetRepoRelativePath(string repoRoot, string fullPath)
    {
        if (!IsUnderDirectory(fullPath, repoRoot))
        {
            throw new InvalidOperationException($"项目路径不在仓库根目录下：{fullPath}");
        }

        return Path.GetRelativePath(repoRoot, fullPath).Replace(Path.DirectorySeparatorChar, '/');
    }

    private static bool IsUnderDirectory(string path, string directory)
    {
        string fullPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string fullDirectory = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return fullPath.StartsWith(fullDirectory, StringComparison.OrdinalIgnoreCase);
    }

}

internal enum SharedProjectSide
{
    Shared = 0,
    Frontend = 1,
    Backend = 2,
}
