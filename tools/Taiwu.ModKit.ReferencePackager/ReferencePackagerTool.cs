using Taiwu.ModKit.Tooling;
using Taiwu.ModKit.Tooling.NuGet;

namespace Taiwu.ModKit.ReferencePackager;

internal static class ReferencePackagerTool
{
    public static Task RunAsync(CommandLineOptions options, CancellationToken cancellationToken)
    {
        if (options.Command == ToolCommand.Pack)
        {
            return PackAsync(options, cancellationToken);
        }

        return PublishAsync(options, cancellationToken);
    }

    private static async Task PackAsync(CommandLineOptions options, CancellationToken cancellationToken)
    {
        PackToolContext context = ToolEnvironment.LoadForPack();
        PackagingConfig config = PackagingConfig.Load(context.ConfigPath);
        string version = PackageVersions.RequireNuGetPackageVersion(options.Version);
        string outputRoot = WorkspacePaths.ResolveRepoRelativePath(config.RequiredOutputRoot(), context.RepoRoot, "outputRoot");
        foreach (ReferencePackage package in ReferencePackageBuilder.Build(context.GameDir, outputRoot, version, config))
        {
            await NuGetPackageWriter.WriteAsync(package.PackageSpec, cancellationToken);
            Console.WriteLine($"已打包: {package.Id} {package.Version}: {package.AssetGroups.Length} 个资产组，{package.AssemblyCount} 个 DLL -> {package.OutputPath}");
        }
    }

    private static Task PublishAsync(CommandLineOptions options, CancellationToken cancellationToken)
    {
        RepositoryToolContext context = ToolEnvironment.Load();
        PackagingConfig config = PackagingConfig.Load(context.ConfigPath);
        string version = PackageVersions.RequireNuGetPackageVersion(options.Version);
        string outputRoot = WorkspacePaths.ResolveRepoRelativePath(config.RequiredOutputRoot(), context.RepoRoot, "outputRoot");
        return NuGetPublisher.PublishToSourceAsync(
            ResolvePackageOutputs(outputRoot, version, config),
            config.RequiredPackageSource(),
            options.SkipDuplicate,
            cancellationToken);
    }

    private static NuGetPackageOutput[] ResolvePackageOutputs(
        string outputRoot,
        string version,
        PackagingConfig config)
    {
        if (config.Packages.Length == 0)
        {
            throw new InvalidOperationException("packages 至少需要包含一个引用包。");
        }

        HashSet<string> packageIds = new(StringComparer.OrdinalIgnoreCase);
        List<NuGetPackageOutput> outputs = [];
        string versionOutputRoot = Path.Combine(outputRoot, version);

        foreach (ReferencePackageConfig packageConfig in config.Packages)
        {
            string id = ReferenceAssemblySelector.RequiredId(packageConfig);
            if (!packageIds.Add(id))
            {
                throw new InvalidOperationException($"引用包 id 重复: {id}");
            }

            string outputPath = Path.Combine(versionOutputRoot, $"{id}.{version}.nupkg");
            if (!File.Exists(outputPath))
            {
                throw new FileNotFoundException(
                    $"包文件不存在: {outputPath}。发布前请先为版本 {version} 运行 pack 命令。",
                    outputPath);
            }

            outputs.Add(new NuGetPackageOutput(id, version, outputPath));
        }

        return [.. outputs.OrderBy(output => output.Id, StringComparer.OrdinalIgnoreCase)];
    }
}
