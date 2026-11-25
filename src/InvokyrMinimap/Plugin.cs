using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;

namespace InvokyrMinimap
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BasePlugin
    {
        public const string PluginGuid = "com.darkharasho.invokyr.minimap";
        public const string PluginName = "InvokyrMinimap";
        public const string PluginVersion = "1.0.1";

        internal static new ManualLogSource Log;
        internal static ConfigEntry<bool> Enabled, ShowMinimap, RevealAll, ShowIdols, ShowMonsters, HideWhenCursorFree;
        internal static ConfigEntry<string> MapKey;
        internal static ConfigEntry<float> MinimapSize, MinimapRange;

        public override void Load()
        {
            Log = base.Log;
            Enabled = Bind("General", "Enabled", true, "Show the maps.", 9);
            MapKey = Config.Bind("General", "MapKey", "Tab", new ConfigDescription(
                "Hold to show the large map (an Input System key name: Tab, M, CapsLock...).",
                null, new ConfigurationManagerAttributes { DispName = "Large map key", Order = 8 }));
            ShowMinimap = Bind("Minimap", "ShowMinimap", true, "Show the small map in the top-right corner.", 7);
            MinimapSize = Config.Bind("Minimap", "Size", 260f, new ConfigDescription(
                "Width and height of the corner map, in pixels at 1080p.",
                new AcceptableValueRange<float>(120f, 500f), new ConfigurationManagerAttributes { DispName = "Minimap size", Order = 6 }));
            MinimapRange = Config.Bind("Minimap", "Range", 30f, new ConfigDescription(
                "Metres from you to the edge of the corner map.",
                new AcceptableValueRange<float>(5f, 150f), new ConfigurationManagerAttributes { DispName = "Minimap range", Order = 5 }));
            RevealAll = Bind("Map", "RevealAll", false, "Show every room from the start instead of only rooms someone on the team has been in.", 4);
            ShowIdols = Bind("Map", "ShowIdols", true, "Mark idols.", 3);
            ShowMonsters = Bind("Map", "ShowMonsters", false, "Mark monsters.", 2);
            HideWhenCursorFree = Bind("General", "HideInMenus", true, "Hide the maps while the mouse cursor is free (menus, pause, shop screens).", 1);

            AddComponent<MinimapController>();
            Log.LogInfo($"{PluginName} {PluginVersion} loaded; hold {MapKey.Value} for the large map");
        }

        private ConfigEntry<bool> Bind(string section, string key, bool value, string description, int order) =>
            Config.Bind(section, key, value, new ConfigDescription(description, null, new ConfigurationManagerAttributes { DispName = key, Order = order }));
    }
}
