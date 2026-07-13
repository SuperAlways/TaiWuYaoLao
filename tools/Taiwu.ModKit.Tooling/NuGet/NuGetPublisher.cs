namespace Taiwu.ModKit.Tooling.NuGet;

public static class NuGetPublisher
{
    private const string GitHubTokenEnv = "TAIWU_MODKIT_GITHUB_TOKEN";

    public static async Task PublishToSourceAsync(
        IReadOnlyCollection<NuGetPackageOutput> packages,
        string packageSource,
        bool skipDuplicate,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(packages);

        if (string.IsNullOrWhiteSpace(packageSource))
        {
            throw new InvalidOperationException("NuGet 发布源不能为空。");
        }

        string apiKey = ResolveApiKey();

        foreach (NuGetPackageOutput package in packages)
        {
            List<string> arguments =
            [
                "nuget",
                "push",
                package.OutputPath,
                "--source",
                packageSource,
                "--api-key",
                apiKey,
            ];

            string displayCommand = $"dotnet nuget push {package.OutputPath} --source {packageSource} --api-key ***";
            if (skipDuplicate)
            {
                arguments.Add("--skip-duplicate");
                displayCommand += " --skip-duplicate";
            }

            await ProcessRunner.RunAsync("dotnet", arguments, displayCommand, cancellationToken);
        }
    }

    private static string ResolveApiKey()
    {
        string? apiKey = Environment.GetEnvironmentVariable(GitHubTokenEnv);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException($"发布 token 未设置。请设置 {GitHubTokenEnv}。");
        }

        return apiKey;
    }
}
