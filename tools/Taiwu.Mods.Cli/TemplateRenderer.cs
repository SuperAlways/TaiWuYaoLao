using Scriban;
using Scriban.Runtime;
using Scriban.Syntax;

namespace Taiwu.Mods.Cli;

internal sealed class TemplateRenderer
{
    private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, object>> _scopes;

    private TemplateRenderer(IReadOnlyDictionary<string, IReadOnlyDictionary<string, object>> scopes)
    {
        _scopes = scopes;
    }

    public static TemplateRenderer ForMod(string modName, string modVersion)
    {
        return new TemplateRenderer(
            new Dictionary<string, IReadOnlyDictionary<string, object>>(StringComparer.Ordinal)
            {
                ["mod"] = new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["name"] = modName,
                    ["version"] = modVersion,
                },
            });
    }

    public static TemplateRenderer ForSharedProject(string projectName, SharedProjectSide side, string targetFramework)
    {
        return new TemplateRenderer(
            new Dictionary<string, IReadOnlyDictionary<string, object>>(StringComparer.Ordinal)
            {
                ["project"] = new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["name"] = projectName,
                    ["side"] = side.ToString(),
                    ["target_framework"] = targetFramework,
                },
            });
    }

    public string RenderPath(string templatePath, string sourcePath)
    {
        return Render(templatePath, $"{sourcePath}:path");
    }

    public string RenderContent(string templateText, string sourcePath)
    {
        return Render(templateText, sourcePath);
    }

    private string Render(string templateText, string sourcePath)
    {
        Template template = Template.Parse(templateText, sourcePath);
        if (template.HasErrors)
        {
            throw new InvalidOperationException(CreateErrorMessage("parse", sourcePath, template.Messages));
        }

        TemplateContext context = CreateContext();
        try
        {
            return template.Render(context);
        }
        catch (ScriptRuntimeException ex)
        {
            throw new InvalidOperationException($"Failed to render template '{sourcePath}': {ex.Message}", ex);
        }
    }

    private TemplateContext CreateContext()
    {
        ScriptObject globals = [];
        foreach (KeyValuePair<string, IReadOnlyDictionary<string, object>> scope in _scopes)
        {
            ScriptObject values = [];
            foreach (KeyValuePair<string, object> value in scope.Value)
            {
                values.SetValue(value.Key, value.Value, readOnly: true);
            }

            globals.SetValue(scope.Key, values, readOnly: true);
        }

        TemplateContext context = new()
        {
            StrictVariables = true,
        };
        context.PushGlobal(globals);

        return context;
    }

    private static string CreateErrorMessage(string operation, string sourcePath, IEnumerable<object> messages)
    {
        return $"Failed to {operation} template '{sourcePath}':{Environment.NewLine}{string.Join(Environment.NewLine, messages)}";
    }
}
