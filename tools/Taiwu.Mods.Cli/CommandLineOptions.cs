using System.CommandLine;
using System.CommandLine.Help;

namespace Taiwu.Mods.Cli;

internal sealed class CommandLineOptions
{
    private CommandLineOptions()
    {
    }

    public required string Name { get; init; }

    public CliOperation Operation { get; init; } = CliOperation.CreateMod;

    public string RepoRoot { get; init; } = Directory.GetCurrentDirectory();

    public string? ModsRoot { get; init; }

    public string? SharedRoot { get; init; }

    public string? ArtifactsRoot { get; init; }

    public string Configuration { get; init; } = "Release";

    public string SharedSide { get; init; } = "Shared";

    public bool Force { get; init; }

    public bool SkipSolution { get; init; }

    public static Command CreateCommand(Action<CommandLineOptions> run)
    {
        Command command = new("Taiwu.Mods.Cli", "太吾 mod 仓库维护命令。");

        command.Options.Add(new HelpOption { Recursive = true });
        command.Subcommands.Add(CreateCreateModCommand(run));
        command.Subcommands.Add(CreateRemoveModCommand(run));
        command.Subcommands.Add(CreatePackModCommand(run));
        command.Subcommands.Add(CreateCreateSharedCommand(run));
        command.Subcommands.Add(CreateRemoveSharedCommand(run));

        return command;
    }

    private static Command CreateCreateModCommand(Action<CommandLineOptions> run)
    {
        Option<string> nameOption = CreateNameOption("Mod 名称。", "ModName");
        Option<string> repoRootOption = CreateRepoRootOption();
        Option<string?> modsRootOption = CreateModsRootOption();
        Option<bool> forceOption = CreateForceOption();
        Option<bool> skipSolutionOption = CreateSkipSolutionOption();
        Command command = new("create-mod", "复制 mod 模板并把项目注册到解决方案。");

        command.Options.Add(nameOption);
        command.Options.Add(repoRootOption);
        command.Options.Add(modsRootOption);
        command.Options.Add(forceOption);
        command.Options.Add(skipSolutionOption);
        command.SetAction(parseResult =>
            run(
                new CommandLineOptions
                {
                    Name = parseResult.GetRequiredValue(nameOption),
                    Operation = CliOperation.CreateMod,
                    RepoRoot = parseResult.GetRequiredValue(repoRootOption),
                    ModsRoot = parseResult.GetValue(modsRootOption),
                    Force = parseResult.GetValue(forceOption),
                    SkipSolution = parseResult.GetValue(skipSolutionOption),
                }));

        return command;
    }

    private static Command CreateRemoveModCommand(Action<CommandLineOptions> run)
    {
        Option<string> nameOption = CreateNameOption("Mod 名称。", "ModName");
        Option<string> repoRootOption = CreateRepoRootOption();
        Option<string?> modsRootOption = CreateModsRootOption();
        Command command = new("remove-mod", "从解决方案取消注册指定 mod，保留文件。");

        command.Options.Add(nameOption);
        command.Options.Add(repoRootOption);
        command.Options.Add(modsRootOption);
        command.SetAction(parseResult =>
            run(
                new CommandLineOptions
                {
                    Name = parseResult.GetRequiredValue(nameOption),
                    Operation = CliOperation.RemoveMod,
                    RepoRoot = parseResult.GetRequiredValue(repoRootOption),
                    ModsRoot = parseResult.GetValue(modsRootOption),
                }));

        return command;
    }

    private static Command CreatePackModCommand(Action<CommandLineOptions> run)
    {
        Option<string> nameOption = CreateNameOption("Mod 名称。", "ModName");
        Option<string> repoRootOption = CreateRepoRootOption();
        Option<string?> modsRootOption = CreateModsRootOption();
        Option<string?> artifactsRootOption = CreateArtifactsRootOption();
        Option<string> configurationOption = CreateConfigurationOption();
        Command command = new("pack-mod", "构建并组装指定 mod 的可部署目录。");

        command.Options.Add(nameOption);
        command.Options.Add(repoRootOption);
        command.Options.Add(modsRootOption);
        command.Options.Add(artifactsRootOption);
        command.Options.Add(configurationOption);
        command.SetAction(parseResult =>
            run(
                new CommandLineOptions
                {
                    Name = parseResult.GetRequiredValue(nameOption),
                    Operation = CliOperation.PackMod,
                    RepoRoot = parseResult.GetRequiredValue(repoRootOption),
                    ModsRoot = parseResult.GetValue(modsRootOption),
                    ArtifactsRoot = parseResult.GetValue(artifactsRootOption),
                    Configuration = parseResult.GetRequiredValue(configurationOption),
                }));

        return command;
    }

