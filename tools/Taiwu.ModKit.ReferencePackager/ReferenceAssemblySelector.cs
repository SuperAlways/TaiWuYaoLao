using System.IO.Enumeration;
using Taiwu.ModKit.Tooling;
using Taiwu.ModKit.Tooling.NuGet;

namespace Taiwu.ModKit.ReferencePackager;

internal static class ReferenceAssemblySelector
{
    public static PackageAssembly[] ResolveAssemblies(
        string gameDir,
        string packageId,
        ReferenceAssetGroupConfig assetGroupConfig)
    {
        string relativeDirectory = RequiredDirectory(packageId, assetGroupConfig);
        string directory = WorkspacePaths.ResolveGameRelativeDirectory(gameDir, relativeDirectory);
        ValidateExcludePatterns(packageId, assetGroupConfig);
        if (assetGroupConfig.RootAssemblies.Length == 0)
        {
            throw new InvalidOperationException($"引用包 '{packageId}' 的 assetGroups[] 必须指定 rootAssemblies。");
        }

        PackageAssembly[] rootAssemblies = ResolveRootAssemblies(directory, packageId, assetGroupConfig);
        if (!assetGroupConfig.FollowReferences)
        {
            return rootAssemblies;
        }

        return ResolveReferencedAssemblies(directory, packageId, assetGroupConfig, rootAssemblies);
    }

    public static string RequiredId(ReferencePackageConfig packageConfig)
    {
        return NuGetPackageRules.RequiredPackageId(packageConfig.Id, "引用包 id");
    }

    private static string RequiredDirectory(string packageId, ReferenceAssetGroupConfig assetGroupConfig)
    {
        if (string.IsNullOrWhiteSpace(assetGroupConfig.Directory))
        {
            throw new InvalidOperationException($"引用包 '{packageId}' 的 assetGroups[].directory 不能为空。");
        }

        return assetGroupConfig.Directory;
    }

    private static PackageAssembly[] ResolveRootAssemblies(
        string directory,
        string packageId,
        ReferenceAssetGroupConfig assetGroupConfig)
    {
        HashSet<string> rootAssemblyPaths = new(StringComparer.OrdinalIgnoreCase);
        List<PackageAssembly> assemblies = [];
        foreach (string rootAssembly in assetGroupConfig.RootAssemblies)
        {
            ValidateRootAssemblyFileName(rootAssembly, packageId);
            string assemblyPath = Path.GetFullPath(Path.Combine(directory, rootAssembly));
            if (!File.Exists(assemblyPath))
            {
                throw new FileNotFoundException(
                    $"引用包 '{packageId}' 的根程序集不存在: {assemblyPath}。",
                    assemblyPath);
            }

            if (MatchesAny(rootAssembly, assetGroupConfig.Exclude))
            {
                throw new InvalidOperationException($"引用包 '{packageId}' 的根程序集 '{rootAssembly}' 同时被 exclude 匹配。");
            }

            if (!rootAssemblyPaths.Add(assemblyPath))
            {
                throw new InvalidOperationException($"引用包 '{packageId}' 重复声明根程序集: {rootAssembly}");
            }

            assemblies.Add(new PackageAssembly(assemblyPath, rootAssembly));
        }

        return
        [
            .. assemblies
            .OrderBy(assembly => assembly.FileName, StringComparer.OrdinalIgnoreCase),
        ];
    }

