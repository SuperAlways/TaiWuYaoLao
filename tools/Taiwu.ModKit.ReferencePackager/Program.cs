using System.CommandLine;
using Taiwu.ModKit.Tooling;

namespace Taiwu.ModKit.ReferencePackager;

internal static class Program
{
    public static Task<int> Main(string[] args)
    {
        return CliEntryPoint.RunAsync(async () =>
        {
            Command command = CommandLineOptions.CreateCommand(ReferencePackagerTool.RunAsync);
            return await command.Parse(args)
                .InvokeAsync(CreateInvocationConfiguration());
        });
    }

    private static InvocationConfiguration CreateInvocationConfiguration()
    {
        return new InvocationConfiguration
        {
            EnableDefaultExceptionHandler = false,
        };
    }
}
