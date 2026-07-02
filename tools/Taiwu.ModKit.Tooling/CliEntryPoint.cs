using System.ComponentModel;
using CliWrap.Exceptions;

namespace Taiwu.ModKit.Tooling;

public static class CliEntryPoint
{
    public static async Task<int> RunAsync(Func<Task<int>> invokeAsync)
    {
        ArgumentNullException.ThrowIfNull(invokeAsync);

        try
        {
            return await invokeAsync();
        }
        catch (OperationCanceledException)
        {
            return 130;
        }
        catch (Exception ex) when (ShouldReportError(ex))
        {
            await Console.Error.WriteLineAsync(ex.Message);
            return 1;
        }
    }

    private static bool ShouldReportError(Exception ex)
    {
        return ex is ArgumentException
            or IOException
            or UnauthorizedAccessException
            or InvalidOperationException
            or CommandExecutionException
            or Win32Exception;
    }
}
