using CliWrap;
using CliWrap.Buffered;

namespace Taiwu.Mods.Cli;

internal static class ProcessRunner
{
    public static async Task RunAsync(
        string fileName,
        string workingDirectory,
        IReadOnlyCollection<string> arguments,
        CancellationToken cancellationToken)
    {
        CommandResult result = await CreateCommand(fileName, workingDirectory, arguments)
            .WithStandardOutputPipe(PipeTarget.ToDelegate(Console.WriteLine))
            .WithStandardErrorPipe(PipeTarget.ToDelegate(Console.Error.WriteLine))
            .ExecuteAsync(cancellationToken)
            .ConfigureAwait(false);

        if (result.ExitCode == 0)
        {
            return;
        }

        throw new InvalidOperationException($"命令执行失败（退出码 {result.ExitCode}）：{CreateDisplayCommand(fileName, arguments)}");
    }

    public static async Task<string> RunForOutputAsync(
        string fileName,
        string workingDirectory,
        IReadOnlyCollection<string> arguments,
        CancellationToken cancellationToken)
    {
        BufferedCommandResult result = await CreateCommand(fileName, workingDirectory, arguments)
            .ExecuteBufferedAsync(cancellationToken)
            .ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            WriteIfNotEmpty(Console.Out, result.StandardOutput);
            WriteIfNotEmpty(Console.Error, result.StandardError);
            throw new InvalidOperationException($"命令执行失败（退出码 {result.ExitCode}）：{CreateDisplayCommand(fileName, arguments)}");
        }

        return result.StandardOutput.Trim();
    }

    private static Command CreateCommand(
        string fileName,
        string workingDirectory,
        IReadOnlyCollection<string> arguments)
    {
        return global::CliWrap.Cli.Wrap(fileName)
            .WithWorkingDirectory(workingDirectory)
            .WithArguments(arguments)
            .WithValidation(CommandResultValidation.None);
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

    private static string CreateDisplayCommand(string fileName, IEnumerable<string> arguments)
    {
        return $"{fileName} {string.Join(' ', arguments)}";
    }
}
