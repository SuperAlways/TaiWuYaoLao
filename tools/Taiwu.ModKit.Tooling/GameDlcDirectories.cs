namespace Taiwu.ModKit.Tooling;

public static class GameDlcDirectories
{
    public const string UnityDataDirectory = "The Scroll of Taiwu_Data";
    public const string DlcResourcesDirectoryName = "DlcResources";

    public static string[] ResolveLatestVersionDirectories(string gameDir)
    {
        string unityDataPath = WorkspacePaths.ResolveGameRelativeDirectory(gameDir, UnityDataDirectory);
        List<string> directories = [];
        foreach (string dlcDirectory in Directory.EnumerateDirectories(unityDataPath))
        {
            string? versionDirectory = TryResolveLatestVersionDirectory(dlcDirectory);
            if (versionDirectory is not null)
            {
                directories.Add(versionDirectory);
            }
        }

        return
        [
            .. directories.OrderBy(
                directory => NormalizeRelativePath(Path.GetRelativePath(gameDir, directory)),
                StringComparer.OrdinalIgnoreCase),
        ];
    }

    private static string? TryResolveLatestVersionDirectory(string dlcDirectory)
    {
        Version? latestVersion = null;
        string? latestDirectory = null;
        foreach (string versionDirectory in Directory.EnumerateDirectories(dlcDirectory))
        {
            if (!Version.TryParse(Path.GetFileName(versionDirectory), out Version? version)
                || !Directory.Exists(Path.Combine(versionDirectory, DlcResourcesDirectoryName)))
            {
                continue;
            }

            if (latestVersion is null || version > latestVersion)
            {
                latestVersion = version;
                latestDirectory = versionDirectory;
            }
        }

        return latestDirectory;
    }

    private static string NormalizeRelativePath(string path)
    {
        return path.Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/');
    }
}
