using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Taiwu.ModKit.Tooling;

public static class YamlConfigLoader
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .WithDuplicateKeyChecking()
        .Build();

    public static T LoadRequired<T>(string configPath)
        where T : class
    {
        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException(
                $"配置文件不存在: {configPath}。",
                configPath);
        }

        try
        {
            T? config = Deserializer.Deserialize<T>(File.ReadAllText(configPath));

            return config ?? throw new InvalidOperationException($"配置文件为空: {configPath}");
        }
        catch (YamlException ex)
        {
            throw new InvalidOperationException($"配置文件格式无效: {configPath}: {ex.Message}", ex);
        }
    }
}
