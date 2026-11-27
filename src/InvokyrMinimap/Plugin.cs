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
        public const string PluginVersion = "1.1.2";

        internal static new ManualLogSource Log;
        internal static ConfigEntry<bool> Enabled, ShowMinimap, RevealAll, ShowIdols, ShowMonsters, HideWhenCursorFree, RotateWithView;
        internal static ConfigEntry<string> MapKey;
        internal static ConfigEntry<float> MinimapSize, MinimapRange, OffsetX, OffsetY;
        internal static ConfigEntry<MapCorner> MinimapCorner;
        internal static ConfigEntry<MapBackground> Background;

        public override void Load()
        {
            Log = base.Log;
            Enabled = Bind("General", "Enabled", true, "Show the maps.", 9);
            MapKey = Config.Bind("General", "MapKey", "Tab", new ConfigDescription(
                "Hold to show the large map (an Input System key name: Tab, M, CapsLock...).",
                null, new ConfigurationManagerAttributes { DispName = "Large map key", Order = 8 }));
            ShowMinimap = Bind("Minimap", "ShowMinimap", true, "Show the small map.", 20);
            MinimapCorner = Config.Bind("Minimap", "Corner", MapCorner.TopRight, new ConfigDescription(
                "Which corner of the screen the small map sits in.",
                null, new ConfigurationManagerAttributes { DispName = "Corner", Order = 19 }));
            OffsetX = Config.Bind("Minimap", "OffsetX", 24f, new ConfigDescription(
                "Distance from the left or right edge of the screen, in pixels at 1080p.",
                new AcceptableValueRange<float>(0f, 1000f), new ConfigurationManagerAttributes { DispName = "Distance from side", Order = 18 }));
            OffsetY = Config.Bind("Minimap", "OffsetY", 110f, new ConfigDescription(
                "Distance from the top or bottom edge of the screen, in pixels at 1080p. The default clears the money and health at the top.",
                new AcceptableValueRange<float>(0f, 1000f), new ConfigurationManagerAttributes { DispName = "Distance from top/bottom", Order = 17 }));
            MinimapSize = Config.Bind("Minimap", "Size", 260f, new ConfigDescription(
                "Width and height of the small map, in pixels at 1080p.",
                new AcceptableValueRange<float>(120f, 500f), new ConfigurationManagerAttributes { DispName = "Minimap size", Order = 16 }));
            MinimapRange = Config.Bind("Minimap", "Range", 30f, new ConfigDescription(
                "Metres from you to the edge of the small map.",
                new AcceptableValueRange<float>(5f, 150f), new ConfigurationManagerAttributes { DispName = "Minimap range", Order = 15 }));
            Background = Config.Bind("Map", "Background", MapBackground.Dark, new ConfigDescription(
                "What's behind the map: Dark (see-through), Black or Tan.",
                null, new ConfigurationManagerAttributes { DispName = "Background", Order = 14 }));
            RotateWithView = Bind("Map", "RotateWithView", true, "Turn the maps with your view, so the way you look is always up. Off: north is up and your arrow turns.", 14);
            RevealAll = Bind("Map", "RevealAll", false, "Show every room from the start instead of only rooms someone on the team has been in.", 13);
            ShowIdols = Bind("Map", "ShowIdols", false, "Mark idols.", 12);
            ShowMonsters = Bind("Map", "ShowMonsters", false, "Mark monsters.", 11);
            HideWhenCursorFree = Bind("General", "HideInMenus", true, "Hide the maps while the mouse cursor is free (menus, pause, shop screens).", 1);

            AddComponent<MinimapController>();
            Log.LogInfo($"{PluginName} {PluginVersion} loaded; hold {MapKey.Value} for the large map");
        }

        private ConfigEntry<bool> Bind(string section, string key, bool value, string description, int order) =>
            Config.Bind(section, key, value, new ConfigDescription(description, null, new ConfigurationManagerAttributes { DispName = key, Order = order }));
    }
}
