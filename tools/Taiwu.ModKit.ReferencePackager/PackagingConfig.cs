using System.Diagnostics.CodeAnalysis;
using Taiwu.ModKit.Tooling;
using Taiwu.ModKit.Tooling.NuGet;

namespace Taiwu.ModKit.ReferencePackager;

[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "由配置解析器实例化。")]
internal sealed class PackagingConfig
{
    public required string OutputRoot { get; init; }

    public string Authors { get; init; } = "Taiwu.ModKit";

    public string? RepositoryUrl { get; init; }

    public required PublishConfig Publish { get; init; }

    public required ReferencePackageConfig[] Packages { get; init; }

    public string RequiredOutputRoot()
    {
        if (string.IsNullOrWhiteSpace(OutputRoot))
        {
            throw new InvalidOperationException("references.yml 必须设置 outputRoot。");
        }

        return OutputRoot;
    }

    public string RequiredPackageSource()
    {
        string? packageSource = Publish.PackageSource;
        if (string.IsNullOrWhiteSpace(packageSource))
        {
            throw new InvalidOperationException("references.yml 必须设置 publish.packageSource。");
        }

        return packageSource;
    }

    public NuGetPackageMetadata CreatePackageMetadata()
    {
        return new NuGetPackageMetadata(
            Authors,
            NuGetPackageRules.OptionalAbsoluteUri(RepositoryUrl, "repositoryUrl"));
    }

    public static PackagingConfig Load(string configPath)
    {
        return YamlConfigLoader.LoadRequired<PackagingConfig>(configPath);
    }
}

[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "由配置解析器实例化。")]
internal sealed class PublishConfig
{
    public required string PackageSource { get; init; }
}

[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "由配置解析器实例化。")]
internal sealed class ReferencePackageConfig
{
    public required string Id { get; init; }

    public string? Title { get; init; }

    public string? Description { get; init; }

    public string[] DependsOn { get; init; } = [];

    public required ReferenceAssetGroupConfig[] AssetGroups { get; init; }
}

[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "由配置解析器实例化。")]
internal sealed class ReferenceAssetGroupConfig
{
    public required string TargetFramework { get; init; }

    public required string Directory { get; init; }

    public required bool FollowReferences { get; init; }

    public required string[] RootAssemblies { get; init; }

    public string[] Exclude { get; init; } = [];
}
