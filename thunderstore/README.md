# Invokyr Minimap

A small map in the top-right corner, and a large map of the whole level while you hold **Tab**.

## What it shows
- Rooms appear once anyone on your team has been in them. Rooms on your floor are solid with outlines; other floors are faint.
- The exit room is green and the shop is blue.
- You (gold, with a short trail pointing the way you face), your teammates (blue, named on the large map) and idols (orange).
- Monsters (red) if you turn on `ShowMonsters`.

## Settings
In `BepInEx/config/com.darkharasho.invokyr.minimap.cfg`: the large map key, the minimap's size and range, `RevealAll` (show every room from the start), idol and monster markers, and hiding the maps while a menu is open.

## Notes
- Client-side: only you see your maps, and nobody else needs the mod.
- The layout comes from the game's own room boxes, so it's blocky rather than wall-accurate.
- Needs BepInEx 6 for IL2CPP (be.755 or newer).
