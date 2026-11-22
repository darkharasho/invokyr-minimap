## Project Context

A BepInEx 6 (BepInEx.Unity.IL2CPP, be.755) mod for Invokyr (Unity 6000.3, IL2CPP): a corner minimap and a large map while Tab is held. Client-side only. Game code is in `vhs.dll`. Packaged Thunderstore-style under `darkharasho`, released on GitHub. Build setup is shared with `invokyr-admin-menu` (see its CLAUDE.md for refs, packaging and the flat zip layout HML needs).

## How it works (`MinimapController.cs`)
- Layout: `EnvironmentElements.Room.GetAll()` and each room's `roomBounds` (List<Bounds>, the room triggers), drawn top-down as rectangles; refreshed every 2 s since rooms stream in. `Room.roomType` flags mark EndRoom (exit) and ShopRoom. Rooms count as seen once any player's `currentRoom` was them (cleared on scene change); `RevealAll` shows all.
- Floors: a box is "this floor" if the local player's Y is within its Y range ±1.5 m; others are drawn faint.
- Players: `EntityManager.Me`, `EntityManager.GetAllPlayersNoAlloc(Il2Cpp List)`. Idols: `EntityManager.Instance.Idols` (Il2Cpp HashSet<ScoreTool>, needs the Il2CppSystem.Core reference). Monsters: `EntityManager.Instance.MonsterEntities`.
- Drawing: OnGUI Repaint only, `GUI.DrawTexture`/`GUI.Label` with manual clipping (`MapView.cs`, tested), no IMGUI controls or groups. Facing is shown with trailing dots, so no rotation is needed.
- Hidden while the cursor is free (menus) when HideInMenus is on. Map key read from the Input System (`Keyboard.current`).

## Building
`scripts/package.sh` → `dist/darkharasho-InvokyrMinimap-<version>.zip`; `hml install nexus-invokyr <zip> --profile Dev`. Tests: `dotnet test tests/InvokyrMinimap.Tests`. Run builds with `nice -n 19 taskset -c 0-3`. Release: bump `Plugin.cs`, csproj, manifest, CHANGELOG; push tag `v<version>`.
