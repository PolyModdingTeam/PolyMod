using System.Text.Json;
using System.Text.Json.Nodes;
using Scriban;
using Scriban.Runtime;

namespace PolyMod.Api;

/// <summary>
/// A GLD patch template with config values.
/// PolyMod uses it for each <c>patch.json</c> and <c>patch_*.json</c> file of a mod.
/// </summary>
/// <remarks>
/// <para>
/// A patch file can use the Scriban template syntax. In the template, write <c>{{ config "key" "default" }}</c> to get a config value.
/// The function gets the value with that key from the section of the mod in <c>mods.json</c>, as text.
/// If the key does not exist, the function adds the key with the default value and gives the default value.
/// </para>
/// <para>
/// The users can change the values in <c>mods.json</c>. The changes apply the next time that PolyMod loads the patch.
/// </para>
/// </remarks>
public class GldConfigTemplate
{
    private static readonly string ConfigPath = Path.Combine(Plugin.BASE_PATH, "mods.json");

    private readonly string templateText;
    private JsonObject currentConfig = new();
    private string modName;

    /// <summary>
    /// Makes a template from the text of a patch file and reads the config values of the mod from <c>mods.json</c>.
    /// </summary>
    /// <param name="templateText">The text of the patch file.</param>
    /// <param name="modName">The mod id. The template uses it as the key of the mod section in <c>mods.json</c>.</param>
    public GldConfigTemplate(string templateText, string modName)
    {
        this.templateText = templateText;
        this.modName = modName;
        Load();
    }
    /// <summary>
    /// Reads the config values of the mod from <c>mods.json</c>.
    /// If the file does not exist, or the mod does not have a JSON object in it, the method uses an empty config.
    /// </summary>
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

    /// <summary>
    /// Renders the template with the config values of the mod.
    /// If the text does not contain <c>{{</c>, the method gives the text without changes.
    /// If the template uses a key that is not in the config, the method writes the default value to <c>mods.json</c>.
    /// </summary>
    /// <returns>The rendered text, or null if the template has an error. The method writes the error to the log.</returns>
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

    /// <summary>
    /// Writes the config values of the mod to <c>mods.json</c> as indented JSON.
    /// The method replaces the section of the mod and keeps the sections of other mods.
    /// If the file does not exist or does not contain a JSON object, the method makes a new file.
    /// </summary>
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
