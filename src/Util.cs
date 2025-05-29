using System.Security.Cryptography;
using System.Text;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Injection;
using Newtonsoft.Json.Linq;
using Polytopia.Data;

namespace PolyMod;
internal static class Util
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

    internal static Version Cast(this Il2CppSystem.Version self)
    {
        return new(self.ToString());
    }

    internal static Version CutRevision(this Version self)
    {
        return new(self.Major, self.Minor, self.Build);
    }

    internal static string GetStyle(TribeData.Type tribe, SkinType skin)
    {
        return skin != SkinType.Default ? EnumCache<SkinType>.GetName(skin) : EnumCache<TribeData.Type>.GetName(tribe);
    }

    internal static string FormatSpriteName(string baseName) // I cant believe i had to do this shit #MIDJIWANFIXYOURSHITCODE
    {
        baseName = baseName.Replace(SpriteData.IMPROVEMENT_AQUA_FARM, EnumCache<ImprovementData.Type>.GetName(ImprovementData.Type.Aquafarm));
        //baseName = baseName.Replace(SpriteData.IMPROVEMENT_ATOLL, EnumCache<ImprovementData.Type>.GetName(ImprovementData.Type.Atoll));
        baseName = baseName.Replace(SpriteData.IMPROVEMENT_BURN_FOREST, EnumCache<ImprovementData.Type>.GetName(ImprovementData.Type.BurnForest));
        baseName = baseName.Replace(SpriteData.IMPROVEMENT_CLEAR_FOREST, EnumCache<ImprovementData.Type>.GetName(ImprovementData.Type.ClearForest));
        baseName = baseName.Replace(SpriteData.IMPROVEMENT_CUSTOMS_HOUSE, EnumCache<ImprovementData.Type>.GetName(ImprovementData.Type.CustomsHouse));
        baseName = baseName.Replace(SpriteData.IMPROVEMENT_FARM, EnumCache<ImprovementData.Type>.GetName(ImprovementData.Type.Farm));
        baseName = baseName.Replace(SpriteData.IMPROVEMENT_FOREST_TEMPLE, EnumCache<ImprovementData.Type>.GetName(ImprovementData.Type.ForestTemple));
        baseName = baseName.Replace(SpriteData.IMPROVEMENT_FORGE, EnumCache<ImprovementData.Type>.GetName(ImprovementData.Type.Forge));
        baseName = baseName.Replace(SpriteData.IMPROVEMENT_GROW_FOREST, EnumCache<ImprovementData.Type>.GetName(ImprovementData.Type.GrowForest));
        baseName = baseName.Replace(SpriteData.IMPROVEMENT_ICE_BANK, EnumCache<ImprovementData.Type>.GetName(ImprovementData.Type.IceBank));
        baseName = baseName.Replace(SpriteData.IMPROVEMENT_ICE_PORT, EnumCache<ImprovementData.Type>.GetName(ImprovementData.Type.Outpost));
        baseName = baseName.Replace(SpriteData.IMPROVEMENT_ICE_TEMPLE, EnumCache<ImprovementData.Type>.GetName(ImprovementData.Type.IceTemple));
        baseName = baseName.Replace(SpriteData.IMPROVEMENT_LUMBER_HUT, EnumCache<ImprovementData.Type>.GetName(ImprovementData.Type.LumberHut));
        //baseName = baseName.Replace(SpriteData.IMPROVEMENT_MARKET, EnumCache<ImprovementData.Type>.GetName(ImprovementData.Type.Market));
        baseName = baseName.Replace(SpriteData.IMPROVEMENT_MINE, EnumCache<ImprovementData.Type>.GetName(ImprovementData.Type.Mine));
        baseName = baseName.Replace(SpriteData.IMPROVEMENT_MOUNTAIN_TEMPLE, EnumCache<ImprovementData.Type>.GetName(ImprovementData.Type.MountainTemple));
        baseName = baseName.Replace(SpriteData.IMPROVEMENT_PORT, EnumCache<ImprovementData.Type>.GetName(ImprovementData.Type.Port));
        baseName = baseName.Replace(SpriteData.IMPROVEMENT_ROAD, EnumCache<ImprovementData.Type>.GetName(ImprovementData.Type.Road));
        baseName = baseName.Replace(SpriteData.IMPROVEMENT_RUIN, EnumCache<ImprovementData.Type>.GetName(ImprovementData.Type.Ruin));
        baseName = baseName.Replace(SpriteData.IMPROVEMENT_SANCTUARY, EnumCache<ImprovementData.Type>.GetName(ImprovementData.Type.Sanctuary));
        baseName = baseName.Replace(SpriteData.IMPROVEMENT_SAWMILL, EnumCache<ImprovementData.Type>.GetName(ImprovementData.Type.Sawmill));
        baseName = baseName.Replace(SpriteData.IMPROVEMENT_TEMPLE, EnumCache<ImprovementData.Type>.GetName(ImprovementData.Type.Temple));
        baseName = baseName.Replace(SpriteData.IMPROVEMENT_WATER_TEMPLE, EnumCache<ImprovementData.Type>.GetName(ImprovementData.Type.WaterTemple));
        baseName = baseName.Replace(SpriteData.IMPROVEMENT_WINDMILL, EnumCache<ImprovementData.Type>.GetName(ImprovementData.Type.Windmill));

        baseName = baseName.Replace(SpriteData.RESOURCE_AQUACROP, EnumCache<ResourceData.Type>.GetName(ResourceData.Type.AquaCrop));
        baseName = baseName.Replace(SpriteData.RESOURCE_CROP, EnumCache<ResourceData.Type>.GetName(ResourceData.Type.Crop));
        baseName = baseName.Replace(SpriteData.RESOURCE_FISH, EnumCache<ResourceData.Type>.GetName(ResourceData.Type.Fish));
        baseName = baseName.Replace(SpriteData.RESOURCE_FRUIT, EnumCache<ResourceData.Type>.GetName(ResourceData.Type.Fruit));
        baseName = baseName.Replace(SpriteData.RESOURCE_GAME, EnumCache<ResourceData.Type>.GetName(ResourceData.Type.Game));
        baseName = baseName.Replace(SpriteData.RESOURCE_METAL, EnumCache<ResourceData.Type>.GetName(ResourceData.Type.Metal));
        baseName = baseName.Replace(SpriteData.RESOURCE_SPORES, EnumCache<ResourceData.Type>.GetName(ResourceData.Type.Spores));
        baseName = baseName.Replace(SpriteData.RESOURCE_STARFISH, EnumCache<ResourceData.Type>.GetName(ResourceData.Type.Starfish));
        baseName = baseName.Replace(SpriteData.RESOURCE_WHALE, EnumCache<ResourceData.Type>.GetName(ResourceData.Type.Whale));

        baseName = baseName.Replace(SpriteData.TILE_FIELD, EnumCache<TerrainData.Type>.GetName(TerrainData.Type.Field));
        //baseName = baseName.Replace(SpriteData.TILE_FOREST, EnumCache<TerrainData.Type>.GetName(TerrainData.Type.Forest));
        baseName = baseName.Replace(SpriteData.TILE_ICE, EnumCache<TerrainData.Type>.GetName(TerrainData.Type.Ice));
        baseName = baseName.Replace(SpriteData.TILE_MOUNTAIN, EnumCache<TerrainData.Type>.GetName(TerrainData.Type.Mountain));
        baseName = baseName.Replace(SpriteData.TILE_OCEAN, EnumCache<TerrainData.Type>.GetName(TerrainData.Type.Ocean));
        baseName = baseName.Replace(SpriteData.TILE_UNKNOWN, EnumCache<TerrainData.Type>.GetName(TerrainData.Type.Field));
        baseName = baseName.Replace(SpriteData.TILE_WATER, EnumCache<TerrainData.Type>.GetName(TerrainData.Type.Water));
        //baseName = baseName.Replace(SpriteData.TILE_WETLAND, EnumCache<TerrainData.Type>.GetName(TerrainData.Type.Field) + "_flooded");

        baseName = baseName.Replace("UI_", "");
        return baseName;
    }
}
