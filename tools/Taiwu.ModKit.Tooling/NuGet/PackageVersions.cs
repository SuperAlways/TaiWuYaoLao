using NuGet.Versioning;

namespace Taiwu.ModKit.Tooling.NuGet;

public static class PackageVersions
{
    public static string RequireNuGetPackageVersion(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            throw new InvalidOperationException("必须通过 --version 提供包版本。");
        }

        string normalizedVersion = version.Trim();
        if (!NuGetVersion.TryParse(normalizedVersion, out NuGetVersion? parsedVersion))
        {
            throw new InvalidOperationException($"包版本不是有效的 NuGet 版本: {version}");
        }

        return parsedVersion.ToNormalizedString();
    }
}
