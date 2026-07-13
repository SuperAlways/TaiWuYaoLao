using System.Reflection;
using System.Runtime.Loader;

namespace Taiwu.ModKit.Tooling;

public sealed class GameAssemblyCatalog
{
    private const string BackendDirectoryName = "Backend";
    private const string UnityDataDirectoryName = "The Scroll of Taiwu_Data";
    private const string ManagedDirectoryName = "Managed";
    private static readonly Lock ResolverLock = new();
    private static readonly HashSet<string> RegisteredAssemblySearchDirectories = new(StringComparer.OrdinalIgnoreCase);
    private static bool s_resolverRegistered;

    private GameAssemblyCatalog(
        string gameDir,
        string backendDirectory,
        string unityDataDirectory,
        string unityManagedDirectory)
    {
        GameDir = gameDir;
        BackendDirectory = backendDirectory;
        UnityDataDirectory = unityDataDirectory;
        UnityManagedDirectory = unityManagedDirectory;
    }

    public string GameDir { get; }

    public string BackendDirectory { get; }

    public string UnityDataDirectory { get; }

    public string UnityManagedDirectory { get; }

    public static GameAssemblyCatalog Load(string gameDir)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameDir);

        string normalizedGameDir = Path.GetFullPath(gameDir);
        if (!Directory.Exists(normalizedGameDir))
        {
            throw new DirectoryNotFoundException($"游戏目录不存在: {normalizedGameDir}");
        }

        string backendDirectory = ResolveRequiredDirectory(
            normalizedGameDir,
            BackendDirectoryName,
            "游戏后端程序集目录");
        string unityDataDirectory = ResolveRequiredDirectory(
            normalizedGameDir,
            UnityDataDirectoryName,
            "游戏 Unity 数据目录");
        string unityManagedDirectory = ResolveRequiredDirectory(
            unityDataDirectory,
            ManagedDirectoryName,
            "游戏 Unity Managed 目录");
        string[] assemblySearchDirectories = [backendDirectory, unityManagedDirectory];

        RegisterAssemblyResolver(assemblySearchDirectories);

        return new GameAssemblyCatalog(
            normalizedGameDir,
            backendDirectory,
            unityDataDirectory,
            unityManagedDirectory);
    }

    public Assembly LoadBackendAssembly(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        return LoadAssembly(BackendDirectory, fileName);
    }

    public Assembly LoadUnityManagedAssembly(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        return LoadAssembly(UnityManagedDirectory, fileName);
    }

    private static string ResolveRequiredDirectory(string root, string relativePath, string label)
    {
        string directory = Path.GetFullPath(Path.Combine(
            root,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException($"{label}不存在: {directory}");
        }

        return directory;
    }

    private static Assembly LoadAssembly(string directory, string fileName)
    {
        string path = Path.Combine(directory, fileName);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"游戏程序集不存在: {path}", path);
        }

        return Assembly.LoadFrom(path);
    }

    private static void RegisterAssemblyResolver(IReadOnlyList<string> assemblySearchDirectories)
    {
        lock (ResolverLock)
        {
            foreach (string directory in assemblySearchDirectories)
            {
                _ = RegisteredAssemblySearchDirectories.Add(Path.GetFullPath(directory));
            }

            if (s_resolverRegistered)
            {
                return;
            }

            AssemblyLoadContext.Default.Resolving += ResolveAssembly;
            s_resolverRegistered = true;
        }
    }

    private static Assembly? ResolveAssembly(AssemblyLoadContext context, AssemblyName assemblyName)
    {
        string? name = assemblyName.Name;
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        string[] directories;
        lock (ResolverLock)
        {
            directories = [.. RegisteredAssemblySearchDirectories];
        }

        foreach (string directory in directories)
        {
            string path = Path.Combine(directory, name + ".dll");
            if (File.Exists(path))
            {
                return context.LoadFromAssemblyPath(path);
            }
        }

        return null;
    }
}
