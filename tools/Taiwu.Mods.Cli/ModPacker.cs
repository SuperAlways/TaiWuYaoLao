using System.Text.Json;

namespace Taiwu.Mods.Cli;

internal sealed class ModPacker(
    string repoRoot,
    string modsRoot,
    string artifactsRoot,
    string configuration)
{
    private const string ILRepackToolRelativePath = "tools/ILRepack.exe";
    private const string PackProjectFileName = "Taiwu.Mod.Pack.proj";
    private const string ResolvePackOutputsTargetName = "ResolveTaiwuModPackOutputs";

    public async Task PackAsync(string modName, CancellationToken cancellationToken)
    {
        string modRoot = Path.Combine(modsRoot, modName);
        string packageRoot = Path.Combine(artifactsRoot, modName);
        string packProjectPath = Path.Combine(modRoot, PackProjectFileName);
        if (!File.Exists(packProjectPath))
        {
            throw new InvalidOperationException($"未找到 mod 组包入口：{packProjectPath}");
        }

        PackPlan plan = await ResolveModPackAsync(packProjectPath, cancellationToken);

        if (Directory.Exists(packageRoot))
        {
            Directory.Delete(packageRoot, recursive: true);
        }

        await WritePackPlanOutputsAsync(plan, packageRoot, cancellationToken);

        Console.WriteLine($"已打包 mod '{modName}'：{packageRoot}");
    }

    private async Task<PackPlan> ResolveModPackAsync(
        string packProjectPath,
        CancellationToken cancellationToken)
    {
        PackPlanBuilder builder = new();
        foreach (PackOutput output in await GetPackOutputsAsync(
            packProjectPath,
            restore: false,
            cancellationToken))
        {
            switch (output.Kind)
            {
                case "Project":
                    builder.Add(await ResolveProjectPackAsync(output.SourcePath, cancellationToken));
                    break;

                case "File":
                    builder.AddFile(CreateFile(output));
                    break;

                case "Directory":
                    builder.AddDirectory(CreateDirectory(output));
                    break;

                default:
                    throw new InvalidOperationException($"mod 组包入口输出了不支持的包产物类型 '{output.Kind}'：{output.SourcePath}");
            }
        }

        return builder.Build();
    }

    private async Task<PackPlan> ResolveProjectPackAsync(
        string projectPath,
        CancellationToken cancellationToken)
    {
        PackPlanBuilder builder = new();
        PackAssemblyInput? assemblyInput = null;
        List<PackFile> mergeDependencies = [];
        List<string> libraryPaths = [];

        foreach (PackOutput output in await GetPackOutputsAsync(
            projectPath,
            restore: true,
            cancellationToken))
        {
            switch (output.Kind)
            {
                case "Entry":
                    if (assemblyInput is not null)
                    {
                        throw new InvalidOperationException($"项目声明了多个组包入口程序集：{projectPath}");
                    }

                    assemblyInput = new PackAssemblyInput(CreateFile(output), CreateOptions(output));
                    break;

                case "Merge":
                    mergeDependencies.Add(CreateFile(output, packagePath: string.Empty));
                    break;

                case "Library":
                    libraryPaths.Add(output.SourcePath);
                    break;

                case "File":
                    builder.AddFile(CreateFile(output));
                    break;

                case "Directory":
                    builder.AddDirectory(CreateDirectory(output));
                    break;

                default:
                    throw new InvalidOperationException($"项目输出了不支持的包产物类型 '{output.Kind}'：{output.SourcePath}");
            }
        }

        if (assemblyInput is null)
        {
            if (mergeDependencies.Count != 0)
            {
                throw new InvalidOperationException($"项目声明了需要合并的依赖，但没有声明 TaiwuModPackEntry：{projectPath}");
            }
        }
        else
        {
            builder.AddAssembly(
                new PackAssembly(
                    assemblyInput.Entry,
                    mergeDependencies,
                    [.. libraryPaths.Distinct(StringComparer.OrdinalIgnoreCase)],
                    assemblyInput.Options));
        }

        return builder.Build();
    }

    private async Task<IReadOnlyList<PackOutput>> GetPackOutputsAsync(
        string projectPath,
        bool restore,
        CancellationToken cancellationToken)
    {
        string target = restore
            ? $"Restore;{ResolvePackOutputsTargetName}"
            : ResolvePackOutputsTargetName;
        string output = await ProcessRunner.RunForOutputAsync(
            "dotnet",
            repoRoot,
            [
                "msbuild",
                projectPath,
                $"-t:{target}",
                $"-getTargetResult:{ResolvePackOutputsTargetName}",
                $"-p:Configuration={configuration}",
            ],
            cancellationToken);

        return ReadPackOutputs(output, projectPath);
    }

    private static List<PackOutput> ReadPackOutputs(string json, string projectPath)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement targetResult = document.RootElement
            .GetProperty("TargetResults")
            .GetProperty(ResolvePackOutputsTargetName);
        string result = GetRequiredString(targetResult, "Result");
        if (!string.Equals(result, "Success", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"MSBuild 目标执行失败：{projectPath}: {ResolvePackOutputsTargetName}: {result}");
        }

        if (!targetResult.TryGetProperty("Items", out JsonElement items))
        {
            return [];
        }

        List<PackOutput> outputs = [];
        foreach (JsonElement item in items.EnumerateArray())
        {
            Dictionary<string, string> metadata = CreateMetadata(item);
            outputs.Add(
                new PackOutput(
                    metadata["SourcePath"],
                    metadata["Kind"],
                    metadata.GetValueOrDefault("PackagePath"),
                    metadata));
        }

        return outputs;
    }

    private async Task WritePackPlanOutputsAsync(
        PackPlan plan,
        string packageRoot,
        CancellationToken cancellationToken)
    {
        foreach (PackAssembly assembly in plan.Assemblies)
        {
            if (assembly.MergeDependencies.Count == 0)
            {
                CopyPackFile(assembly.Entry, packageRoot);
            }
            else
            {
                await MergeEntryAssemblyAsync(assembly, packageRoot, cancellationToken);
            }
        }

        foreach (PackFile file in plan.Files)
        {
            CopyPackFile(file, packageRoot);
        }

        foreach (PackDirectory directory in plan.Directories)
        {
            CopyPackDirectory(directory, packageRoot);
        }
    }

    private async Task MergeEntryAssemblyAsync(
        PackAssembly assembly,
        string packageRoot,
        CancellationToken cancellationToken)
    {
        string outputPath = GetPackageDestinationPath(assembly.Entry, packageRoot);
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

        if (assembly.Options.InternalizeMergedDependencies)
        {
            arguments.Add("/internalize");
            if (assembly.Options.RenameInternalizedDependencies)
            {
                arguments.Add("/renameinternalized");
            }
        }

        if (assembly.Options.AllowDuplicateInternalizedResources)
        {
            arguments.Add("/allowduplicateresources");
        }

        if (!string.IsNullOrEmpty(assembly.Options.KeyFile))
        {
            arguments.Add($"/keyfile:{assembly.Options.KeyFile}");
        }

        foreach (string libraryPath in assembly.LibraryPaths)
        {
            arguments.Add($"/lib:{libraryPath}");
        }

        arguments.Add(assembly.Entry.SourcePath);
        arguments.AddRange(assembly.MergeDependencies.Select(static dependency => dependency.SourcePath));

        await ProcessRunner.RunAsync("dotnet", repoRoot, arguments, cancellationToken);
    }

    private static void CopyPackFile(PackFile file, string packageRoot)
    {
        CopyFile(file.SourcePath, GetPackageDestinationPath(file, packageRoot));
    }

    private static void CopyPackDirectory(PackDirectory directory, string packageRoot)
    {
        string destinationRoot = GetPackageDestinationPath(directory, packageRoot);
        foreach (string sourcePath in Directory.EnumerateFiles(directory.SourcePath, "*", SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(directory.SourcePath, sourcePath);
            CopyFile(sourcePath, Path.Combine(destinationRoot, relativePath));
        }
    }

    private static void CopyFile(string sourcePath, string destinationPath)
    {
        string? destinationDirectory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(destinationDirectory))
        {
            _ = Directory.CreateDirectory(destinationDirectory);
        }

        File.Copy(sourcePath, destinationPath, overwrite: false);
    }

    private static PackFile CreateFile(PackOutput output, string? packagePath = null)
    {
        return new PackFile(
            output.SourcePath,
            packagePath ?? NormalizePackagePath(output.PackagePath ?? string.Empty, output.SourcePath));
    }

    private static PackDirectory CreateDirectory(PackOutput output)
    {
        return new PackDirectory(
            output.SourcePath,
            NormalizePackagePath(output.PackagePath ?? string.Empty, output.SourcePath));
    }

    private static PackAssemblyOptions CreateOptions(PackOutput output)
    {
        return new PackAssemblyOptions(
            bool.Parse(GetRequiredMetadata(output, "InternalizeMergedDependencies")),
            bool.Parse(GetRequiredMetadata(output, "RenameInternalizedDependencies")),
            bool.Parse(GetRequiredMetadata(output, "AllowDuplicateInternalizedResources")),
            output.Metadata.GetValueOrDefault("KeyFile"));
    }

    private static string GetPackageDestinationPath(PackPlanOutput output, string packageRoot)
    {
        string destinationPath = Path.GetFullPath(
            NormalizePathSeparators(output.PackagePath),
            packageRoot);
        string fullPackageRoot = Path.GetFullPath(packageRoot);
        if (!IsUnderDirectoryOrSame(destinationPath, fullPackageRoot))
        {
            throw new InvalidOperationException($"包产物路径越过可部署目录：{output.SourcePath}: {output.PackagePath}");
        }

        return destinationPath;
    }

    private static string NormalizePackagePath(string packagePath, string sourcePath)
    {
        string normalizedPath = packagePath.Replace('\\', '/').Trim('/');
        if (normalizedPath.Length == 0)
        {
            throw new InvalidOperationException($"包产物 PackagePath 不能为空：{sourcePath}");
        }

        if (Path.IsPathRooted(packagePath) || normalizedPath.Contains(':', StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"包产物 PackagePath 必须是相对路径：{sourcePath}: {packagePath}");
        }

        foreach (string segment in normalizedPath.Split('/'))
        {
            if (segment.Length == 0 || segment is "." or "..")
            {
                throw new InvalidOperationException($"包产物 PackagePath 无效：{sourcePath}: {packagePath}");
            }
        }

        return normalizedPath;
    }

    private static Dictionary<string, string> CreateMetadata(JsonElement item)
    {
        Dictionary<string, string> metadata = new(StringComparer.OrdinalIgnoreCase);
        foreach (JsonProperty property in item.EnumerateObject())
        {
            metadata[property.Name] = property.Value.ValueKind == JsonValueKind.String
                ? property.Value.GetString() ?? string.Empty
                : property.Value.ToString();
        }

        return metadata;
    }

    private static string GetRequiredString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement property))
        {
            throw new InvalidOperationException($"MSBuild JSON 输出缺少 '{propertyName}'。");
        }

        return property.GetString() ?? string.Empty;
    }

    private static string GetRequiredMetadata(PackOutput output, string name)
    {
        return output.Metadata.TryGetValue(name, out string? value)
            ? value
            : throw new InvalidOperationException($"包产物缺少 '{name}'：{output.SourcePath}");
    }

    private static bool IsUnderDirectoryOrSame(string path, string directory)
    {
        string fullPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string fullDirectory = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return string.Equals(fullPath, fullDirectory, StringComparison.OrdinalIgnoreCase)
            || fullPath.StartsWith($"{fullDirectory}{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetILRepackToolPath()
    {
        return Path.Combine(AppContext.BaseDirectory, ILRepackToolRelativePath);
    }

    private static string NormalizePathSeparators(string path)
    {
        return path.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
    }

    private sealed class PackPlanBuilder
    {
        private readonly List<PackAssembly> _assemblies = [];
        private readonly List<PackFile> _files = [];
        private readonly List<PackDirectory> _directories = [];

        public void Add(PackPlan plan)
        {
            _assemblies.AddRange(plan.Assemblies);
            _files.AddRange(plan.Files);
            _directories.AddRange(plan.Directories);
        }

        public void AddAssembly(PackAssembly assembly)
        {
            _assemblies.Add(assembly);
        }

        public void AddFile(PackFile file)
        {
            _files.Add(file);
        }

        public void AddDirectory(PackDirectory directory)
        {
            _directories.Add(directory);
        }

        public PackPlan Build()
        {
            return new PackPlan(_assemblies, _files, _directories);
        }
    }

    private sealed record PackOutput(
        string SourcePath,
        string Kind,
        string? PackagePath,
        IReadOnlyDictionary<string, string> Metadata);

    private sealed record PackPlan(
        IReadOnlyList<PackAssembly> Assemblies,
        IReadOnlyList<PackFile> Files,
        IReadOnlyList<PackDirectory> Directories);

    private sealed record PackAssembly(
        PackFile Entry,
        IReadOnlyList<PackFile> MergeDependencies,
        IReadOnlyList<string> LibraryPaths,
        PackAssemblyOptions Options);

    private sealed record PackAssemblyInput(
        PackFile Entry,
        PackAssemblyOptions Options);

    private abstract record PackPlanOutput(
        string SourcePath,
        string PackagePath);

    private sealed record PackFile(
        string SourcePath,
        string PackagePath)
        : PackPlanOutput(SourcePath, PackagePath);

    private sealed record PackDirectory(
        string SourcePath,
        string PackagePath)
        : PackPlanOutput(SourcePath, PackagePath);

    private sealed record PackAssemblyOptions(
        bool InternalizeMergedDependencies,
        bool RenameInternalizedDependencies,
        bool AllowDuplicateInternalizedResources,
        string? KeyFile);
}
