using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Taiwu.ModKit.Tooling.NuGet;

public static class NuGetPackageWriter
{
    private const string GeneratedLicenseFileName = "LICENSE.md";
    private const string GeneratedReadmeFileName = "README.md";
    private const string ReferenceOnlyPackageWarningCode = "NU5131";

    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public static async Task WriteAsync(NuGetPackageSpec package, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(package);

        ValidatePackageFiles(package);

        string outputDirectory = Path.GetDirectoryName(package.OutputPath)
            ?? throw new InvalidOperationException("包输出路径必须包含目录。");
        _ = Directory.CreateDirectory(outputDirectory);
        if (File.Exists(package.OutputPath))
        {
            File.Delete(package.OutputPath);
        }

        string tempDirectory = Path.Combine(Path.GetTempPath(), $"taiwu-modkit-pack-{Guid.NewGuid():N}");
        try
        {
            _ = Directory.CreateDirectory(tempDirectory);
            string nuspecPath = Path.Combine(tempDirectory, "package.nuspec");
            string licensePath = Path.Combine(tempDirectory, GeneratedLicenseFileName);
            string readmePath = Path.Combine(tempDirectory, GeneratedReadmeFileName);

            WriteText(licensePath, CreateLicenseNotice(package));
            WriteText(readmePath, CreateReadme(package));
            WriteXml(nuspecPath, CreateNuspec(package, licensePath, readmePath));
            await PackNuspecAsync(package, nuspecPath, outputDirectory, tempDirectory, cancellationToken);

            if (!File.Exists(package.OutputPath))
            {
                throw new FileNotFoundException($"dotnet pack 没有生成预期包文件: {package.OutputPath}", package.OutputPath);
            }
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }

    private static async Task PackNuspecAsync(
        NuGetPackageSpec package,
        string nuspecPath,
        string outputDirectory,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        List<string> arguments =
        [
            "pack",
            nuspecPath,
            "--output",
            outputDirectory,
            "--no-restore",
            "--no-logo",
            "--verbosity",
            "minimal",
        ];

        string displayCommand = $"dotnet pack {nuspecPath} --output {outputDirectory} --no-restore --no-logo --verbosity minimal";
        if (HasReferenceOnlyAssets(package))
        {
            // Reference packages intentionally omit lib assets so game DLLs are not copied at runtime.
            arguments.Add($"-p:NoWarn={ReferenceOnlyPackageWarningCode}");
            displayCommand += $" -p:NoWarn={ReferenceOnlyPackageWarningCode}";
        }

        await ProcessRunner.RunAsync("dotnet", arguments, displayCommand, cancellationToken, workingDirectory);
    }

    private static XDocument CreateNuspec(NuGetPackageSpec package, string licensePath, string readmePath)
    {
        XNamespace ns = "http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd";
        List<object> metadata =
        [
            new XElement(ns + "id", package.Id),
            new XElement(ns + "version", package.Version),
            new XElement(ns + "title", package.Title),
            new XElement(ns + "authors", package.Metadata.Authors),
            new XElement(ns + "requireLicenseAcceptance", true),
            new XElement(ns + "license", new XAttribute("type", "file"), GeneratedLicenseFileName),
            new XElement(ns + "description", package.Description),
            new XElement(ns + "readme", GeneratedReadmeFileName),
            CreateDependencies(ns, package),
        ];

        XElement? references = CreateReferences(ns, package);
        if (references is not null)
        {
            metadata.Add(references);
        }

        if (package.Metadata.RepositoryUri is not null)
        {
            metadata.Add(
                new XElement(
                    ns + "repository",
                    new XAttribute("type", "git"),
                    new XAttribute("url", package.Metadata.RepositoryUri.AbsoluteUri)));
        }

        return new XDocument(
            new XElement(
                ns + "package",
                new XElement(ns + "metadata", metadata),
                CreateFiles(ns, package, licensePath, readmePath)));
    }

    private static XElement CreateDependencies(XNamespace ns, NuGetPackageSpec package)
    {
        return new XElement(
            ns + "dependencies",
            package.DependencyGroups
                .OrderBy(group => group.TargetFramework, StringComparer.OrdinalIgnoreCase)
                .Select(group =>
                {
                    string targetFramework = NuGetPackageRules.RequiredTargetFramework(
                        group.TargetFramework,
                        $"包 '{package.Id}' 的依赖目标框架");
                    XElement dependencyGroup = new(ns + "group", new XAttribute("targetFramework", targetFramework));
                    foreach (NuGetDependency dependency in group.Dependencies.OrderBy(dependency => dependency.Id, StringComparer.OrdinalIgnoreCase))
                    {
                        string dependencyId = NuGetPackageRules.RequiredPackageId(
                            dependency.Id,
                            $"包 '{package.Id}' 的依赖 id");
                        dependencyGroup.Add(
                            new XElement(
                                ns + "dependency",
                                new XAttribute("id", dependencyId),
                                new XAttribute(
                                    "version",
                                    NuGetPackageRules.ToExactVersionRange(
                                        dependency.Version,
                                        $"包 '{package.Id}' 的依赖 '{dependencyId}' version"))));
                    }

                    return dependencyGroup;
                }));
    }

    private static XElement? CreateReferences(XNamespace ns, NuGetPackageSpec package)
    {
        HashSet<string> runtimeAssets = ReadRuntimeAssetNames(package);
        IGrouping<string, string>[] referenceGroups =
        [
            .. ReadReferenceFiles(package)
            .Where(reference => runtimeAssets.Contains(reference.AssetName))
            .GroupBy(reference => reference.TargetFramework, reference => reference.FileName, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase),
        ];

        if (referenceGroups.Length == 0)
        {
            return null;
        }

        return new XElement(
            ns + "references",
            referenceGroups.Select(group => new XElement(
                ns + "group",
                new XAttribute("targetFramework", group.Key),
                group
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Order(StringComparer.OrdinalIgnoreCase)
                    .Select(fileName => new XElement(ns + "reference", new XAttribute("file", fileName))))));
    }

    private static XElement CreateFiles(XNamespace ns, NuGetPackageSpec package, string licensePath, string readmePath)
    {
        XElement files = new(ns + "files");
        foreach (NuGetPackageFile file in package.Files.OrderBy(file => file.EntryName, StringComparer.OrdinalIgnoreCase))
        {
            files.Add(new XElement(
                ns + "file",
                new XAttribute("src", file.SourcePath),
                new XAttribute("target", NormalizePackagePath(file.EntryName))));
        }

        files.Add(new XElement(
            ns + "file",
            new XAttribute("src", licensePath),
            new XAttribute("target", GeneratedLicenseFileName)));
        files.Add(new XElement(
            ns + "file",
            new XAttribute("src", readmePath),
            new XAttribute("target", GeneratedReadmeFileName)));

        return files;
    }

    private static void ValidatePackageFiles(NuGetPackageSpec package)
    {
        _ = NuGetPackageRules.RequiredPackageId(package.Id, "包 id");
        _ = NuGetPackageRules.RequiredPackageVersion(package.Version, $"包 '{package.Id}' version");
        ValidatePackageMetadata(package);

        if (package.Files.Count == 0)
        {
            throw new InvalidOperationException($"包 '{package.Id}' 至少需要包含一个文件。");
        }

        ValidateDependencyGroups(package);

        HashSet<string> entryNames = new(StringComparer.OrdinalIgnoreCase);
        foreach (NuGetPackageFile file in package.Files)
        {
            string entryName = NormalizePackagePath(file.EntryName);
            ValidatePackageEntryName(package, entryName);
            if (IsGeneratedPackageFile(entryName))
            {
                throw new InvalidOperationException($"包 '{package.Id}' 的文件目标路径与生成文件冲突: {entryName}");
            }

            if (!entryNames.Add(entryName))
            {
                throw new InvalidOperationException($"包 '{package.Id}' 包含重复文件目标路径: {entryName}");
            }
        }

        _ = ReadReferenceFiles(package).ToArray();
    }

    private static void ValidatePackageMetadata(NuGetPackageSpec package)
    {
        if (string.IsNullOrWhiteSpace(package.Metadata.Authors))
        {
            throw new InvalidOperationException($"包 '{package.Id}' 的 authors 不能为空。");
        }

        if (package.Metadata.RepositoryUri is { IsAbsoluteUri: false })
        {
            throw new InvalidOperationException($"包 '{package.Id}' 的 repositoryUrl 必须是绝对 URI: {package.Metadata.RepositoryUri}");
        }
    }

    private static void ValidateDependencyGroups(NuGetPackageSpec package)
    {
        if (package.DependencyGroups.Count == 0)
        {
            throw new InvalidOperationException($"包 '{package.Id}' 至少需要包含一个依赖目标框架组。");
        }

        HashSet<string> targetFrameworks = new(StringComparer.OrdinalIgnoreCase);
        foreach (NuGetDependencyGroup group in package.DependencyGroups)
        {
            string targetFramework = NuGetPackageRules.RequiredTargetFramework(
                group.TargetFramework,
                $"包 '{package.Id}' 的依赖目标框架");

            if (!targetFrameworks.Add(targetFramework))
            {
                throw new InvalidOperationException($"包 '{package.Id}' 重复声明依赖目标框架: {targetFramework}");
            }

            HashSet<string> dependencyIds = new(StringComparer.OrdinalIgnoreCase);
            foreach (NuGetDependency dependency in group.Dependencies)
            {
                string dependencyId = NuGetPackageRules.RequiredPackageId(
                    dependency.Id,
                    $"包 '{package.Id}' 的依赖 id");
                _ = NuGetPackageRules.RequiredPackageVersion(
                    dependency.Version,
                    $"包 '{package.Id}' 的依赖 '{dependencyId}' version");
                if (!dependencyIds.Add(dependencyId))
                {
                    throw new InvalidOperationException($"包 '{package.Id}' 的依赖目标框架 '{targetFramework}' 重复声明依赖: {dependencyId}");
                }
            }
        }
    }

    private static void ValidatePackageEntryName(NuGetPackageSpec package, string entryName)
    {
        if (string.IsNullOrWhiteSpace(entryName))
        {
            throw new InvalidOperationException($"包 '{package.Id}' 包含空目标路径。");
        }

        if (IsRootedPackagePath(entryName))
        {
            throw new InvalidOperationException($"包 '{package.Id}' 的文件目标路径必须是包内相对路径: {entryName}");
        }

        string[] segments = entryName.Split('/');
        if (segments.Any(segment => segment.Length == 0 || segment is "." or ".."))
        {
            throw new InvalidOperationException($"包 '{package.Id}' 的文件目标路径不能包含空段、'.' 或 '..': {entryName}");
        }
    }

    private static bool IsGeneratedPackageFile(string entryName)
    {
        return string.Equals(entryName, GeneratedLicenseFileName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(entryName, GeneratedReadmeFileName, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRootedPackagePath(string entryName)
    {
        return Path.IsPathRooted(entryName)
            || (entryName.Length > 0 && entryName[0] == '/')
            || HasWindowsDrivePrefix(entryName);
    }

    private static bool HasWindowsDrivePrefix(string path)
    {
        return path.Length >= 2
            && path[1] == ':'
            && ((path[0] >= 'A' && path[0] <= 'Z') || (path[0] >= 'a' && path[0] <= 'z'));
    }

    private static IEnumerable<ReferenceFile> ReadReferenceFiles(NuGetPackageSpec package)
    {
        foreach (NuGetPackageFile file in package.Files)
        {
            string entryName = NormalizePackagePath(file.EntryName);
            if (!entryName.StartsWith("ref/", StringComparison.OrdinalIgnoreCase)
                || !entryName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string[] segments = entryName.Split('/');
            if (segments.Length != 3 || string.IsNullOrWhiteSpace(segments[1]) || string.IsNullOrWhiteSpace(segments[2]))
            {
                throw new InvalidOperationException($"包 '{package.Id}' 的 ref DLL 必须位于 ref/<tfm>/<file>.dll: {entryName}");
            }

            string targetFramework = NuGetPackageRules.RequiredTargetFramework(
                segments[1],
                $"包 '{package.Id}' 的 ref 目标框架");

            yield return new ReferenceFile(targetFramework, segments[2], $"{targetFramework}/{segments[2]}");
        }
    }

    private static HashSet<string> ReadRuntimeAssetNames(NuGetPackageSpec package)
    {
        return package.Files
            .Select(file => NormalizePackagePath(file.EntryName))
            .Where(path => path.StartsWith("lib/", StringComparison.OrdinalIgnoreCase))
            .Select(path => path["lib/".Length..])
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static bool HasReferenceOnlyAssets(NuGetPackageSpec package)
    {
        HashSet<string> runtimeAssets = ReadRuntimeAssetNames(package);
        return ReadReferenceFiles(package).Any(reference => !runtimeAssets.Contains(reference.AssetName));
    }

    private static string CreateLicenseNotice(NuGetPackageSpec package)
    {
        return $"""
            # License Notice

            This generated package requires license acceptance before use.

            Package ID: `{package.Id}`

            The packaged assemblies are not licensed by Taiwu.ModKit. Their use and distribution are governed by the applicable game, Unity package, or third-party assembly licenses.

            Publish this package only to private or otherwise controlled feeds where you have confirmed the necessary rights.
            """;
    }

    private static string CreateReadme(NuGetPackageSpec package)
    {
        return $"""
            # {package.Title}

            Package ID: `{package.Id}`

            This package is generated by Taiwu.ModKit tooling from local game or Unity package assemblies.

            It is intended for controlled mod development feeds. Verify that you have the right to use and distribute the packaged assemblies before publishing.
            """;
    }

    private static string NormalizePackagePath(string path)
    {
        return path.Replace('\\', '/');
    }

    private static void WriteText(string path, string content)
    {
        File.WriteAllText(path, content, Utf8NoBom);
    }

    private static void WriteXml(string path, XDocument document)
    {
        XmlWriterSettings settings = new()
        {
            Encoding = Utf8NoBom,
            Indent = true,
            NewLineChars = "\n",
        };

        using XmlWriter writer = XmlWriter.Create(path, settings);
        document.Save(writer);
    }

    private sealed record ReferenceFile(string TargetFramework, string FileName, string AssetName);
}
