using System.ComponentModel;
using System.CommandLine;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Taiwu.Mods.Cli;

internal static partial class Program
{
    private const string ModNameToken = "__ModName__";
    private const string MarkdownModNameToken = "{{ModName}}";
    private const string ProjectNameToken = "__ProjectName__";
    private const string MarkdownProjectNameToken = "{{ProjectName}}";
    private const string TargetFrameworkToken = "__TargetFramework__";
    private const string DefaultModTemplateRelativePath = "templates/mod";
    private const string DefaultSharedTemplateRelativePath = "templates/shared";
    private const string DefaultModsRelativePath = "mods";
    private const string DefaultSharedRelativePath = "shared";
    private const string PluginsDirectoryName = "Plugins";
    private const string SolutionFileName = "Taiwu.Mods.slnx";
    private const string ModsSolutionFolderName = "/mods/";
    private const string SharedSolutionFolderName = "/shared/";

    public static int Main(string[] args)
    {
        try
        {
            Command command = CommandLineOptions.CreateCommand(Run);
            return command.Parse(args).Invoke(CreateInvocationConfiguration());
        }
        catch (ArgumentException ex)
        {
            return ReportError(ex);
        }
        catch (IOException ex)
        {
            return ReportError(ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            return ReportError(ex);
        }
        catch (InvalidOperationException ex)
        {
            return ReportError(ex);
        }
        catch (Win32Exception ex)
        {
            return ReportError(ex);
        }
    }

    private static InvocationConfiguration CreateInvocationConfiguration()
    {
        return new InvocationConfiguration
        {
            EnableDefaultExceptionHandler = false,
        };
    }

    private static int ReportError(Exception ex)
    {
        Console.Error.WriteLine($"error: {ex.Message}");
        return 1;
    }

    private static void Run(CommandLineOptions options)
    {
        switch (options.Operation)
        {
            case CliOperation.CreateMod:
                CreateMod(options);
                break;
            case CliOperation.RemoveMod:
                RemoveMod(options);
                break;
            case CliOperation.PackMod:
                PackMod(options);
                break;
            case CliOperation.CreateShared:
                CreateSharedProject(options);
                break;
            case CliOperation.RemoveShared:
                RemoveSharedProject(options);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(options));
        }
    }

    private static void CreateMod(CommandLineOptions options)
    {
        ValidateNamespaceStyleIdentifier(options.Name, "ModName");

        string repoRoot = Path.GetFullPath(options.RepoRoot);
        string templateRoot = Path.Combine(repoRoot, DefaultModTemplateRelativePath);
        string modsRoot = Path.GetFullPath(options.ModsRoot ?? Path.Combine(repoRoot, DefaultModsRelativePath));
        string modRoot = Path.Combine(modsRoot, options.Name);

        if (!Directory.Exists(templateRoot))
        {
            throw new DirectoryNotFoundException($"Template directory does not exist: {templateRoot}");
        }

        if (Directory.Exists(modRoot) && !options.Force)
        {
            throw new InvalidOperationException($"Mod directory already exists: {modRoot}. Pass --force to overwrite template files.");
        }

        CopyTemplate(templateRoot, modRoot, CreateModTemplateReplacements(options.Name), options.Force);

        if (!options.SkipSolution && IsUnderDirectory(modRoot, repoRoot))
        {
            AddProjectsToSolution(repoRoot, GetModProjectFullPaths(modsRoot, options.Name));
        }

        Console.WriteLine($"Created mod '{options.Name}' at {modRoot}");
    }

