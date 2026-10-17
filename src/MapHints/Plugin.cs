using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MapHints
{
    internal static class PluginInfo
    {
        public const string Guid = "pikselyt.belmontscurse.maphints";
        public const string Name = "Map Hints";
        public const string Version = "0.2.0";   // keep in step with <Version> in MapHints.csproj
    }

    /// <summary>
    /// Highlights the secret zones and collectibles you still
    /// miss on the in-game map. Positions come from the hint tool's extracted data; what you already have
    /// is read live from the game.
    /// </summary>
    [BepInPlugin(PluginInfo.Guid, PluginInfo.Name, PluginInfo.Version)]
    public class Plugin : BasePlugin
    {
        internal static ManualLogSource Logger;
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<string> ToggleKeyName;
        internal static Key ToggleKey;
        internal static ConfigEntry<float> ThresholdPercent;
        internal static ConfigEntry<bool> ShowSecretZones;
        internal static ConfigEntry<bool> ShowCollectibles;
        internal static ConfigEntry<string> DataDirectory;
        internal static ConfigEntry<bool> Verbose;

        public override void Load()
        {
            Logger = Log;
            Enabled = Config.Bind("General", "Enabled", true,
                "Highlights on/off. The toggle key flips this at any time, and the choice is remembered between sessions.");
            ToggleKeyName = Config.Bind("General", "ToggleKey", "F10",
                "Keyboard key that turns the highlights on/off (Unity Input System key name, e.g. F10, H, Tab).");
            if (!Enum.TryParse(ToggleKeyName.Value, true, out ToggleKey) || ToggleKey == Key.None)
            {
                ToggleKey = Key.F10;
                Logger.LogWarning($"Unknown ToggleKey '{ToggleKeyName.Value}', using F10.");
            }
            ThresholdPercent = Config.Bind("General", "ThresholdPercent", 0f,
                "Highlights appear on a world's map once that world's completion reaches this percentage. 0 = always.");
            ShowSecretZones = Config.Bind("General", "ShowSecretZones", true, "Highlight undiscovered secret zones (red frames).");
            ShowCollectibles = Config.Bind("General", "ShowCollectibles", true, "Highlight collectibles you have not taken yet (coloured squares).");
            DataDirectory = Config.Bind("Data", "DataDirectory", "",
                "Folder with the hint tool's data/*.json. Empty = a 'data' folder next to this plugin.");
            Verbose = Config.Bind("Debug", "Verbose", true, "Log what the plugin decides (useful while testing).");

            ClassInjector.RegisterTypeInIl2Cpp<Runner>();
            AddComponent<Runner>();
            Logger.LogInfo($"{PluginInfo.Name} {PluginInfo.Version} loaded");
        }
    }

    public class Runner : MonoBehaviour
    {
        public Runner(IntPtr ptr) : base(ptr) { }

        static readonly Dictionary<string, Color> KindColors = new Dictionary<string, Color>
        {
            ["chest"] = new Color(1f, 0.82f, 0.1f),
            ["health"] = new Color(1f, 0.25f, 0.45f),
            ["mana"] = new Color(0.3f, 0.6f, 1f),
            ["atk_book"] = new Color(1f, 0.5f, 0f),
            ["def_book"] = new Color(0f, 0.8f, 0.8f),
            ["xp"] = new Color(0.55f, 1f, 0.3f),
        };
        static readonly Color ZoneColor = new Color(1f, 0.15f, 0.15f);

        readonly Overlay _overlay = new Overlay();
        float _next;
        string _signature = "";
        string _warnedMismatch;

        public void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb[Plugin.ToggleKey].wasPressedThisFrame)
            {
                Plugin.Enabled.Value = !Plugin.Enabled.Value;   // saved to the config file, so it persists
                Plugin.Logger.LogInfo("Highlights " + (Plugin.Enabled.Value ? "on" : "off"));
                _signature = "";   // force a redraw
                _next = 0f;        // apply right away, not on the next tick
            }

            float now = Time.unscaledTime;
            if (now < _next) return;
            _next = now + 0.5f;
            try { Refresh(); }
            catch (Exception e) { Plugin.Logger.LogError("Refresh failed: " + e); _next = now + 5f; }
        }

        void Reset()
        {
            _overlay.Clear();   // cheap when nothing is drawn
            _signature = "";
        }

        void Refresh()
        {
            if (!Plugin.Enabled.Value) { Reset(); return; }

            var scan = MapScan.Read();
            if (scan == null) { Reset(); return; }
            string world = scan.World;

            int total = HintData.TotalCompletion(world);
            int have = GameState.CompletionCount(world, out string detail);
            float pct = total > 0 ? 100f * have / total : 0f;
            if (Plugin.ThresholdPercent.Value > 0f && pct < Plugin.ThresholdPercent.Value)
            {
                string key = "below|" + world;
                if (_signature != key)
                {
                    _overlay.Clear();
                    _signature = key;
                    Plugin.Logger.LogInfo($"{world}: {pct:0.0}% ({have}/{total}; {detail}) is below {Plugin.ThresholdPercent.Value}%; no highlights.");
                }
                return;
            }

            var saves = GameState.WorldSavesFor(world);
            if (saves.Count == 0)
            {
                if (_warnedMismatch != world) { _warnedMismatch = world; Plugin.Logger.LogWarning($"No save data for {world}; no highlights."); }
                Reset();
                return;
            }

            var hints = HintData.Get(world);
            if (hints == null) { Reset(); return; }

            var zones = Plugin.ShowSecretZones.Value
                ? hints.Zones.Where(z => !GameState.SecretZoneFound(world, z.Room, z.Index)).ToList()
                : new List<Zone>();
            var items = Plugin.ShowCollectibles.Value
                ? hints.Items.Where(i => !GameState.ItemCollected(saves, i.Guid)).ToList()
                : new List<Item>();

            var cal = Calibration.Compute(hints, scan);
            var sig = new StringBuilder(world).Append('@').Append(cal.World.x).Append(',').Append(cal.World.y).Append('/').Append(cal.Matched);
            foreach (var z in zones) sig.Append('|').Append(z.Room).Append(':').Append(z.Index);
            foreach (var i in items) sig.Append('|').Append(i.Guid);
            string signature = sig.ToString();
            if (signature == _signature) return;   // nothing changed since the last redraw
            _signature = signature;

            _overlay.Begin();
            foreach (var z in zones) { var o = cal.For(z.Room); _overlay.Zone(z.X + o.x, z.Y + o.y, z.W, z.H, ZoneColor); }
            foreach (var i in items)
            {
                var o = cal.For(i.Room);
                _overlay.Marker(i.X + o.x, i.Y + o.y, KindColors.TryGetValue(i.Kind, out var col) ? col : Color.white);
            }

            if (Plugin.Verbose.Value)
                Plugin.Logger.LogInfo($"{world}: {pct:0.0}% ({have}/{total}; {detail}); [{GameState.Describe(saves)}]; highlighting {zones.Count} secret zones and {items.Count} collectibles. " +
                    $"Calibration: offset ({cal.World.x}, {cal.World.y}), matched {cal.Matched}/{cal.Total} zones, {cal.DistinctOffsets} distinct offset(s).");
        }
    }
}
