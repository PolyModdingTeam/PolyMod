
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using HarmonyLib;
using MoonSharp.Interpreter;
using Polytopia.Data;
using UnityEngine;
using ScriptRuntimeException = MoonSharp.Interpreter.ScriptRuntimeException;

namespace PolyMod.modApi;

[MoonSharpUserData]
public static class Patch
{
    private record class PatchTuple
    {
        public List<(Closure hook, Script script)> patches;
        public bool isAlreadyBeingPatched;
    }
    private static readonly Harmony harmony = new Harmony("com.polymoddingteam.polymod");
    private static readonly HarmonyMethod prefixMethod = new HarmonyMethod(typeof(Patch).GetMethod(nameof(Prefix), BindingFlags.NonPublic | BindingFlags.Static));
    private static readonly HarmonyMethod prefixVoidMethod = new HarmonyMethod(typeof(Patch).GetMethod(nameof(PrefixVoid), BindingFlags.NonPublic | BindingFlags.Static));

    private static readonly Dictionary<MethodBase, PatchTuple> patches = new ();

    private static readonly Dictionary<string, MethodBase> whiteList = new();
    static Patch()
    {
        BuildWhiteList();
    }
    private static void BuildWhiteList()
    {
        var gameLogicAsm = typeof(GameLogicData).Assembly;
        foreach (var type in gameLogicAsm.GetTypes())
        {
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            {
                whiteList[$"{type.FullName}.{method.Name}"] = method;
            }
        }
        var uiAsm = typeof(PopupButtonContainer).Assembly;
        foreach (var type in uiAsm.GetTypes())
        {
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            {
                whiteList[$"{type.FullName}.{method.Name}"] = method;
            }
        }
    }
    public static void Wrap(Script script, string targetMethod, Closure hook)
    {
        if (!whiteList.TryGetValue(targetMethod, out var methBase))
            throw new ScriptRuntimeException("Tried to patch non-whitelisted method!");
        Debug.Assert(methBase != null, "methBase != null");
        if (patches.TryGetValue(methBase, out var tuple))
        {
            Debug.Assert(tuple.patches != null, "list != null");
            tuple.patches.Add((hook, script));
            return;
        }
        Debug.Assert(hook != null, "hook != null");
        Debug.Assert(prefixMethod != null, "prefixMethod != null");
        tuple = new()
        {
            patches = new ()
            {
                (hook, script) 
            }
        };
        patches[methBase] = tuple;
        harmony.Patch(methBase, 
            (methBase is MethodInfo mi && mi.ReturnType != typeof(void))
                ? prefixMethod : prefixVoidMethod);
    }
    private static bool Prefix(MethodBase __originalMethod, ref object __result, object[] __args, object __instance)
    {
        if (!patches.TryGetValue(__originalMethod, out var tuple)) throw new InvalidOperationException();
        if (tuple.isAlreadyBeingPatched) return true;
        tuple.isAlreadyBeingPatched = true;
        try
        {
            DynValue ExecuteChain(int i)
            {
                if (i >= tuple.patches.Count)
                {
                    var lastScript = tuple.patches[i - 1].script;
                    var result = __originalMethod.Invoke(__instance, __args);
                    return DynValue.FromObject(lastScript, result);
                }

                var (currentHook, script) = tuple.patches[i];
                var origForThisHook = DynValue.FromObject(
                    script,
                    () => ExecuteChain(i + 1)
                );

                return __originalMethod.IsStatic ? currentHook.Call(origForThisHook, __args) : currentHook.Call(origForThisHook, __instance, __args);
            }
            __result = ExecuteChain(0).ToObject();
            return false;
        }
        catch (ScriptRuntimeException e)
        {
            Plugin.logger.LogError($"IN METHOD {__originalMethod.DeclaringType.FullName}.{__originalMethod.Name}({__originalMethod.GetParameters()})");
            Plugin.logger.LogError(e.DecoratedMessage);
            return true;
        }
        finally
        {
            tuple.isAlreadyBeingPatched = false;
        }
    }
    private static bool PrefixVoid(MethodBase __originalMethod, object[] __args, object? __instance)
    {
        object? nothing = null;
        return Prefix(__originalMethod, ref nothing, __args, __instance);
    }
}