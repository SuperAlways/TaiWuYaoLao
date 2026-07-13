namespace Taiwu.ModKit.Tooling.NuGet;

public sealed record NuGetPackageSpec(
    string Id,
    string Version,
    string Title,
    string Description,
    NuGetPackageMetadata Metadata,
    IReadOnlyCollection<NuGetDependencyGroup> DependencyGroups,
    IReadOnlyCollection<NuGetPackageFile> Files,
    string OutputPath);

public sealed record NuGetPackageMetadata(string Authors, Uri? RepositoryUri);

public sealed record NuGetDependencyGroup(
    string TargetFramework,
    IReadOnlyCollection<NuGetDependency> Dependencies);

public sealed record NuGetDependency(string Id, string Version);

public sealed record NuGetPackageFile(string SourcePath, string EntryName);

public sealed record NuGetPackageOutput(string Id, string Version, string OutputPath);
