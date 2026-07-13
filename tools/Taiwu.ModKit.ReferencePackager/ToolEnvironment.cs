using Taiwu.ModKit.Tooling;

namespace Taiwu.ModKit.ReferencePackager;

internal static class ToolEnvironment
{
    private const string ConfigFileName = "references.yml";
    private const string GameDirEnv = "TAIWU_MODKIT_GAME_DIR";
    private const string RepoMarkerFileName = "Taiwu.ModKit.slnx";

    public static RepositoryToolContext Load()
    {
        return RepositoryToolEnvironment.Load(RepoMarkerFileName, ConfigFileName);
    }

    public static PackToolContext LoadForPack()
    {
        RepositoryToolContext context = Load();
        return new PackToolContext(
            context.RepoRoot,
            WorkspacePaths.RequiredDirectoryFromEnv(GameDirEnv, "本机游戏目录"),
            context.ConfigPath);
    }
}

internal sealed record PackToolContext(string RepoRoot, string GameDir, string ConfigPath);
