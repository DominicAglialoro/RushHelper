namespace Celeste.Mod.RushHelper; 

public static class MapMetaExtensions {
    public static void Load() => On.Celeste.Mod.Meta.MapMeta.GetInventory += MapMeta_GetInventory;

    public static void Unload() => On.Celeste.Mod.Meta.MapMeta.GetInventory += MapMeta_GetInventory;

    private static PlayerInventory? MapMeta_GetInventory(On.Celeste.Mod.Meta.MapMeta.orig_GetInventory getInventory, string meta) {
        if (meta == "HeavenRush")
            return new PlayerInventory(1, true, false, true);

        return getInventory(meta);
    }
}