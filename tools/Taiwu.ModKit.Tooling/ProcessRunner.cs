using CliWrap;
using CliWrap.Buffered;

namespace Taiwu.ModKit.Tooling;

public static class ProcessRunner
{
    public static async Task RunAsync(
        string fileName,
        IReadOnlyCollection<string> arguments,
        string displayCommand,
        CancellationToken cancellationToken,
        string? workingDirectory = null,
        bool echoOutput = true)
    {
        Console.WriteLine(displayCommand);

        Command command = CreateCommand(fileName, arguments, workingDirectory);

        if (echoOutput)
        {
            await RunWithEchoAsync(command, displayCommand, cancellationToken);
            return;
        }

        await RunBufferedAsync(command, displayCommand, cancellationToken);
    }

    private static Command CreateCommand(
        string fileName,
        IReadOnlyCollection<string> arguments,
        string? workingDirectory)
    {
        Command command = Cli.Wrap(fileName)
            .WithArguments(arguments)
            .WithValidation(CommandResultValidation.None);

        if (!string.IsNullOrWhiteSpace(workingDirectory))
        {
            command = command.WithWorkingDirectory(workingDirectory);
        }

        return command;
    }

    private static async Task RunWithEchoAsync(Command command, string displayCommand, CancellationToken cancellationToken)
    {
        CommandResult result = await command
            .WithStandardOutputPipe(PipeTarget.ToDelegate(Console.WriteLine))
            .WithStandardErrorPipe(PipeTarget.ToDelegate(Console.Error.WriteLine))
            .ExecuteAsync(cancellationToken);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"{displayCommand} 退出码为 {result.ExitCode}。");
        }
    }

    private static async Task RunBufferedAsync(Command command, string displayCommand, CancellationToken cancellationToken)
    {
        BufferedCommandResult result = await command.ExecuteBufferedAsync(cancellationToken);

        if (result.ExitCode == 0)
        {
            return;
        }

        WriteIfNotEmpty(Console.Out, result.StandardOutput);
        WriteIfNotEmpty(Console.Error, result.StandardError);
        throw new InvalidOperationException($"{displayCommand} 退出码为 {result.ExitCode}。");
    }

    private static void WriteIfNotEmpty(TextWriter writer, string value)
    {
        string output = value.TrimEnd();
        if (string.IsNullOrWhiteSpace(output))
        {
            return;
        }

        writer.WriteLine(output);
    }
}
