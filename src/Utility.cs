using System.Security.Cryptography;
using System.Text;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Injection;
using Newtonsoft.Json.Linq;
using Polytopia.Data;

namespace PolyMod
{
    internal static class Utility
    {
        internal static Il2CppSystem.Type WrapType<T>() where T : class
        {
            if (!ClassInjector.IsTypeRegisteredInIl2Cpp<T>())
                ClassInjector.RegisterTypeInIl2Cpp<T>();
            return Il2CppType.From(typeof(T));
        }

        internal static string Hash(object data)
        {
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(data.ToString()!)));
        }

        internal static string GetJTokenName(JToken token, int n = 1)
		{
			return token.Path.Split('.')[^n];
		}

        internal static bool EqualNoRevision(this Il2CppSystem.Version self, Il2CppSystem.Version version)
        {
            return self.Major == version.Major && self.Minor == version.Minor && self.Build == version.Build;
        }

        internal static string GetStyle(TribeData.Type tribe, SkinType skin)
		{
			return skin != SkinType.Default ? EnumCache<SkinType>.GetName(skin) : EnumCache<TribeData.Type>.GetName(tribe);
		}
    }
}