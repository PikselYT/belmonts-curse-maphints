using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using UnityEngine;

namespace MapHints
{
    /// <summary>
    /// Reads the save state the game keeps in memory (no save file access).
    /// The player's own world is "live" (RuntimeWorldSave); other worlds are whatever the loaded save holds.
    /// </summary>
    internal static class GameState
    {
        static ExplorerComponent _explorer;

        public static ExplorerComponent Explorer
        {
            get
            {
                if (_explorer == null) _explorer = UnityEngine.Object.FindObjectOfType<ExplorerComponent>();
                return _explorer;
            }
        }

        /// <summary>Key of the world the player is currently in, e.g. World_Biome_06_MainHall.</summary>
        public static string CurrentWorldKey()
        {
            var logic = SaveLogic.Get();
            return logic == null ? null : logic.GetCurrentWorldSaveKey();
        }

        static WorldSave SavedWorld(string world)
        {
            var logic = SaveLogic.Get();
            var worlds = logic?.CurrentSave?.worldSaves;
            return worlds != null && worlds.TryGetValue(world, out var ws) ? ws : null;
        }

        /// <summary>
        /// Saves that may hold entity states of <paramref name="world"/>: the loaded save, plus the live runtime
        /// save for the current world (it can be empty or ahead of the loaded save, so both are consulted).
        /// </summary>
        public static List<WorldSave> WorldSavesFor(string world)
        {
            var result = new List<WorldSave>();
            var saved = SavedWorld(world);
            if (saved != null) result.Add(saved);
            var logic = SaveLogic.Get();
            if (logic != null && world == CurrentWorldKey())
            {
                var live = logic.RuntimeWorldSave;
                if (live != null) result.Add(live);
            }
            return result;
        }

        public static string Describe(List<WorldSave> saves) =>
            string.Join(", ", saves.Select(s => (s.savableEntityStates?.Count ?? -1) + " entity states"));

        /// <summary>Completion item count of a world, as the game's own counter reports it.</summary>
        public static int CompletionCount(string world, out string detail)
        {
            var logic = SaveLogic.Get();
            int saved = SavedWorld(world)?.currentCompletionItemCount ?? 0;
            if (logic == null || world != CurrentWorldKey()) { detail = $"saved={saved}"; return saved; }

            int runtime = logic.RuntimeWorldSave?.currentCompletionItemCount ?? 0;
            int computed = -1;
            try
            {
                var ex = Explorer;
                if (ex != null) computed = logic.GetCurrentWorldCompletionCount(ex.selfEntity);
            }
            catch (Exception e) { Plugin.Logger.LogWarning("GetCurrentWorldCompletionCount failed: " + e.Message); }
            detail = $"computed={computed} runtime={runtime} saved={saved}";
            return Math.Max(Math.Max(computed, runtime), saved);
        }

        public static bool SecretZoneFound(string world, string room, int index)
        {
            var ex = Explorer;
            return ex != null && ex.DiscoveredSecretZone(world + "_" + room, index);
        }

        /// <summary>The game stores {"activated": true} for every chest opened / item taken / source destroyed.</summary>
        public static bool ItemCollected(List<WorldSave> saves, string guid)
        {
            foreach (var ws in saves)
            {
                var states = ws.savableEntityStates;
                if (states == null || !states.TryGetValue(guid, out string raw) || string.IsNullOrEmpty(raw)) continue;
                try
                {
                    using var doc = JsonDocument.Parse(raw);
                    if (doc.RootElement.TryGetProperty("activated", out var a) && a.ValueKind == JsonValueKind.True) return true;
                }
                catch (Exception) { }
            }
            return false;
        }
    }
}