    private static void CreateSharedProject(CommandLineOptions options)
    {
        ValidateNamespaceStyleIdentifier(options.Name, "ProjectName");
        SharedProjectSide side = ParseSharedProjectSide(options.SharedSide);

        string repoRoot = Path.GetFullPath(options.RepoRoot);
        string templateRoot = Path.Combine(repoRoot, DefaultSharedTemplateRelativePath);
        string sharedRoot = Path.GetFullPath(options.SharedRoot ?? Path.Combine(repoRoot, DefaultSharedRelativePath));
        string projectRoot = Path.Combine(sharedRoot, options.Name);

        if (!Directory.Exists(templateRoot))
        {
            throw new DirectoryNotFoundException($"Template directory does not exist: {templateRoot}");
        }

        if (Directory.Exists(projectRoot) && !options.Force)
        {
            throw new InvalidOperationException($"Shared project directory already exists: {projectRoot}. Pass --force to overwrite template files.");
        }

        CopyTemplate(templateRoot, projectRoot, CreateSharedProjectTemplateReplacements(options.Name, side), options.Force);

        if (!options.SkipSolution && IsUnderDirectory(projectRoot, repoRoot))
        {
            AddProjectsToSolution(repoRoot, [GetSharedProjectFullPath(sharedRoot, options.Name)]);
        }

        Console.WriteLine($"Created shared project '{options.Name}' at {projectRoot}");
    }

    private static void CopyTemplate(string templateRoot, string destinationRoot, IReadOnlyDictionary<string, string> replacements, bool force)
    {
        foreach (string templateFile in Directory.EnumerateFiles(templateRoot, "*", SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(templateRoot, templateFile);
            string destinationRelativePath = ReplaceTokens(relativePath, replacements);
            string destinationPath = Path.Combine(destinationRoot, destinationRelativePath);

            if (File.Exists(destinationPath) && !force)
            {
                throw new IOException($"Destination file already exists: {destinationPath}");
            }

            string? destinationDirectory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(destinationDirectory))
            {
                _ = Directory.CreateDirectory(destinationDirectory);
            }

            if (ShouldReplaceTokens(templateFile))
            {
                string content = File.ReadAllText(templateFile, Encoding.UTF8);
                File.WriteAllText(destinationPath, ReplaceTokens(content, replacements), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                continue;
            }

            File.Copy(templateFile, destinationPath, overwrite: force);
        }
    }

    private static void AddProjectsToSolution(string repoRoot, IEnumerable<string> fullProjectPaths)
    {
        string solutionPath = Path.Combine(repoRoot, SolutionFileName);
        if (!File.Exists(solutionPath))
        {
            throw new FileNotFoundException($"Solution file does not exist: {solutionPath}");
        }

        string[] projectPaths =
        [
            .. fullProjectPaths.Select(fullProjectPath => GetRepoRelativePath(repoRoot, fullProjectPath)),
        ];

        RunDotnet(repoRoot, ["sln", SolutionFileName, "add", .. projectPaths]);
        EnsureStandardSolutionFolders(repoRoot);
    }

    private static void RemoveMod(CommandLineOptions options)
    {
        ValidateNamespaceStyleIdentifier(options.Name, "ModName");

        string repoRoot = Path.GetFullPath(options.RepoRoot);
        string modsRoot = Path.GetFullPath(options.ModsRoot ?? Path.Combine(repoRoot, DefaultModsRelativePath));
        RemoveProjectsFromSolution(repoRoot, "mod", options.Name, GetModProjectFullPaths(modsRoot, options.Name));
    }

    private static void RemoveSharedProject(CommandLineOptions options)
    {
        ValidateNamespaceStyleIdentifier(options.Name, "ProjectName");

        string repoRoot = Path.GetFullPath(options.RepoRoot);
        string sharedRoot = Path.GetFullPath(options.SharedRoot ?? Path.Combine(repoRoot, DefaultSharedRelativePath));
        RemoveProjectsFromSolution(repoRoot, "shared project", options.Name, [GetSharedProjectFullPath(sharedRoot, options.Name)]);
    }

    private static void RemoveProjectsFromSolution(string repoRoot, string projectKind, string projectName, IEnumerable<string> fullProjectPaths)
    {
        string solutionPath = Path.Combine(repoRoot, SolutionFileName);
        if (!File.Exists(solutionPath))
        {
            throw new FileNotFoundException($"Solution file does not exist: {solutionPath}");
        }

        List<string> existingProjectPaths = [];
        foreach (string fullProjectPath in fullProjectPaths)
        {
            string projectPath = GetRepoRelativePath(repoRoot, fullProjectPath);
            if (File.Exists(fullProjectPath))
            {
                existingProjectPaths.Add(projectPath);
            }
            else
            {
                Console.WriteLine($"Skipped missing project file: {projectPath}");
            }
        }

        if (existingProjectPaths.Count == 0)
        {
            Console.WriteLine($"No solution projects found for {projectKind} '{projectName}'.");
            return;
        }

        RunDotnet(repoRoot, ["sln", SolutionFileName, "remove", .. existingProjectPaths]);
        EnsureStandardSolutionFolders(repoRoot);
        Console.WriteLine($"Removed {projectKind} '{projectName}' projects from {SolutionFileName}. Files were not deleted.");
    }

    private static void PackMod(CommandLineOptions options)
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
            if (!File.Exists(fullProjectPath))
            {
                throw new FileNotFoundException($"Mod project does not exist: {fullProjectPath}");
            }
        }

