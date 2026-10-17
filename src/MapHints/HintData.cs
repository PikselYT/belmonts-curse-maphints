using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace MapHints
{
    internal sealed class Zone
    {
        public string Room;      // scene name, e.g. MainHall_MVP_Room_560_uid196
        public int Index;
        public float X, Y, W, H; // world tile coordinates (lower-left corner + size)
    }

    internal sealed class Item
    {
        public string Room;
        public string Guid;
        public string Kind;
        public float X, Y;       // world tile coordinates
    }

    /// <summary>Everything the mod knows about one world, loaded from the hint tool's data/*.json.</summary>
    internal sealed class WorldHints
    {
        public string World;
        public List<Zone> Zones = new List<Zone>();   // only zones that count for completion (not resetable)
        public List<Item> Items = new List<Item>();
    }

    internal static class HintData
    {
        static readonly Dictionary<string, WorldHints> _cache = new Dictionary<string, WorldHints>();
        static Dictionary<string, int> _totals;

        static string DataDir()
        {
            string cfg = Plugin.DataDirectory.Value;
            if (!string.IsNullOrWhiteSpace(cfg)) return cfg;
            return Path.Combine(Path.GetDirectoryName(typeof(Plugin).Assembly.Location) ?? ".", "data");
        }

        /// <summary>Total completion item count of a world (CompletionAggregator), or 0 when unknown.</summary>
        public static int TotalCompletion(string world)
        {
            if (_totals == null)
            {
                _totals = new Dictionary<string, int>();
                try
                {
                    string f = Path.Combine(DataDir(), "completion.json");
                    if (File.Exists(f))
                        using (var doc = JsonDocument.Parse(File.ReadAllText(f)))
                            foreach (var p in doc.RootElement.EnumerateObject()) _totals[p.Name] = p.Value.GetInt32();
                    else Plugin.Logger.LogWarning($"completion.json not found in {DataDir()}");
                }
                catch (Exception e) { Plugin.Logger.LogError("Reading completion.json failed: " + e); }
            }
            return _totals.TryGetValue(world, out int n) ? n : 0;
        }

        /// <summary>World a room id belongs to, e.g. World_Biome_01_Streets_Poor_Streets_Room_01 -> World_Biome_01_Streets_Poor.</summary>
        public static string WorldOf(string worldRoomId)
        {
            TotalCompletion("");   // makes sure the world list is loaded
            string best = null;
            foreach (var w in _totals.Keys)
                if (worldRoomId.StartsWith(w + "_", StringComparison.Ordinal) && (best == null || w.Length > best.Length)) best = w;
            return best;
        }

        public static WorldHints Get(string world)
        {
            if (_cache.TryGetValue(world, out var cached)) return cached;
            WorldHints result = null;
            try
            {
                string f = Path.Combine(DataDir(), world + ".json");
                if (File.Exists(f)) result = Parse(world, File.ReadAllText(f));
                else Plugin.Logger.LogWarning($"No hint data for {world} ({f}). Run the hint tool's extract step and copy data/*.json there.");
            }
            catch (Exception e) { Plugin.Logger.LogError($"Reading hint data for {world} failed: {e}"); }
            _cache[world] = result;
            return result;
        }

        static WorldHints Parse(string world, string json)
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var rooms = root.GetProperty("rooms");
            (float, float) Origin(string room)
            {
                var r = rooms.GetProperty(room);   // chunk is 24x13 tiles; some rooms carry an edition offset
                return ((r.GetProperty("cx").GetInt32() + r.GetProperty("off")[0].GetInt32()) * 24f,
                        (r.GetProperty("cy").GetInt32() + r.GetProperty("off")[1].GetInt32()) * 13f);
            }

            var hints = new WorldHints { World = world };
            foreach (var z in root.GetProperty("zones").EnumerateArray())
            {
                if (z.GetProperty("resetable").GetInt32() != 0) continue;   // never recorded as discovered, so not counted
                string room = z.GetProperty("room").GetString();
                var (ox, oy) = Origin(room);
                hints.Zones.Add(new Zone
                {
                    Room = room, Index = z.GetProperty("idx").GetInt32(),
                    X = ox + z.GetProperty("x").GetSingle(), Y = oy + z.GetProperty("y").GetSingle(),
                    W = z.GetProperty("w").GetSingle(), H = z.GetProperty("h").GetSingle(),
                });
            }
            foreach (var it in root.GetProperty("items").EnumerateArray())
            {
                string room = it.GetProperty("room").GetString();
                var (ox, oy) = Origin(room);
                hints.Items.Add(new Item
                {
                    Room = room, Guid = it.GetProperty("guid").GetString(), Kind = it.GetProperty("kind").GetString(),
                    X = ox + it.GetProperty("x").GetSingle(), Y = oy + it.GetProperty("y").GetSingle(),
                });
            }
            Plugin.Logger.LogInfo($"Loaded hint data for {world}: {hints.Zones.Count} secret zones, {hints.Items.Count} collectibles");
            return hints;
        }
    }
}
