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
    private const string PluginsDirectoryName = "Plugins";
    private const string SolutionFileName = "Taiwu.Mods.slnx";
    private const string ModsSolutionFolderName = "mods";
    private const string SharedSolutionFolderName = "shared";

    public static async Task<int> Main(string[] args)
    {
        try
        {
            Command command = CommandLineOptions.CreateCommand(RunAsync);
            return await command.Parse(args)
                .InvokeAsync(CreateInvocationConfiguration())
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return 130;
        }
        catch (Exception ex) when (ShouldReportError(ex))
        {
            return await ReportErrorAsync(ex).ConfigureAwait(false);
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
        await Console.Error.WriteLineAsync($"error: {ex.Message}").ConfigureAwait(false);
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
            throw new InvalidOperationException($"Mod directory already exists: {modRoot}. Pass --force to overwrite template files.");
        }

        TemplateDirectory.Create(templateRoot, TemplateRenderer.ForMod(options.Name, DefaultModVersion)).CopyTo(modRoot, options.Force);

        if (!options.SkipSolution && IsUnderDirectory(modRoot, repoRoot))
        {
            await AddProjectsToSolutionAsync(repoRoot, ModsSolutionFolderName, GetModProjectFullPaths(modsRoot, options.Name), cancellationToken)
                .ConfigureAwait(false);
        }

        Console.WriteLine($"Created mod '{options.Name}' at {modRoot}");
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
            throw new InvalidOperationException($"Shared project directory already exists: {projectRoot}. Pass --force to overwrite template files.");
        }

        TemplateDirectory.Create(templateRoot, TemplateRenderer.ForSharedProject(options.Name, side, GetDefaultSharedProjectTargetFramework(side))).CopyTo(projectRoot, options.Force);

        if (!options.SkipSolution && IsUnderDirectory(projectRoot, repoRoot))
        {
            await AddProjectsToSolutionAsync(repoRoot, SharedSolutionFolderName, [GetSharedProjectFullPath(sharedRoot, options.Name)], cancellationToken)
                .ConfigureAwait(false);
        }

        Console.WriteLine($"Created shared project '{options.Name}' at {projectRoot}");
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

        await ProcessRunner.RunAsync("dotnet", repoRoot, ["sln", SolutionFileName, "remove", .. projectPaths], cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task PackModAsync(CommandLineOptions options, CancellationToken cancellationToken)
    {
        ValidateNamespaceStyleIdentifier(options.Name, "ModName");

        string repoRoot = Path.GetFullPath(options.RepoRoot);
        string modsRoot = Path.GetFullPath(options.ModsRoot ?? Path.Combine(repoRoot, DefaultModsRelativePath));
        string artifactsRoot = Path.GetFullPath(options.ArtifactsRoot ?? Path.Combine(repoRoot, "artifacts", "mods"));
        string modRoot = Path.Combine(modsRoot, options.Name);
        string packageRoot = Path.Combine(artifactsRoot, options.Name);

        if (!Directory.Exists(modRoot))
        {
            throw new DirectoryNotFoundException($"Mod directory does not exist: {modRoot}");
        }

        string[] fullProjectPaths = GetModProjectFullPaths(modsRoot, options.Name);
        foreach (string fullProjectPath in fullProjectPaths)
        {
            await ProcessRunner.RunAsync("dotnet", repoRoot, ["build", fullProjectPath, "--configuration", options.Configuration], cancellationToken)
                .ConfigureAwait(false);
        }

        if (Directory.Exists(packageRoot))
        {
            Directory.Delete(packageRoot, recursive: true);
        }

        CopyPackageFiles(modRoot, packageRoot);
        await CopyPluginOutputsAsync(repoRoot, fullProjectPaths, options.Configuration, packageRoot, cancellationToken)
            .ConfigureAwait(false);
        Console.WriteLine($"Packed mod '{options.Name}' to {packageRoot}");
    }

    private static void CopyPackageFiles(string modRoot, string packageRoot)
    {
        foreach (string sourcePath in Directory.EnumerateFiles(modRoot, "*", SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(modRoot, sourcePath);
            if (ShouldExcludeFromPackage(relativePath))
            {
                continue;
            }

            string destinationPath = Path.Combine(packageRoot, relativePath);
            string? destinationDirectory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(destinationDirectory))
            {
                _ = Directory.CreateDirectory(destinationDirectory);
            }

            File.Copy(sourcePath, destinationPath, overwrite: true);
        }
    }

    private static bool ShouldExcludeFromPackage(string relativePath)
    {
        string normalizedPath = relativePath.Replace(Path.DirectorySeparatorChar, '/');
        string fileName = Path.GetFileName(normalizedPath);
        return normalizedPath.StartsWith("src/", StringComparison.Ordinal)
            || normalizedPath.StartsWith($"{PluginsDirectoryName}/", StringComparison.Ordinal)
            || normalizedPath.StartsWith("bin/", StringComparison.Ordinal)
            || normalizedPath.StartsWith("obj/", StringComparison.Ordinal)
            || normalizedPath.Contains("/bin/", StringComparison.Ordinal)
            || normalizedPath.Contains("/obj/", StringComparison.Ordinal)
            || fileName is ".gitignore" or ".gitkeep" or "README.md";
    }

    private static async Task CopyPluginOutputsAsync(
        string repoRoot,
        IEnumerable<string> projectPaths,
        string configuration,
        string packageRoot,
        CancellationToken cancellationToken)
    {
        foreach (string projectPath in projectPaths)
        {
            string outputPath = await GetProjectTargetDirectoryAsync(repoRoot, projectPath, configuration, cancellationToken)
                .ConfigureAwait(false);

            foreach (string sourcePath in Directory.EnumerateFiles(outputPath))
            {
                string extension = Path.GetExtension(sourcePath);
                if (!PackagePluginOutputExtensions.Contains(extension))
                {
                    continue;
                }

                string destinationPath = Path.Combine(packageRoot, PluginsDirectoryName, Path.GetFileName(sourcePath));
                string? destinationDirectory = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(destinationDirectory))
                {
                    _ = Directory.CreateDirectory(destinationDirectory);
                }

                File.Copy(sourcePath, destinationPath, overwrite: true);
            }
        }
    }

    private static async Task<string> GetProjectTargetDirectoryAsync(
        string repoRoot,
        string projectPath,
        string configuration,
        CancellationToken cancellationToken)
    {
        string targetDirectory = await ProcessRunner.RunForOutputAsync(
            "dotnet",
            repoRoot,
            ["msbuild", projectPath, "-getProperty:TargetDir", $"-p:Configuration={configuration}"],
            cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(targetDirectory))
        {
            throw new InvalidOperationException($"Project TargetDir is empty: {projectPath}");
        }

        string projectDirectory = Path.GetDirectoryName(projectPath)
            ?? throw new InvalidOperationException($"Project path has no directory: {projectPath}");

        return Path.GetFullPath(targetDirectory, projectDirectory);
    }

    private static string[] GetModProjectFullPaths(string modsRoot, string modName)
    {
        return
        [
            Path.Combine(modsRoot, modName, "src", "Frontend", $"{modName}.Frontend.csproj"),
            Path.Combine(modsRoot, modName, "src", "Backend", $"{modName}.Backend.csproj"),
        ];
    }

    private static string GetSharedProjectFullPath(string sharedRoot, string projectName)
    {
        return Path.Combine(sharedRoot, projectName, $"{projectName}.csproj");
    }

    private static void ValidateNamespaceStyleIdentifier(string value, string valueName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{valueName} cannot be empty.");
        }

        foreach (string segment in value.Split('.'))
        {
            if (!SyntaxFacts.IsValidIdentifier(segment) || SyntaxFacts.GetKeywordKind(segment) != SyntaxKind.None)
            {
                throw new ArgumentException($"{valueName} must be a C# namespace-style identifier, for example MyMod or MyCompany.MyMod.");
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

        throw new ArgumentException("Shared project side must be Shared, Frontend, or Backend.");
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
            throw new InvalidOperationException($"Project path is outside repository root: {fullPath}");
        }

        return Path.GetRelativePath(repoRoot, fullPath).Replace(Path.DirectorySeparatorChar, '/');
    }

    private static bool IsUnderDirectory(string path, string directory)
    {
        string fullPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string fullDirectory = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return fullPath.StartsWith(fullDirectory, StringComparison.OrdinalIgnoreCase);
    }

    private static readonly HashSet<string> PackagePluginOutputExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".dll",
        ".json",
    };
}

internal enum SharedProjectSide
{
    Shared = 0,
    Frontend = 1,
    Backend = 2,
}
