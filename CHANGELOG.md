# Changelog

## 0.2.0
- Checks the hint data's format version (`data/meta.json`, needs hint tool 1.2.0+) and logs a clear error instead of
  misreading data from a different format.
- Release package (`MapHints-<version>.zip`, built by `build.ps1`) and a GitHub issue template.
- The plugin version now lives in one place in the code (`PluginInfo`).
- The hint tool copies the data into the plugin folder by itself; no manual copying.

## 0.1.0
- First version: highlights undiscovered secret zones (red frames) and collectibles you have not taken yet
  (coloured squares: chest, HP, mana, attack book, defense book, XP source) on the in-game map.
- Works for every world on the map, including worlds you are not currently in.
- Map offset is measured per world from the game's own secret-zone map items, so markers line up.
- Reads what you already found live from the game (no save file access).
- Toggle with F10 (configurable) at any time; the choice is remembered.
- Optional completion threshold (`ThresholdPercent`, default 0 = always on).
