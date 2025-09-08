using Polytopia.Data;

namespace PolyMod.modApi;

public static class LuaEnumCache
{
	// Store delegates for each enum type
	private static readonly Dictionary<string, Func<string, object>> _getTypeFuncs = new();
	private static readonly Dictionary<string, Action<string, object>> _addMappingFuncs = new();
	private static readonly Dictionary<string, Type> _enumTypes = new();

	// Register a type for Lua use
	public static void RegisterEnum<T>(string typeName) where T : struct, Enum
	{
		_enumTypes[typeName] = typeof(T);
		// Add GetType function
		_getTypeFuncs[typeName] = (string name) =>
		{
			if (EnumCache<T>.TryGetType(name, out var value))
				return value;
			return null;
		};

		// Add AddMapping function
		_addMappingFuncs[typeName] = (string name, object val) =>
		{
			if (val is T enumVal)
				EnumCache<T>.AddMapping(name, enumVal);
			else
				throw new ArgumentException($"Value must be of type {typeof(T)} but is of type {val.GetType()}");
		};
	}

	// Lua-friendly GetType
	public static object GetType(string typeName, string name)
	{
		if (_getTypeFuncs.TryGetValue(typeName, out var func))
			return func(name);
		throw new Exception($"Enum type '{typeName}' not registered.");
	}
	
	// Lua-friendly TryGetType
	public static bool TryGetType(string typeName, string name, out object value)
	{
		if (_getTypeFuncs.TryGetValue(typeName, out var func))
		{
			value = func(name);
			return value != null;
		}
		value = null;
		return false;
	}

	// Lua-friendly AddMapping
	public static void AddMapping(string typeName, string name, object value)
	{
		
		if (!_addMappingFuncs.TryGetValue(typeName, out var func))
		{
			throw new Exception($"Enum type '{typeName}' not registered.");
		}

		// If the value is a double, attempt to convert it to the underlying type of the enum
		if (value is double doubleValue)
		{
			if (!_enumTypes.TryGetValue(typeName, out var enumType))
			{
				// This should not happen if func was found, but for safety.
				throw new Exception($"Enum type '{typeName}' not registered correctly (type info missing).");
			}
			var underlyingType = Enum.GetUnderlyingType(enumType);
			var valueInUnderlyingType = Convert.ChangeType(doubleValue, underlyingType);
			value = Enum.ToObject(enumType, valueInUnderlyingType);
		}

		func(name, value);
	}
}