    private static Command CreateCreateSharedCommand(Action<CommandLineOptions> run)
    {
        Option<string> nameOption = CreateNameOption("内部共享项目名称。", "ProjectName");
        Option<string> repoRootOption = CreateRepoRootOption();
        Option<string?> sharedRootOption = CreateSharedRootOption();
        Option<string> sharedSideOption = CreateSharedSideOption();
        Option<bool> forceOption = CreateForceOption();
        Option<bool> skipSolutionOption = CreateSkipSolutionOption();
        Command command = new("create-shared", "复制内部共享项目模板并把项目注册到解决方案。");

        command.Options.Add(nameOption);
        command.Options.Add(repoRootOption);
        command.Options.Add(sharedRootOption);
        command.Options.Add(sharedSideOption);
        command.Options.Add(forceOption);
        command.Options.Add(skipSolutionOption);
        command.SetAction(parseResult =>
            run(
                new CommandLineOptions
                {
                    Name = parseResult.GetRequiredValue(nameOption),
                    Operation = CliOperation.CreateShared,
                    RepoRoot = parseResult.GetRequiredValue(repoRootOption),
                    SharedRoot = parseResult.GetValue(sharedRootOption),
                    SharedSide = parseResult.GetRequiredValue(sharedSideOption),
                    Force = parseResult.GetValue(forceOption),
                    SkipSolution = parseResult.GetValue(skipSolutionOption),
                }));

        return command;
    }

    private static Command CreateRemoveSharedCommand(Action<CommandLineOptions> run)
    {
        Option<string> nameOption = CreateNameOption("内部共享项目名称。", "ProjectName");
        Option<string> repoRootOption = CreateRepoRootOption();
        Option<string?> sharedRootOption = CreateSharedRootOption();
        Command command = new("remove-shared", "从解决方案取消注册指定内部共享项目，保留文件。");

        command.Options.Add(nameOption);
        command.Options.Add(repoRootOption);
        command.Options.Add(sharedRootOption);
        command.SetAction(parseResult =>
            run(
                new CommandLineOptions
                {
                    Name = parseResult.GetRequiredValue(nameOption),
                    Operation = CliOperation.RemoveShared,
                    RepoRoot = parseResult.GetRequiredValue(repoRootOption),
                    SharedRoot = parseResult.GetValue(sharedRootOption),
                }));

        return command;
    }

    private static Option<string> CreateNameOption(string description, string helpName)
    {
        return new Option<string>("--name")
        {
            Description = description,
            HelpName = helpName,
            Required = true,
        };
    }

    private static Option<string> CreateRepoRootOption()
    {
        return new Option<string>("--repo-root")
        {
            Description = "仓库根目录。",
            HelpName = "path",
            DefaultValueFactory = _ => Directory.GetCurrentDirectory(),
        };
    }

    private static Option<string?> CreateSharedRootOption()
    {
        return new Option<string?>("--shared-root")
        {
            Description = "shared 目录；不传时使用仓库根目录下的 shared。",
            HelpName = "path",
        };
    }

    private static Option<string> CreateSharedSideOption()
    {
        return new Option<string>("--side")
        {
            Description = "内部共享项目端侧：Shared、Frontend 或 Backend。",
            HelpName = "side",
            DefaultValueFactory = _ => "Shared",
        };
    }

    private static Option<string?> CreateModsRootOption()
    {
        return new Option<string?>("--mods-root")
        {
            Description = "mods 目录；不传时使用仓库根目录下的 mods。",
            HelpName = "path",
        };
    }

    private static Option<string?> CreateArtifactsRootOption()
    {
        return new Option<string?>("--artifacts-root")
        {
            Description = "打包输出根目录；不传时使用仓库根目录下的 artifacts/mods。",
            HelpName = "path",
        };
    }

    private static Option<string> CreateConfigurationOption()
    {
        return new Option<string>("--configuration")
        {
            Description = "打包时使用的构建配置。",
            HelpName = "configuration",
            DefaultValueFactory = _ => "Release",
        };
    }

    private static Option<bool> CreateForceOption()
    {
        return new Option<bool>("--force")
        {
            Description = "覆盖已存在的模板文件。",
        };
    }

    private static Option<bool> CreateSkipSolutionOption()
    {
        return new Option<bool>("--skip-solution")
        {
            Description = "不把新建项目加入解决方案。",
        };
    }
}

internal enum CliOperation
{
    CreateMod = 0,
    RemoveMod = 1,
    PackMod = 2,
    CreateShared = 3,
    RemoveShared = 4,
}