        foreach (string fullProjectPath in fullProjectPaths)
        {
            RunDotnet(repoRoot, "build", fullProjectPath, "--configuration", options.Configuration);
        }

        if (Directory.Exists(packageRoot))
        {
            Directory.Delete(packageRoot, recursive: true);
        }

        CopyPackageFiles(modRoot, packageRoot);
        CopyPluginOutputs(repoRoot, fullProjectPaths, options.Configuration, packageRoot);
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

    private static void CopyPluginOutputs(string repoRoot, IEnumerable<string> projectPaths, string configuration, string packageRoot)
    {
        foreach (string projectPath in projectPaths)
        {
            string outputPath = GetProjectTargetDirectory(repoRoot, projectPath, configuration);
            if (!Directory.Exists(outputPath))
            {
                throw new DirectoryNotFoundException($"Project output directory does not exist: {outputPath}");
            }

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

    private static string GetProjectTargetDirectory(string repoRoot, string projectPath, string configuration)
    {
        string targetDirectory = RunDotnetForOutput(
            repoRoot,
            "msbuild",
            projectPath,
            "-getProperty:TargetDir",
            $"-p:Configuration={configuration}");

        if (string.IsNullOrWhiteSpace(targetDirectory))
        {
            throw new InvalidOperationException($"Project TargetDir is empty: {projectPath}");
        }

        string projectDirectory = Path.GetDirectoryName(projectPath)
            ?? throw new InvalidOperationException($"Project path has no directory: {projectPath}");

        return Path.GetFullPath(targetDirectory, projectDirectory);
    }

    private static void EnsureSolutionFolder(string repoRoot, string folderName)
    {
        string solutionPath = Path.Combine(repoRoot, SolutionFileName);
        XDocument document = XDocument.Load(solutionPath);
        XElement root = document.Root ?? throw new InvalidOperationException($"Solution file has no root element: {solutionPath}");

        if (root.Elements("Folder").Any(element => string.Equals((string?)element.Attribute("Name"), folderName, StringComparison.Ordinal)))
        {
            return;
        }

        XElement folder = new("Folder", new XAttribute("Name", folderName));
        XElement? insertBefore = root.Elements("Folder").FirstOrDefault(element =>
            string.CompareOrdinal((string?)element.Attribute("Name"), folderName) > 0);

        if (insertBefore is null)
        {
            root.Add(folder);
        }
        else
        {
            insertBefore.AddBeforeSelf(folder);
        }

        File.WriteAllText(solutionPath, $"{document}{Environment.NewLine}", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static void EnsureStandardSolutionFolders(string repoRoot)
    {
        EnsureSolutionFolder(repoRoot, ModsSolutionFolderName);
        EnsureSolutionFolder(repoRoot, SharedSolutionFolderName);
    }

    private static bool ShouldReplaceTokens(string templateFile)
    {
        return TextTemplateExtensions.Contains(Path.GetExtension(templateFile));
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

    private static void RunDotnet(string workingDirectory, params string[] arguments)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = "dotnet",
            WorkingDirectory = workingDirectory,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };

        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start dotnet.");

        string standardOutput = process.StandardOutput.ReadToEnd();
        string standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (!string.IsNullOrWhiteSpace(standardOutput))
        {
            Console.Write(standardOutput);
        }

        if (!string.IsNullOrWhiteSpace(standardError))
        {
            Console.Error.Write(standardError);
        }

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"dotnet {string.Join(' ', arguments)} failed with exit code {process.ExitCode}.");
        }
    }

    private static string RunDotnetForOutput(string workingDirectory, params string[] arguments)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = "dotnet",
            WorkingDirectory = workingDirectory,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };

        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start dotnet.");

