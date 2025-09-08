using System.Linq;
using System.Text.Json.Nodes;
using BepInEx.Logging;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppSystem.Collections;
using MoonSharp.Interpreter;
using MoonSharp.Interpreter.Debugging;
using MoonSharp.Interpreter.Interop;
using Newtonsoft.Json.Linq;
using PolyMod.modApi;
using Polytopia.Data;
using PolytopiaBackendBase.Game;
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

        UserData.RegisterType(typeof(Registry));
        lua.Globals["Registry"] = typeof(Registry);
        
        UserData.RegisterType(typeof(Patch));
        lua.Globals["Patch"] = typeof(Patch);

        UserData.RegisterType(typeof(General));
        lua.Globals["General"] = typeof(General);

        UserData.RegisterType(typeof(LuaEnumCache));
        lua.Globals["EnumCache"] = typeof(LuaEnumCache);
        
        LuaEnumCache.RegisterEnum<GameMode>("GameMode");
        LuaEnumCache.RegisterEnum<TribeData.Type>("TribeType");
        LuaEnumCache.RegisterEnum<TechData.Type>("TechType");
        LuaEnumCache.RegisterEnum<UnitData.Type>("UnitType");
        LuaEnumCache.RegisterEnum<ImprovementData.Type>("ImprovementType");
        LuaEnumCache.RegisterEnum<Polytopia.Data.TerrainData.Type>("TerrainType");
        LuaEnumCache.RegisterEnum<ResourceData.Type>("ResourceType");
        LuaEnumCache.RegisterEnum<TaskData.Type>("TaskType");
        LuaEnumCache.RegisterEnum<TribeAbility.Type>("TribeAbilityType");
        LuaEnumCache.RegisterEnum<UnitAbility.Type>("UnitAbilityType");
        LuaEnumCache.RegisterEnum<ImprovementAbility.Type>("ImprovementAbilityType");
        LuaEnumCache.RegisterEnum<PlayerAbility.Type>("PlayerAbilityType");
        LuaEnumCache.RegisterEnum<UnitData.WeaponEnum>("WeaponType");
        LuaEnumCache.RegisterEnum<KeyCode>("KeyCode");
        LuaEnumCache.RegisterEnum<SkinType>("SkinType");
        

        UserData.RegisterType<LuaConfig>();
        lua.Globals["Config"] = new LuaConfig(modName, Config<JsonNode>.ConfigTypes.PerMod, lua);
        lua.Globals["ExposedConfig"] = new LuaConfig(modName, Config<JsonNode>.ConfigTypes.Exposed, lua);
        
        UserData.RegisterType(typeof(Input));
        lua.Globals["Input"] = typeof(Input);
        
        UserData.RegisterExtensionType(typeof(Il2cppEnumerableExtensions));
        
        lua.Globals["clrTypeOf"] = DynValue.NewCallback((_, args) => DynValue.NewString(args[0].ToObject().GetType().FullName));
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
            logger.LogError(e.DecoratedMessage ?? e.Message);
            foreach (var item in e.CallStack)
            {
                logger.LogError($"AT {item.Location}");
            }
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

public static class Il2cppEnumerableExtensions
{
    public static System.Collections.IEnumerable ToMono<T>(this Il2CppSystem.Collections.Generic.IEnumerable<T> enumerable)
    {
        var list = Il2CppSystem.Linq.Enumerable.ToList(enumerable);
        foreach (T v in list)
        {
            yield return v;
        }
    }
}