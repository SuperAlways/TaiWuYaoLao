namespace Taiwu.ModKit.Tooling;

public static class WorkspacePaths
{
    public static string RequiredDirectoryFromEnv(string envName, string targetDescription)
    {
        string? value = Environment.GetEnvironmentVariable(envName);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"请将 {envName} 设置为{targetDescription}。");
        }

        string path = Path.GetFullPath(value);
        if (!Directory.Exists(path))
        {
            throw new DirectoryNotFoundException($"{envName} 指向的目录不存在: {path}");
        }

        return path;
    }

    public static string RequiredFileFromEnv(string envName, string targetDescription)
    {
        string? value = Environment.GetEnvironmentVariable(envName);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"请将 {envName} 设置为{targetDescription}。");
        }

        string path = Path.GetFullPath(value);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"{envName} 指向的文件不存在: {path}", path);
        }

        return path;
    }

    public static string ResolveRepoRelativePath(string value, string repoRoot, string settingName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{settingName} 不能为空。");
        }

        if (Path.IsPathRooted(value))
        {
            throw new InvalidOperationException($"{settingName} 必须是相对仓库根目录的路径。");
        }

        string resolved = Path.GetFullPath(Path.Combine(repoRoot, value));
        if (SameDirectory(resolved, repoRoot))
        {
            throw new InvalidOperationException($"{settingName} 不能是仓库根目录。");
        }

        if (!IsWithinOrEqual(resolved, repoRoot))
        {
            throw new InvalidOperationException($"{settingName} 必须位于仓库目录内。");
        }

        return resolved;
    }

    public static string ResolveGameRelativeDirectory(string gameDir, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException("游戏相对目录不能为空。");
        }

        if (Path.IsPathRooted(value))
        {
            throw new InvalidOperationException($"游戏相对目录不能是根路径: {value}");
        }

        string resolved = Path.GetFullPath(Path.Combine(gameDir, value));
        if (!IsWithinOrEqual(resolved, gameDir))
        {
            throw new InvalidOperationException($"游戏相对目录必须位于游戏目录内: {value}");
        }

        if (!Directory.Exists(resolved))
        {
            throw new DirectoryNotFoundException($"配置指向的游戏目录不存在: {resolved}");
        }

        return resolved;
    }

    public static string FindRepoRoot(string markerFileName, IEnumerable<string> startDirectories)
    {
        ArgumentNullException.ThrowIfNull(startDirectories);

        foreach (string startDirectory in startDirectories)
        {
            DirectoryInfo? directory = new(Path.GetFullPath(startDirectory));

            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, markerFileName)))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new DirectoryNotFoundException($"无法从当前目录或工具输出目录找到仓库根目录。预期标记文件: {markerFileName}");
    }

    public static void DeleteDirectoryInsideRoot(string directory, string rootDirectory, string rootDescription)
    {
        string resolved = Path.GetFullPath(directory);
        if (!IsWithinOrEqual(resolved, rootDirectory) || SameDirectory(resolved, rootDirectory))
        {
            throw new InvalidOperationException($"拒绝删除 {rootDescription} 外或其根目录: {resolved}");
        }

        if (Directory.Exists(resolved))
        {
            Directory.Delete(resolved, recursive: true);
        }
    }

    public static string SanitizeRelativePath(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        string[] segments =
        [
            .. value
            .Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries)
            .Select(SanitizePathSegment),
        ];

        return Path.Combine(segments);
    }

    public static string SanitizePathSegment(string value)
    {
        char[] invalidChars = Path.GetInvalidFileNameChars();
        return string.Concat(value.Select(ch => invalidChars.Contains(ch) ? '_' : ch));
    }

    private static bool IsWithinOrEqual(string path, string root)
    {
        string normalizedPath = NormalizeDirectory(path);
        string normalizedRoot = NormalizeDirectory(root);

        return SameDirectory(normalizedPath, normalizedRoot)
            || normalizedPath.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static bool SameDirectory(string left, string right)
    {
        return StringComparer.OrdinalIgnoreCase.Equals(NormalizeDirectory(left), NormalizeDirectory(right));
    }

    private static string NormalizeDirectory(string path)
    {
        return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}