    private static void ValidateRootAssemblyFileName(string fileName, string packageId)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new InvalidOperationException($"引用包 '{packageId}' 的根程序集文件名不能为空。");
        }

        if (fileName.Contains(Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || fileName.Contains(Path.AltDirectorySeparatorChar, StringComparison.Ordinal)
            || fileName.Contains('*', StringComparison.Ordinal)
            || fileName.Contains('?', StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"引用包 '{packageId}' 的根程序集必须是精确 DLL 文件名: {fileName}");
        }

        if (!fileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"引用包 '{packageId}' 的根程序集必须是 DLL 文件: {fileName}");
        }
    }

    private static void ValidateExcludePatterns(string packageId, ReferenceAssetGroupConfig assetGroupConfig)
    {
        foreach (string pattern in assetGroupConfig.Exclude)
        {
            if (string.IsNullOrWhiteSpace(pattern))
            {
                throw new InvalidOperationException($"引用包 '{packageId}' 的 exclude 不能包含空模式。");
            }

            if (pattern.Contains(Path.DirectorySeparatorChar, StringComparison.Ordinal)
                || pattern.Contains(Path.AltDirectorySeparatorChar, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"引用包 '{packageId}' 的 exclude 模式不能包含目录分隔符: {pattern}");
            }
        }
    }

    private static bool MatchesAny(string fileName, IEnumerable<string> patterns)
    {
        foreach (string pattern in patterns)
        {
            if (FileSystemName.MatchesSimpleExpression(pattern, fileName, ignoreCase: true))
            {
                return true;
            }
        }

        return false;
    }

    private static PackageAssembly[] ResolveReferencedAssemblies(
        string directory,
        string packageId,
        ReferenceAssetGroupConfig assetGroupConfig,
        IEnumerable<PackageAssembly> rootAssemblies)
    {
        Dictionary<string, string> assemblyPaths = BuildAssemblyPathIndex(directory);
        Dictionary<string, PackageAssembly> packageAssembliesBySourcePath = new(StringComparer.OrdinalIgnoreCase);
        Queue<PackageAssembly> pendingAssemblies = [];

        foreach (PackageAssembly rootAssembly in rootAssemblies)
        {
            AddPackageAssembly(rootAssembly, packageAssembliesBySourcePath, pendingAssemblies);
        }

        HashSet<string> missingReferences = new(StringComparer.OrdinalIgnoreCase);
        while (pendingAssemblies.Count > 0)
        {
            PackageAssembly assembly = pendingAssemblies.Dequeue();
            foreach (string referenceName in AssemblyReferenceReader.ReadNonFrameworkReferences(assembly.SourcePath))
            {
                if (!assemblyPaths.TryGetValue(referenceName, out string? referencePath))
                {
                    _ = missingReferences.Add(referenceName);
                    continue;
                }

                string fileName = Path.GetFileName(referencePath);
                if (MatchesAny(fileName, assetGroupConfig.Exclude))
                {
                    continue;
                }

                AddPackageAssembly(
                    new PackageAssembly(referencePath, fileName),
                    packageAssembliesBySourcePath,
                    pendingAssemblies);
            }
        }

        if (missingReferences.Count > 0)
        {
            throw new InvalidOperationException($"引用包 '{packageId}' 无法解析引用程序集: {string.Join(", ", missingReferences.Order(StringComparer.OrdinalIgnoreCase))}");
        }

        return
        [
            .. packageAssembliesBySourcePath.Values
            .OrderBy(assembly => assembly.FileName, StringComparer.OrdinalIgnoreCase),
        ];
    }

    private static Dictionary<string, string> BuildAssemblyPathIndex(string directory)
    {
        Dictionary<string, string> assemblyPaths = new(StringComparer.OrdinalIgnoreCase);
        foreach (string path in Directory.EnumerateFiles(directory, "*.dll", SearchOption.TopDirectoryOnly))
        {
            string? assemblyName = AssemblyReferenceReader.ReadAssemblyName(path);
            if (string.IsNullOrWhiteSpace(assemblyName))
            {
                continue;
            }

            if (!assemblyPaths.TryAdd(assemblyName, path))
            {
                throw new InvalidOperationException($"目录 '{directory}' 中有多个 DLL 声明了程序集名 '{assemblyName}'。");
            }
        }

        return assemblyPaths;
    }

    private static void AddPackageAssembly(
        PackageAssembly assembly,
        IDictionary<string, PackageAssembly> packageAssembliesBySourcePath,
        Queue<PackageAssembly> pendingAssemblies)
    {
        if (!packageAssembliesBySourcePath.TryAdd(assembly.SourcePath, assembly))
        {
            return;
        }

        pendingAssemblies.Enqueue(assembly);
    }
}
