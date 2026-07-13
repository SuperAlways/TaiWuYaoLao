using Taiwu.ModKit.Tooling;
using Taiwu.ModKit.Tooling.NuGet;

namespace Taiwu.ModKit.ReferencePackager;

internal sealed record ReferencePackage(
    NuGetPackageSpec PackageSpec,
    ReferencePackageAssetGroup[] AssetGroups)
{
    public string Id => PackageSpec.Id;

    public string Version => PackageSpec.Version;

    public string OutputPath => PackageSpec.OutputPath;

    public int AssemblyCount => AssetGroups.Sum(group => group.Assemblies.Length);
}

internal sealed record ReferencePackageAssetGroup(
    string TargetFramework,
    PackageAssembly[] Assemblies);
