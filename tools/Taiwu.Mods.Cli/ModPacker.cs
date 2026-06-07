namespace Taiwu.Mods.Cli;

internal sealed class ModPacker(
    string repoRoot,
    string modsRoot,
    string artifactsRoot,
    string configuration)
{
    private const string PluginsDirectoryName = "Plugins";
    private const string ILRepackToolRelativePath = "tools/ILRepack.exe";

    public async Task PackAsync(string modName, CancellationToken cancellationToken)
    {
        string modRoot = Path.Combine(modsRoot, modName);
        string packageRoot = Path.Combine(artifactsRoot, modName);

        List<PackManifest> manifests = [];
        foreach (string projectPath in GetModProjectFullPaths(modName))
        {
            PackManifest manifest = await GenerateProjectPackManifestAsync(projectPath, cancellationToken).ConfigureAwait(false);
            manifests.Add(manifest);
        }

        if (Directory.Exists(packageRoot))
        {
            Directory.Delete(packageRoot, recursive: true);
        }

        CopyPackageFiles(modRoot, packageRoot);
        foreach (PackManifest manifest in manifests)
        {
            await WritePackManifestOutputsAsync(manifest, packageRoot, cancellationToken).ConfigureAwait(false);
        }

        Console.WriteLine($"已打包 mod '{modName}'：{packageRoot}");
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

            CopyFile(sourcePath, Path.Combine(packageRoot, relativePath));
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

    private async Task<PackManifest> GenerateProjectPackManifestAsync(string projectPath, CancellationToken cancellationToken)
    {
        string manifestPath = await GetProjectPackManifestPathAsync(projectPath, cancellationToken).ConfigureAwait(false);
        await ProcessRunner.RunAsync(
            "dotnet",
            repoRoot,
            ["msbuild", projectPath, "-restore", "-t:GenerateTaiwuModPackManifest", $"-p:Configuration={configuration}"],
            cancellationToken).ConfigureAwait(false);

        return ReadPackManifest(manifestPath);
    }

    private async Task<string> GetProjectPackManifestPathAsync(string projectPath, CancellationToken cancellationToken)
    {
        string manifestPath = await ProcessRunner.RunForOutputAsync(
            "dotnet",
            repoRoot,
            ["msbuild", projectPath, "-getProperty:TaiwuModPackManifestFile", $"-p:Configuration={configuration}"],
            cancellationToken).ConfigureAwait(false);

        return Path.GetFullPath(NormalizePathSeparators(manifestPath), Path.GetDirectoryName(projectPath)!);
    }

    private static PackManifest ReadPackManifest(string manifestPath)
    {
        PackManifestBuilder builder = new(manifestPath);
        int lineNumber = 0;
        foreach (string line in File.ReadLines(manifestPath))
        {
            lineNumber++;
            string[] fields = line.TrimStart('\uFEFF').Split('|');
            builder.Add(fields[0], fields[1], fields[2], lineNumber);
        }

        return builder.Build();
    }

    private async Task WritePackManifestOutputsAsync(
        PackManifest manifest,
        string packageRoot,
        CancellationToken cancellationToken)
    {
        if (manifest.MergeDependencies.Count == 0)
        {
            CopyManifestFile(manifest.Entry, packageRoot);
        }
        else
        {
            await MergeEntryAssemblyAsync(manifest, packageRoot, cancellationToken).ConfigureAwait(false);
        }

        foreach (PackManifestFile dependency in manifest.CopyDependencies)
        {
            CopyManifestFile(dependency, packageRoot);
        }
    }

    private async Task MergeEntryAssemblyAsync(
        PackManifest manifest,
        string packageRoot,
        CancellationToken cancellationToken)
    {
        string outputPath = GetPackageDestinationPath(manifest.Entry, packageRoot);
        string? outputDirectory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDirectory))
        {
            _ = Directory.CreateDirectory(outputDirectory);
        }

        List<string> arguments =
        [
            GetILRepackToolPath(),
            "/ndebug",
            "/parallel",
            $"/out:{outputPath}",
        ];

        if (manifest.Options.InternalizeMergedDependencies)
        {
            arguments.Add("/internalize");
            if (manifest.Options.RenameInternalizedDependencies)
            {
                arguments.Add("/renameinternalized");
            }
        }

        if (manifest.Options.AllowDuplicateInternalizedResources)
        {
            arguments.Add("/allowduplicateresources");
        }

        if (!string.IsNullOrEmpty(manifest.Options.KeyFile))
        {
            arguments.Add($"/keyfile:{manifest.Options.KeyFile}");
        }

        foreach (string libraryPath in manifest.LibraryPaths)
        {
            arguments.Add($"/lib:{libraryPath}");
        }

        arguments.Add(manifest.Entry.SourcePath);
        arguments.AddRange(manifest.MergeDependencies.Select(static dependency => dependency.SourcePath));

        await ProcessRunner.RunAsync("dotnet", repoRoot, arguments, cancellationToken).ConfigureAwait(false);
    }

    private static void CopyManifestFile(PackManifestFile entry, string packageRoot)
    {
        CopyFile(entry.SourcePath, GetPackageDestinationPath(entry, packageRoot));
    }

    private static void CopyFile(string sourcePath, string destinationPath)
    {
        string? destinationDirectory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(destinationDirectory))
        {
            _ = Directory.CreateDirectory(destinationDirectory);
        }

        File.Copy(sourcePath, destinationPath, overwrite: true);
    }

    private static string GetPackageDestinationPath(PackManifestFile entry, string packageRoot)
    {
        return Path.GetFullPath(entry.PackagePath, packageRoot);
    }

    private string[] GetModProjectFullPaths(string modName)
    {
        return
        [
            Path.Combine(modsRoot, modName, "src", "Frontend", $"{modName}.Frontend.csproj"),
            Path.Combine(modsRoot, modName, "src", "Backend", $"{modName}.Backend.csproj"),
        ];
    }

    private static string GetILRepackToolPath()
    {
        return Path.Combine(AppContext.BaseDirectory, ILRepackToolRelativePath);
    }

    private static string NormalizePathSeparators(string path)
    {
        return path.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
    }

    private sealed class PackManifestBuilder(string manifestPath)
    {
        private readonly List<PackManifestFile> _mergeDependencies = [];
        private readonly List<PackManifestFile> _copyDependencies = [];
        private readonly List<string> _libraryPaths = [];
        private readonly Dictionary<string, string> _options = new(StringComparer.OrdinalIgnoreCase);
        private PackManifestFile? _entry;

        public void Add(string kind, string firstValue, string secondValue, int lineNumber)
        {
            switch (kind)
            {
                case "entry":
                    SetEntry(firstValue, secondValue, lineNumber);
                    break;

                case "merge":
                    _mergeDependencies.Add(CreateFile(firstValue, string.Empty, lineNumber));
                    break;

                case "copy":
                    _copyDependencies.Add(CreateFile(firstValue, secondValue, lineNumber));
                    break;

                case "library":
                    _libraryPaths.Add(Path.GetFullPath(NormalizePathSeparators(firstValue)));
                    break;

                case "option":
                    _options[firstValue] = secondValue;
                    break;

                default:
                    throw new InvalidOperationException($"未知 pack manifest 记录类型：{manifestPath}:{lineNumber}: {kind}");
            }
        }

        public PackManifest Build()
        {
            PackManifestFile entry = _entry
                ?? throw new InvalidOperationException($"pack manifest 缺少 entry 记录：{manifestPath}");

            return new PackManifest(entry, _mergeDependencies, _copyDependencies, [.. _libraryPaths.Distinct(StringComparer.OrdinalIgnoreCase)], CreateOptions());
        }

        private void SetEntry(string sourcePath, string packagePath, int lineNumber)
        {
            if (_entry is not null)
            {
                throw new InvalidOperationException($"pack manifest 包含多个 entry 记录：{manifestPath}");
            }

            _entry = CreateFile(sourcePath, packagePath, lineNumber);
        }

        private PackManifestFile CreateFile(string sourcePath, string packagePath, int lineNumber)
        {
            return new PackManifestFile(
                Path.GetFullPath(NormalizePathSeparators(sourcePath)),
                packagePath,
                manifestPath,
                lineNumber);
        }

        private PackManifestOptions CreateOptions()
        {
            return new PackManifestOptions(
                bool.Parse(_options["InternalizeMergedDependencies"]),
                bool.Parse(_options["RenameInternalizedDependencies"]),
                bool.Parse(_options["AllowDuplicateInternalizedResources"]),
                _options.GetValueOrDefault("KeyFile"));
        }
    }

    private sealed record PackManifest(
        PackManifestFile Entry,
        IReadOnlyList<PackManifestFile> MergeDependencies,
        IReadOnlyList<PackManifestFile> CopyDependencies,
        IReadOnlyList<string> LibraryPaths,
        PackManifestOptions Options);

    private sealed record PackManifestFile(
        string SourcePath,
        string PackagePath,
        string ManifestPath,
        int LineNumber);

    private sealed record PackManifestOptions(
        bool InternalizeMergedDependencies,
        bool RenameInternalizedDependencies,
        bool AllowDuplicateInternalizedResources,
        string? KeyFile);
}
