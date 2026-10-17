# Changelog

## 0.1.0
- First version: highlights undiscovered secret zones (red frames) and collectibles you have not taken yet
  (coloured squares: chest, HP, mana, attack book, defense book, XP source) on the in-game map.
- Works for every world on the map, including worlds you are not currently in.
- Map offset is measured per world from the game's own secret-zone map items, so markers line up.
- Reads what you already found live from the game (no save file access).
- Toggle with F10 (configurable) at any time; the choice is remembered.
- Optional completion threshold (`ThresholdPercent`, default 0 = always on).
