using System.Text.Json;
using System.Text.Json.Nodes;
using Scriban;
using Scriban.Runtime;

namespace PolyMod.Managers;

public class GldConfigTemplate
{
    private static readonly string ConfigPath = Path.Combine(Plugin.BASE_PATH, "mods.json");
    
    private readonly string _templateText;
    private JsonObject _currentConfig = new();
    private string _modName;

    public GldConfigTemplate(string templateText, string modName)
    {
        _templateText = templateText;
        _modName = modName;
        Load();
    }
    private void Load()
    {
        if (File.Exists(ConfigPath))
        {
            var json = File.ReadAllText(ConfigPath);
            if (JsonNode.Parse(json) is JsonObject modsConfig 
                && modsConfig.TryGetPropertyValue(_modName, out var modConfigNode) 
                && modConfigNode is JsonObject modConfig)
            {
                _currentConfig = modConfig;
                return;
            }
        }
        _currentConfig = new JsonObject();
    }

    public string Render()
    {
        if (!_templateText.Contains("{{")) return _templateText;
        var template = Template.Parse(_templateText);
        var context = new TemplateContext();
        var scriptObject = new ScriptObject();
        
        bool changedConfig = false;
        scriptObject.Import("config", 
            new Func<string, string, string>((key, defaultValue) =>
            {
                if (_currentConfig.TryGetPropertyValue(key, out var token) && token != null)
                {
                    return token.ToString();
                }

                changedConfig = true;
                _currentConfig[key] = defaultValue;
                
                return defaultValue;
            })
        );
        context.PushGlobal(scriptObject);
        var result = template.Render(context);
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

        modsConfigJson[_modName] = _currentConfig;
        File.WriteAllText(ConfigPath, modsConfigJson.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }
}