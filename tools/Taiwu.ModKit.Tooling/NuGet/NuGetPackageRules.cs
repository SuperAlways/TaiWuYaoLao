using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Versioning;

namespace Taiwu.ModKit.Tooling.NuGet;

public static class NuGetPackageRules
{
    public static string RequiredPackageId(string? packageId, string fieldName)
    {
        string value = RequiredText(packageId, fieldName);
        if (!PackageIdValidator.IsValidPackageId(value))
        {
            throw new InvalidOperationException($"{fieldName} 不是合法的 NuGet package id: {value}");
        }

        return value;
    }

    public static string RequiredTargetFramework(string? targetFramework, string fieldName)
    {
        string value = RequiredText(targetFramework, fieldName);
        NuGetFramework framework = NuGetFramework.ParseFolder(value);
        if (framework.IsUnsupported)
        {
            throw new InvalidOperationException($"{fieldName} 不是 NuGet 可识别的 target framework: {value}");
        }

        return framework.GetShortFolderName();
    }

    public static string RequiredPackageVersion(string? version, string fieldName)
    {
        string value = RequiredText(version, fieldName);
        if (!NuGetVersion.TryParse(value, out NuGetVersion? nuGetVersion))
        {
            throw new InvalidOperationException($"{fieldName} 不是合法的 NuGet 版本: {value}");
        }

        return nuGetVersion.ToNormalizedString();
    }

    public static Uri? OptionalAbsoluteUri(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string trimmedValue = value.Trim();
        if (!Uri.TryCreate(trimmedValue, UriKind.Absolute, out Uri? uri))
        {
            throw new InvalidOperationException($"{fieldName} 不是合法的绝对 URI: {trimmedValue}");
        }

        return uri;
    }

    public static string ToExactVersionRange(string version, string fieldName)
    {
        return $"[{RequiredPackageVersion(version, fieldName)}]";
    }

    private static string RequiredText(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{fieldName} 不能为空。");
        }

        return value.Trim();
    }
}
