using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Taiwu.ModKit.ReferencePackager;

internal static class AssemblyReferenceReader
{
    private static readonly string[] FrameworkAssemblyPrefixes =
    [
        "Microsoft",
        "mscorlib",
        "netstandard",
        "System",
        "WindowsBase",
    ];

    public static string? ReadAssemblyName(string assemblyPath)
    {
        using FileStream stream = File.OpenRead(assemblyPath);
        using PEReader peReader = new(stream);
        if (!peReader.HasMetadata)
        {
            return null;
        }

        using MetadataReaderProvider provider = MetadataReaderProvider.FromMetadataImage(peReader.GetMetadata().GetContent());
        MetadataReader reader = provider.GetMetadataReader();
        return reader.GetString(reader.GetAssemblyDefinition().Name);
    }

    public static string[] ReadNonFrameworkReferences(string assemblyPath)
    {
        using FileStream stream = File.OpenRead(assemblyPath);
        using PEReader peReader = new(stream);
        if (!peReader.HasMetadata)
        {
            return [];
        }

        using MetadataReaderProvider provider = MetadataReaderProvider.FromMetadataImage(peReader.GetMetadata().GetContent());
        MetadataReader reader = provider.GetMetadataReader();

        return
        [
            .. reader.AssemblyReferences
            .Select(handle => reader.GetString(reader.GetAssemblyReference(handle).Name))
            .Where(referenceName => !IsFrameworkAssembly(referenceName))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase),
        ];
    }

    private static bool IsFrameworkAssembly(string assemblyName)
    {
        foreach (string prefix in FrameworkAssemblyPrefixes)
        {
            if (assemblyName.Equals(prefix, StringComparison.OrdinalIgnoreCase)
                || assemblyName.StartsWith(prefix + ".", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
