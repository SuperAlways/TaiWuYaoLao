using Taiwu.ModKit.Tooling;
using Taiwu.ModKit.Tooling.NuGet;

namespace Taiwu.ModKit.ReferencePackager;

internal static class ReferencePackageBuilder
{
    public static ReferencePackage[] Build(
        string gameDir,
        string outputRoot,
        string version,
        PackagingConfig config)
    {
        if (config.Packages.Length == 0)
        {
            throw new InvalidOperationException("packages 至少需要包含一个引用包。");
        }

        Dictionary<string, ReferencePackageConfig> packageConfigs = new(StringComparer.OrdinalIgnoreCase);
        List<ReferencePackage> packages = [];
        string versionOutputRoot = Path.Combine(outputRoot, version);

        foreach (ReferencePackageConfig packageConfig in config.Packages)
        {
            string id = ReferenceAssemblySelector.RequiredId(packageConfig);
            if (!packageConfigs.TryAdd(id, packageConfig))
            {
                throw new InvalidOperationException($"引用包 id 重复: {id}");
            }
        }

        foreach ((string id, ReferencePackageConfig packageConfig) in packageConfigs)
        {
            ReferencePackageAssetGroup[] assetGroups = BuildPackageAssetGroups(gameDir, id, packageConfig);
            string outputPath = Path.Combine(versionOutputRoot, $"{id}.{version}.nupkg");

            packages.Add(new ReferencePackage(
                new NuGetPackageSpec(
                    id,
                    version,
                    string.IsNullOrWhiteSpace(packageConfig.Title) ? id : packageConfig.Title,
                    string.IsNullOrWhiteSpace(packageConfig.Description)
                        ? "从 references.yml assetGroups 收集的编译期引用。"
                        : packageConfig.Description,
                    config.CreatePackageMetadata(),
                    BuildPackageDependencyGroups(packageConfig, packageConfigs, version),
                    BuildPackageFiles(assetGroups),
                    outputPath),
                assetGroups));
        }

        return [.. packages.OrderBy(package => package.Id, StringComparer.OrdinalIgnoreCase)];
    }

    private static ReferencePackageAssetGroup[] BuildPackageAssetGroups(
        string gameDir,
        string id,
        ReferencePackageConfig packageConfig)
    {
        if (packageConfig.AssetGroups.Length == 0)
        {
            throw new InvalidOperationException($"引用包 '{id}' 必须指定 assetGroups。");
        }

        HashSet<string> targetFrameworks = new(StringComparer.OrdinalIgnoreCase);
        List<ReferencePackageAssetGroup> assetGroups = [];
        foreach (ReferenceAssetGroupConfig assetGroupConfig in packageConfig.AssetGroups)
        {
            string targetFramework = ReadTargetFramework(id, assetGroupConfig);
            if (!targetFrameworks.Add(targetFramework))
            {
                throw new InvalidOperationException($"引用包 '{id}' 重复声明 assetGroups[].targetFramework: {targetFramework}");
            }

            PackageAssembly[] assemblies = ReferenceAssemblySelector.ResolveAssemblies(gameDir, id, assetGroupConfig);
            if (assemblies.Length == 0)
            {
                throw new InvalidOperationException($"引用包 '{id}' 的资产组 '{targetFramework}' 没有匹配到任何 DLL。");
            }

            assetGroups.Add(new ReferencePackageAssetGroup(targetFramework, assemblies));
        }

        return
        [
            .. assetGroups.OrderBy(group => group.TargetFramework, StringComparer.OrdinalIgnoreCase),
        ];
    }

    private static string ReadTargetFramework(string id, ReferenceAssetGroupConfig assetGroupConfig)
    {
        return NuGetPackageRules.RequiredTargetFramework(
            assetGroupConfig.TargetFramework,
            $"引用包 '{id}' 的 assetGroups[].targetFramework");
    }

    private static NuGetDependencyGroup[] BuildPackageDependencyGroups(
        ReferencePackageConfig packageConfig,
        IReadOnlyDictionary<string, ReferencePackageConfig> packageConfigs,
        string version)
    {
        string id = ReferenceAssemblySelector.RequiredId(packageConfig);
        string[] targetFrameworks = ReadPackageTargetFrameworks(id, packageConfig);
        NuGetDependency[] dependencies = BuildPackageDependencies(packageConfig, packageConfigs, version);

        return
        [
            .. targetFrameworks.Select(targetFramework => new NuGetDependencyGroup(targetFramework, dependencies)),
        ];
    }

    private static NuGetDependency[] BuildPackageDependencies(
        ReferencePackageConfig packageConfig,
        IReadOnlyDictionary<string, ReferencePackageConfig> packageConfigs,
        string version)
    {
        string id = ReferenceAssemblySelector.RequiredId(packageConfig);
        HashSet<string> dependencyIds = new(StringComparer.OrdinalIgnoreCase);
        foreach (string dependencyId in packageConfig.DependsOn)
        {
            string normalizedDependencyId = NuGetPackageRules.RequiredPackageId(
                dependencyId,
                $"引用包 '{id}' 的依赖 id");

            if (StringComparer.OrdinalIgnoreCase.Equals(id, normalizedDependencyId))
            {
                throw new InvalidOperationException($"引用包 '{id}' 不能依赖自身。");
            }

            if (!packageConfigs.ContainsKey(normalizedDependencyId))
            {
                throw new InvalidOperationException($"引用包 '{id}' 依赖了未知包 '{normalizedDependencyId}'。");
            }

            if (!dependencyIds.Add(normalizedDependencyId))
            {
                throw new InvalidOperationException($"引用包 '{id}' 重复声明依赖 '{normalizedDependencyId}'。");
            }
        }

        return
        [
            .. dependencyIds
            .Order(StringComparer.OrdinalIgnoreCase)
            .Select(dependencyId => new NuGetDependency(dependencyId, version)),
        ];
    }

    private static string[] ReadPackageTargetFrameworks(string id, ReferencePackageConfig packageConfig)
    {
        if (packageConfig.AssetGroups.Length == 0)
        {
            throw new InvalidOperationException($"引用包 '{id}' 必须指定 assetGroups。");
        }

        HashSet<string> targetFrameworks = new(StringComparer.OrdinalIgnoreCase);
        foreach (ReferenceAssetGroupConfig assetGroupConfig in packageConfig.AssetGroups)
        {
            string targetFramework = ReadTargetFramework(id, assetGroupConfig);
            if (!targetFrameworks.Add(targetFramework))
            {
                throw new InvalidOperationException($"引用包 '{id}' 重复声明 assetGroups[].targetFramework: {targetFramework}");
            }
        }

        return [.. targetFrameworks.Order(StringComparer.OrdinalIgnoreCase)];
    }

    private static NuGetPackageFile[] BuildPackageFiles(IEnumerable<ReferencePackageAssetGroup> assetGroups)
    {
        List<NuGetPackageFile> files = [];
        foreach (ReferencePackageAssetGroup assetGroup in assetGroups)
        {
            files.AddRange(assetGroup.Assemblies.Select(assembly => new NuGetPackageFile(
                assembly.SourcePath,
                $"ref/{assetGroup.TargetFramework}/{assembly.FileName}")));
        }

        return [.. files];
    }
}
