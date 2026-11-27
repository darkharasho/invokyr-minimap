# Changelog

## 1.1.1
- Room outlines are back, now around the real room shapes: a light line where floor meets empty space or another room.
- Revived teammates no longer leave a stale copy on the map: switched-off player objects are skipped, and when two share a name the living one is drawn.

## 1.1.0
- Rooms now show their real shape: each room you've been in is scanned with raycasts, so the map shows floor, walls and pits instead of the overlapping room boxes.
- The map turns with your view, so the way you look is always up (`RotateWithView`; turn it off for north-up).
- You're an arrow now, not a dot with a trail. Teammates are circles, idols diamonds, monsters squares.
- Place the small map in any corner (`Corner`) and nudge it with `OffsetX` / `OffsetY`. By default it sits lower, below the money.
- Idols are off by default (`ShowIdols`). An existing config keeps its old value.

## 1.0.1
- Declare the BepInEx IL2CPP pack as a dependency, so mod managers install BepInEx with this mod.

## 1.0.0
- First release: corner minimap and large map (hold Tab) built from the game's room boxes, with explored rooms, teammates, idols, exit and shop.
