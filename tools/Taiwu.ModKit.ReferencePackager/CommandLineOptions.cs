using System.CommandLine;
using System.CommandLine.Help;

namespace Taiwu.ModKit.ReferencePackager;

internal enum ToolCommand
{
    Pack = 0,
    Publish = 1,
}

internal sealed record CommandLineOptions(
    ToolCommand Command,
    string? Version,
    bool SkipDuplicate)
{
    public static Command CreateCommand(Func<CommandLineOptions, CancellationToken, Task> run)
    {
        Command command = new("Taiwu.ModKit.ReferencePackager", "收集、打包和发布太吾引用程序集 NuGet 包。");

        command.Options.Add(new HelpOption { Recursive = true });
        command.Subcommands.Add(CreatePackCommand(run));
        command.Subcommands.Add(CreatePublishCommand(run));

        return command;
    }

    private static Command CreatePackCommand(Func<CommandLineOptions, CancellationToken, Task> run)
    {
        Option<string> versionOption = CreateVersionOption();
        Command command = new("pack", "从本机游戏目录收集配置中的 DLL，并生成 NuGet 包。");

        command.Options.Add(versionOption);
        command.SetAction((parseResult, cancellationToken) =>
            run(new CommandLineOptions(ToolCommand.Pack, parseResult.GetRequiredValue(versionOption), true), cancellationToken));

        return command;
    }

    private static Command CreatePublishCommand(Func<CommandLineOptions, CancellationToken, Task> run)
    {
        Option<string> versionOption = CreateVersionOption();
        Option<bool> noSkipDuplicateOption = CreateNoSkipDuplicateOption();
        Command command = new("publish", "发布已经生成的 NuGet 包。");

        command.Options.Add(versionOption);
        command.Options.Add(noSkipDuplicateOption);
        command.SetAction((parseResult, cancellationToken) =>
            run(
                new CommandLineOptions(
                    ToolCommand.Publish,
                    parseResult.GetRequiredValue(versionOption),
                    !parseResult.GetValue(noSkipDuplicateOption)),
                cancellationToken));

        return command;
    }

    private static Option<string> CreateVersionOption()
    {
        return new Option<string>("--version")
        {
            Description = "NuGet 包版本。",
            HelpName = "version",
            Required = true,
        };
    }

    private static Option<bool> CreateNoSkipDuplicateOption()
    {
        return new Option<bool>("--no-skip-duplicate")
        {
            Description = "发布时不向 dotnet nuget push 传递 --skip-duplicate。",
        };
    }
}
