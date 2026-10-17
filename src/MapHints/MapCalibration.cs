using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MapHints
{
    /// <summary>The map's own secret-zone items for the world on screen: position of each zone's lower-left corner.</summary>
    internal sealed class MapScan
    {
        public string World;
        public readonly Dictionary<string, Vector2> ZoneItems = new Dictionary<string, Vector2>();   // "scene#index"

        /// <summary>The map shows one world; its items are the active ones (items of other worlds can stay loaded).</summary>
        public static MapScan Read()
        {
            var perWorld = new Dictionary<string, List<(string scene, int idx, Vector2 pos)>>();
            foreach (var it in UnityEngine.Object.FindObjectsOfType<LevelMapItem>())
            {
                if (!it.gameObject.activeInHierarchy) continue;
                string roomId = it.itemWorldRoomId;   // World_Biome_10_HallOfMirrors_HallOfMirrors_MVP_Room_33_uid133
                if (string.IsNullOrEmpty(roomId)) continue;
                string world = HintData.WorldOf(roomId);
                if (world == null) continue;
                if (!perWorld.TryGetValue(world, out var list)) perWorld[world] = list = new List<(string, int, Vector2)>();
                // Items that merely sit inside a secret zone also carry its index; only the zone's own item marks its corner.
                bool isZoneItem = it.name.StartsWith("LevelMap_SecretZone_Item") && it.itemLinkedSecretZoneId >= 0;
                var p = it.transform.position;
                list.Add((isZoneItem ? roomId.Substring(world.Length + 1) : null, it.itemLinkedSecretZoneId, new Vector2(p.x, p.y)));
            }
            if (perWorld.Count == 0) return null;

            var top = perWorld.OrderByDescending(kv => kv.Value.Count).First();
            var scan = new MapScan { World = top.Key };
            foreach (var (scene, idx, pos) in top.Value)
                if (scene != null) scan.ZoneItems[scene + "#" + idx] = pos;
            return scan;
        }
    }

    /// <summary>
    /// Offset between the extracted data's coordinates and the map's world space. It is not the same in every
    /// world, so it is measured: each zone item on the map must sit at the zone's corner from the data.
    /// </summary>
    internal sealed class Calibration
    {
        public Vector2 World;
        public int Matched, Total, DistinctOffsets;
        readonly Dictionary<string, Vector2> _perRoom = new Dictionary<string, Vector2>();

        public Vector2 For(string room) => _perRoom.TryGetValue(room, out var o) ? o : World;

        static Vector2 Mode(IEnumerable<Vector2> values)
        {
            var groups = values.GroupBy(v => (Mathf.Round(v.x * 2f), Mathf.Round(v.y * 2f)));
            return groups.OrderByDescending(g => g.Count()).First().First();
        }

        public static Calibration Compute(WorldHints hints, MapScan scan)
        {
            var c = new Calibration { Total = hints.Zones.Count };
            var all = new List<Vector2>();
            var byRoom = new Dictionary<string, List<Vector2>>();
            foreach (var z in hints.Zones)
            {
                if (!scan.ZoneItems.TryGetValue(z.Room + "#" + z.Index, out var pos)) continue;
                var delta = new Vector2(pos.x - z.X, pos.y - z.Y);
                all.Add(delta);
                if (!byRoom.TryGetValue(z.Room, out var l)) byRoom[z.Room] = l = new List<Vector2>();
                l.Add(delta);
            }
            c.Matched = all.Count;
            if (all.Count == 0) return c;
            c.World = Mode(all);
            foreach (var kv in byRoom) c._perRoom[kv.Key] = Mode(kv.Value);
            c.DistinctOffsets = all.Select(v => (Mathf.Round(v.x * 2f), Mathf.Round(v.y * 2f))).Distinct().Count();
            return c;
        }
    }
}
