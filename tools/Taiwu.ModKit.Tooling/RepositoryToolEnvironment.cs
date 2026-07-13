using dotenv.net;

namespace Taiwu.ModKit.Tooling;

public static class RepositoryToolEnvironment
{
    private const string EnvFileName = ".env";

    public static RepositoryToolContext Load(string repoMarkerFileName, string configFileName)
    {
        string repoRoot = WorkspacePaths.FindRepoRoot(
            repoMarkerFileName,
            [Directory.GetCurrentDirectory(), AppContext.BaseDirectory]);
        LoadRepoEnvironment(repoRoot);

        return new RepositoryToolContext(
            repoRoot,
            ResolveBundledConfigPath(configFileName));
    }

    private static string ResolveBundledConfigPath(string configFileName)
    {
        if (string.IsNullOrWhiteSpace(configFileName))
        {
            throw new InvalidOperationException("工具配置文件名不能为空。");
        }

        string configPath = Path.Combine(AppContext.BaseDirectory, configFileName);
        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException(
                $"随工具复制的配置文件不存在: {configPath}。请先构建工具，确保 {configFileName} 已复制到输出目录。",
                configPath);
        }

        return configPath;
    }

    private static void LoadRepoEnvironment(string repoRoot)
    {
        string envPath = Path.Combine(repoRoot, EnvFileName);
        if (!File.Exists(envPath))
        {
            return;
        }

        DotEnv.Load(options: new DotEnvOptions(
            ignoreExceptions: false,
            envFilePaths: [envPath],
            trimValues: true,
            overwriteExistingVars: false));
    }
}

public sealed record RepositoryToolContext(string RepoRoot, string ConfigPath);
