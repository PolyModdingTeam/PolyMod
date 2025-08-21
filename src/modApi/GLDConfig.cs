using System.Text.Json;
using System.Text.Json.Nodes;
using Scriban;
using Scriban.Runtime;

namespace PolyMod.modApi;

internal class GldConfigTemplate
{
    private static readonly string ConfigPath = Path.Combine(Constants.BASE_PATH, "mods.json");
    
    private readonly string templateText;
    private JsonObject currentConfig = new();
    private string modName;

    public GldConfigTemplate(string templateText, string modName)
    {
        this.templateText = templateText;
        this.modName = modName;
        Load();
    }
    private void Load()
    {
        if (File.Exists(ConfigPath))
        {
            var json = File.ReadAllText(ConfigPath);
            if (JsonNode.Parse(json) is JsonObject modsConfig 
                && modsConfig.TryGetPropertyValue(modName, out var modConfigNode) 
                && modConfigNode is JsonObject modConfig)
            {
                currentConfig = modConfig;
                return;
            }
        }
        currentConfig = new JsonObject();
    }

    public string? Render()
    {
        if (!templateText.Contains("{{")) return templateText;
        var template = Template.Parse(templateText);
        var context = new TemplateContext();
        var scriptObject = new ScriptObject();
        
        bool changedConfig = false;
        scriptObject.Import("config", 
            new Func<string, string, string>((key, defaultValue) =>
            {
                if (currentConfig.TryGetPropertyValue(key, out var token) && token != null)
                {
                    return token.ToString();
                }

                changedConfig = true;
                currentConfig[key] = defaultValue;
                
                return defaultValue;
            })
        );
        context.PushGlobal(scriptObject);
        string? result;
        try
        {
            result = template.Render(context);
        }
        catch (Exception e)
        {
            Plugin.logger.LogError("error during parse of gld patch template: " + e.ToString());
            result = null;
        }
        if (changedConfig)
        {
            SaveChanges();
        }
        return result;
    }

    public void SaveChanges()
    {
        JsonObject modsConfigJson;
        if (File.Exists(ConfigPath))
        {
            var modsConfigText = File.ReadAllText(ConfigPath);
            modsConfigJson = (JsonNode.Parse(modsConfigText) as JsonObject) ?? new JsonObject();
        }
        else
        {
            modsConfigJson = new JsonObject();
        }

        modsConfigJson[modName] = currentConfig;
        File.WriteAllText(ConfigPath, modsConfigJson.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }
}