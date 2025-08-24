using System.Linq;
using System.Text.Json.Nodes;
using BepInEx.Logging;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppSystem.Collections;
using MoonSharp.Interpreter;
using MoonSharp.Interpreter.Interop;
using Newtonsoft.Json.Linq;
using PolyMod.modApi;
using Polytopia.Data;
using UnityEngine;
using IDisposable = Il2CppSystem.IDisposable;
using Input = PolyMod.modApi.Input;
using Object = Il2CppSystem.Object;

namespace PolyMod;

public class LuaManager
{
    private Script lua;
    private ManualLogSource logger;
    static LuaManager()
    {
        Script.GlobalOptions.CustomConverters.SetClrToScriptCustomConversion(typeof(IEnumerable<>), (_, enumerable) =>
        {
            IEnumerator enumerator =  ((IEnumerable) enumerable).GetEnumerator();
            return DynValue.NewCallback((context, args) =>
            {
                if (enumerator.MoveNext())
                {
                    // Return the current item as a single value tuple
                    return DynValue.NewTuple(DynValue.FromObject(context.GetScript(), enumerator.Current));
                }
                else
                {
                    // Iterator is finished
                    ((object)enumerator as IDisposable).Dispose();
                    return DynValue.Nil;
                }
            });
        });
    }
    public LuaManager(string modName)
    {
        logger = BepInEx.Logging.Logger.CreateLogSource($"PolyMod] [{modName}");
        
        lua = new Script
        {
            Options =
            {
                DebugPrint = (message) => logger.LogInfo(message),
            }
        };

        void RegisterTypesAndExtensions(IEnumerable<Type> types)
        {
            foreach (var type in types)
            {
                if (type is not { IsPublic: true }) continue;
                if (type is { IsSealed: true, IsAbstract: true }) UserData.RegisterExtensionType(type);
                else
                {
                    UserData.RegisterType(type);
                    lua.Globals[type.Name] = type;
                }
            }
        }
        
        RegisterTypesAndExtensions(typeof(GameLogicData).Assembly.GetTypes());
        RegisterTypesAndExtensions(typeof(PopupButtonContainer).Assembly.GetTypes());
        RegisterTypesAndExtensions(typeof(Enumerable).Assembly.GetTypes()); // linq

        #region PolyMod.*
        UserData.RegisterType(typeof(Patch));
        lua.Globals["Patch"] = typeof(Patch);

        UserData.RegisterType(typeof(General));
        lua.Globals["General"] = typeof(General);

        UserData.RegisterType<LuaConfig>();
        lua.Globals["Config"] = new LuaConfig(modName, Config<JsonNode>.ConfigTypes.PerMod, lua);
        lua.Globals["ExposedConfig"] = new LuaConfig(modName, Config<JsonNode>.ConfigTypes.Exposed, lua);
        
        UserData.RegisterType(typeof(Input));
        lua.Globals["Input"] = typeof(Input);
        #endregion
        
        #region Il2cppSystem.*
        UserData.RegisterType(typeof(Il2CppReferenceArray<>));
        UserData.RegisterType(typeof(Il2CppSystem.Collections.Generic.Dictionary<,>));
        UserData.RegisterType(typeof(Il2CppSystem.Collections.Generic.List<>));
        UserData.RegisterType(typeof(Il2CppSystem.Collections.Generic.IEnumerable<>));
        
        UserData.RegisterType<JToken>();
        UserData.RegisterType<JObject>();
        UserData.RegisterType<JArray>();
        
        lua.Globals["JToken"] = typeof(JToken);
        lua.Globals["JObject"] = typeof(JObject);
        lua.Globals["JArray"] = typeof(JArray);
        #endregion
        
        #region UnityEngine.*
        UserData.RegisterType<Vector2>();
        UserData.RegisterType<Vector3>();
        UserData.RegisterType<Quaternion>();
        UserData.RegisterType<Color>();
        UserData.RegisterType<Color32>();

        UserData.RegisterType<GameObject>();
        UserData.RegisterType<Component>();
        UserData.RegisterType<Transform>();
        UserData.RegisterType<RectTransform>();

        UserData.RegisterType<Sprite>();
        UserData.RegisterType<Texture2D>();

        UserData.RegisterType<Canvas>();
        
        UserData.RegisterType<Mathf>();
        UserData.RegisterType<Time>();
        
        lua.Globals["Vector2"] = typeof(Vector2);
        lua.Globals["Vector3"] = typeof(Vector3);
        lua.Globals["Quaternion"] = typeof(Quaternion);
        lua.Globals["Color"] = typeof(Color);
        lua.Globals["Color32"] = typeof(Color32);

        lua.Globals["Mathf"] = typeof(Mathf);
        lua.Globals["Time"] = typeof(Time);
        
        #endregion
    }
    public LuaManager(Mod mod) : this(mod.id) { }
    public void Execute(List<string> codes)
    {
        foreach (var code in codes)
        {
            lua.DoString(code);
        }
    }
    public void Execute(string code, string fileName)
    {
        try
        {
            lua.DoString(code, codeFriendlyName:fileName);
        }
        catch (ScriptRuntimeException e)
        {
            logger.LogError(e.DecoratedMessage);
        }
    }
    public void ExecuteFile(string path)
    {
        try
        {
            lua.DoFile(path);
        }
        catch (ScriptRuntimeException e)
        {
            logger.LogError(e.DecoratedMessage);
        }
    }
}
