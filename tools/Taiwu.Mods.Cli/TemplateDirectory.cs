using System.Text;

namespace Taiwu.Mods.Cli;

internal sealed class TemplateDirectory
{
    private const string TemplateFileExtension = ".scriban";

    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private readonly string _templateRoot;
    private readonly TemplateRenderer _renderer;

    private TemplateDirectory(string templateRoot, TemplateRenderer renderer)
    {
        _templateRoot = templateRoot;
        _renderer = renderer;
    }

    public static TemplateDirectory Create(string templateRoot, TemplateRenderer renderer)
    {
        if (!Directory.Exists(templateRoot))
        {
            throw new DirectoryNotFoundException($"Template directory does not exist: {templateRoot}");
        }

        return new TemplateDirectory(templateRoot, renderer);
    }

    public void CopyTo(string destinationRoot, bool force)
    {
        foreach (string templateFile in Directory.EnumerateFiles(_templateRoot, "*", SearchOption.AllDirectories))
        {
            CopyFile(templateFile, destinationRoot, force);
        }
    }

    private void CopyFile(string templateFile, string destinationRoot, bool force)
    {
        string relativePath = Path.GetRelativePath(_templateRoot, templateFile);
        bool renderContent = IsTemplateFile(relativePath);
        string destinationRelativePathTemplate = renderContent ? RemoveTemplateFileExtension(relativePath) : relativePath;
        string destinationRelativePath = _renderer.RenderPath(destinationRelativePathTemplate, templateFile);
        string destinationPath = GetDestinationPath(destinationRoot, destinationRelativePath, templateFile);

        if (File.Exists(destinationPath) && !force)
        {
            throw new IOException($"Destination file already exists: {destinationPath}");
        }

        string? destinationDirectory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(destinationDirectory))
        {
            _ = Directory.CreateDirectory(destinationDirectory);
        }

        if (renderContent)
        {
            string content = File.ReadAllText(templateFile, Encoding.UTF8);
            File.WriteAllText(destinationPath, _renderer.RenderContent(content, templateFile), Utf8NoBom);
            return;
        }

        File.Copy(templateFile, destinationPath, overwrite: force);
    }

    private static string GetDestinationPath(string destinationRoot, string destinationRelativePath, string templateFile)
    {
        if (string.IsNullOrWhiteSpace(destinationRelativePath))
        {
            throw new InvalidOperationException($"Template path rendered to an empty path: {templateFile}");
        }

        string destinationPath = Path.GetFullPath(Path.Combine(destinationRoot, destinationRelativePath));
        string destinationRootPath = Path.GetFullPath(destinationRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        if (Path.IsPathRooted(destinationRelativePath)
            || ContainsParentDirectorySegment(destinationRelativePath)
            || !destinationPath.StartsWith(destinationRootPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Template path rendered outside the destination root: {templateFile}");
        }

        return destinationPath;
    }

    private static bool ContainsParentDirectorySegment(string path)
    {
        return path
            .Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries)
            .Any(segment => string.Equals(segment, "..", StringComparison.Ordinal));
    }

    private static bool IsTemplateFile(string path)
    {
        return path.EndsWith(TemplateFileExtension, StringComparison.OrdinalIgnoreCase);
    }

    private static string RemoveTemplateFileExtension(string path)
    {
        return path[..^TemplateFileExtension.Length];
    }
}
