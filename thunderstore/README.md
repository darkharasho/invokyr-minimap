# Invokyr Minimap

A small map in a corner of the screen, and a large map of the whole level while you hold **Tab**. Both turn with your view, so the way you look is always up.

## What it shows
- Rooms appear once anyone on your team has been in them, in their real shape: floor, walls (light) and pits (dark). Other floors are faint.
- The exit room is green and the shop is blue.
- You (a gold arrow), your teammates (blue circles, named on the large map).
- Idols (orange diamonds) and monsters (red squares) if you turn on `ShowIdols` / `ShowMonsters`.

## Settings
In game with [InvokyrConfig](https://github.com/darkharasho/invokyr-config) (F2), or in `BepInEx/config/com.darkharasho.invokyr.minimap.cfg`: the large map key, the minimap's corner, offsets, size and range, turning with your view, `RevealAll` (show every room from the start), idol and monster markers, and hiding the maps while a menu is open.

## Notes
- Client-side: only you see your maps, and nobody else needs the mod.
- Room shapes come from raycasting each room once you've been in it, spread over a few frames; the map fills in within a second or so.
- Needs BepInEx 6 for IL2CPP (be.755 or newer).