        string standardOutput = process.StandardOutput.ReadToEnd();
        string standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (!string.IsNullOrWhiteSpace(standardError))
        {
            Console.Error.Write(standardError);
        }

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"dotnet {string.Join(' ', arguments)} failed with exit code {process.ExitCode}.");
        }

        return standardOutput.Trim();
    }

    private static Dictionary<string, string> CreateModTemplateReplacements(string modName)
    {
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ModNameToken] = modName,
            [MarkdownModNameToken] = modName,
        };
    }

    private static Dictionary<string, string> CreateSharedProjectTemplateReplacements(string projectName, SharedProjectSide side)
    {
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ProjectNameToken] = projectName,
            [MarkdownProjectNameToken] = projectName,
            [TargetFrameworkToken] = GetDefaultSharedProjectTargetFramework(side),
        };
    }

    private static string ReplaceTokens(string value, IReadOnlyDictionary<string, string> replacements)
    {
        string result = value;
        foreach (KeyValuePair<string, string> replacement in replacements)
        {
            result = result.Replace(replacement.Key, replacement.Value, StringComparison.Ordinal);
        }

        return result;
    }

    private static void ValidateNamespaceStyleIdentifier(string value, string valueName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{valueName} cannot be empty.");
        }

        if (!NamespaceStyleIdentifierRegex().IsMatch(value))
        {
            throw new ArgumentException($"{valueName} must be a C# namespace-style identifier, for example MyMod or MyCompany.MyMod.");
        }

        foreach (string segment in value.Split('.'))
        {
            if (CSharpKeywords.Contains(segment))
            {
                throw new ArgumentException($"{valueName} segment '{segment}' is a C# keyword.");
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

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_]*(\.[A-Za-z_][A-Za-z0-9_]*)*$")]
    private static partial Regex NamespaceStyleIdentifierRegex();

    private static readonly HashSet<string> TextTemplateExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".config",
        ".cs",
        ".csproj",
        ".json",
        ".lua",
        ".md",
        ".proj",
        ".props",
        ".slnx",
        ".toml",
        ".txt",
        ".xml",
        ".yaml",
        ".yml",
    };

    private static readonly HashSet<string> PackagePluginOutputExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".dll",
        ".json",
    };

    private static readonly HashSet<string> CSharpKeywords = new(StringComparer.Ordinal)
    {
        "abstract",
        "as",
        "base",
        "bool",
        "break",
        "byte",
        "case",
        "catch",
        "char",
        "checked",
        "class",
        "const",
        "continue",
        "decimal",
        "default",
        "delegate",
        "do",
        "double",
        "else",
        "enum",
        "event",
        "explicit",
        "extern",
        "false",
        "finally",
        "fixed",
        "float",
        "for",
        "foreach",
        "goto",
        "if",
        "implicit",
        "in",
        "int",
        "interface",
        "internal",
        "is",
        "lock",
        "long",
        "namespace",
        "new",
        "null",
        "object",
        "operator",
        "out",
        "override",
        "params",
        "private",
        "protected",
        "public",
        "readonly",
        "ref",
        "return",
        "sbyte",
        "sealed",
        "short",
        "sizeof",
        "stackalloc",
        "static",
        "string",
        "struct",
        "switch",
        "this",
        "throw",
        "true",
        "try",
        "typeof",
        "uint",
        "ulong",
        "unchecked",
        "unsafe",
        "ushort",
        "using",
        "virtual",
        "void",
        "volatile",
        "while",
    };
}

internal enum SharedProjectSide
{
    Shared = 0,
    Frontend = 1,
    Backend = 2,
}
