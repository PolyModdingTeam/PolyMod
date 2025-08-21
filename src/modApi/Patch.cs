using System.Linq.Expressions;
using System.Reflection;
using HarmonyLib;
using MonoMod.Utils;
using Polytopia.Data;
using UnityEngine;

namespace PolyMod.modApi;

public static class Patch
{
	private static readonly Harmony harmony = new Harmony("com.polymoddingteam.polymod");
	private static readonly HarmonyMethod prefixMethod = new HarmonyMethod(typeof(Patch).GetMethod(nameof(Prefix),
		BindingFlags.NonPublic | BindingFlags.Static));
	private delegate object? WrapperDelegate(object? instance, object[] args);
	private static Dictionary<MethodInfo, WrapperDelegate> patches = new();
	
	private static Dictionary<MethodInfo, Delegate> delegates = new();
	public static void Wrap<TDelegate, TObject>(TDelegate originalMethod, Func<TDelegate, TObject, TDelegate> patch) where TDelegate : Delegate
	{
		patches[originalMethod.Method] = CreateWrapperDelegate(originalMethod, patch);
		
		harmony.Patch(originalMethod.Method, prefixMethod);
	}
	public static void Wrap<TDelegate, TObject>(MethodInfo originalMethod, Func<TDelegate, TObject, TDelegate> patch) where TDelegate : Delegate
	{
		patches[originalMethod] = CreateWrapperDelegate((TDelegate)originalMethod.CreateDelegate(typeof(TDelegate)), patch);
		harmony.Patch(originalMethod, prefixMethod);
	}
	private static WrapperDelegate CreateWrapperDelegate<TDelegate, TObject>(TDelegate originalMethodDelegate, Func<TDelegate, TObject, TDelegate> patch) where TDelegate : Delegate
	{
		var instanceParam = Expression.Parameter(typeof(object));
		var argsParam = Expression.Parameter(typeof(object[]));

		var parameters = originalMethodDelegate.Method.GetParameters();
		var argExpressions = new Expression[parameters.Length];
		for (int i = 0; i < parameters.Length; i++)
		{
			var index = Expression.Constant(i);
			var arrayAccess = Expression.ArrayIndex(argsParam, index);
			var converted = Expression.Convert(arrayAccess, parameters[i].ParameterType);
			argExpressions[i] = converted;
		}
		
		var instanceCast = Expression.Convert(instanceParam, typeof(TObject));

		var patchCall = Expression.Invoke(Expression.Constant(patch), Expression.Constant(originalMethodDelegate), instanceCast);
		var replacementVar = Expression.Variable(typeof(TDelegate));
		var assignReplacement = Expression.Assign(replacementVar, patchCall);

		var replacementCall = Expression.Invoke(replacementVar, argExpressions);
		var block = Expression.Block(
			new[] { replacementVar },
			assignReplacement,
			replacementCall
		);

		var lambda = Expression.Lambda<WrapperDelegate>(block, instanceParam, argsParam);
		return lambda.Compile();
	}
	
	public static void Wrap<TDelegate>(TDelegate originalMethod, Func<TDelegate, TDelegate> patch) where TDelegate : Delegate
	{
		patches[originalMethod.Method] = CreateWrapperDelegate(originalMethod, patch);

		harmony.Patch(originalMethod.Method, prefixMethod);
	}
	public static void Wrap<TDelegate>(MethodInfo originalMethod, Func<TDelegate, TDelegate> patch) where TDelegate : Delegate
	{
		patches[originalMethod] = CreateWrapperDelegate((TDelegate) originalMethod.CreateDelegate(typeof(TDelegate)), patch);

		harmony.Patch(originalMethod, prefixMethod);
	}
	private static WrapperDelegate CreateWrapperDelegate<TDelegate>(TDelegate originalMethodDelegate, Func<TDelegate, TDelegate> patch) where TDelegate : Delegate
	{
		var instanceParam = Expression.Parameter(typeof(object));
		var argsParam = Expression.Parameter(typeof(object[]));
		
		var patchCall = Expression.Invoke(Expression.Constant(patch), Expression.Constant(originalMethodDelegate));
		var replacementVar = Expression.Variable(typeof(TDelegate));
		var assignReplacement = Expression.Assign(replacementVar, patchCall);
		
		var parameters = originalMethodDelegate.Method.GetParameters();
		var argExpressions = new Expression[parameters.Length];
		for (int i = 0; i < parameters.Length; i++)
		{
			var index = Expression.Constant(i);
			var arrayAccess = Expression.ArrayIndex(argsParam, index);
			var converted = Expression.Convert(arrayAccess, parameters[i].ParameterType);
			argExpressions[i] = converted;
		}
		var replacementCall = Expression.Invoke(replacementVar, argExpressions);
		var block = Expression.Block(
			new[] { replacementVar },
			assignReplacement,
			replacementCall
		);

		var lambda = Expression.Lambda<WrapperDelegate>(block, instanceParam, argsParam);
		return lambda.Compile();
	}
	private static bool Prefix(MethodBase __originalMethod, ref object? __result, object[] __args, object? __instance)
	{
		var originalMethod = (MethodInfo)__originalMethod;
		Debug.Assert(originalMethod.IsStatic ? __instance == null : __instance != null);
		if (!patches.TryGetValue(originalMethod, out var wrapperDel))
		{
			// throw new MidjiateWTFException("WHO PATCHED A METHOD WITH THIS METHOD MANUALLY" + new string('?', 50))
			Plugin.logger.LogError("[Patch.Prefix] WHO PATCHED A METHOD WITH THIS METHOD MANUALLY" + new string('?', 50));
			return true; // run original
		}
		
		__result = wrapperDel(__instance, __args);

		return false; // dont run original
	}
}

