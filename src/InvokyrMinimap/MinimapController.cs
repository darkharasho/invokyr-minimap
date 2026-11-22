using System;
using System.Collections.Generic;
using _200_Dev.Entities;
using Entities.Player;
using EnvironmentElements;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace InvokyrMinimap
{
    /// <summary>
    /// A corner minimap and a large map while the map key is held. The layout is the game's own room boxes
    /// (<see cref="Room.roomBounds"/>, the triggers that tell which room an entity is in) seen from above, so nothing is
    /// scanned or raycast. Rooms show up once anyone on the team has been in them, unless RevealAll is on.
    /// Data is collected in Update and drawn in OnGUI with GUI.DrawTexture/GUI.Label only (IMGUI controls that need a
    /// control id may fail to unstrip in IL2CPP builds).
    /// </summary>
    public sealed class MinimapController : MonoBehaviour
    {
        public MinimapController(IntPtr ptr) : base(ptr) { }

        private const float ReferenceHeight = 1080f;

        private struct Shape
        {
            public float MinX, MinZ, MaxX, MaxZ, MinY, MaxY;
        }

        private sealed class RoomInfo
        {
            public IntPtr Ptr;
            public ERoomType Type;
            public readonly List<Shape> Boxes = new List<Shape>();
        }

        private struct Dot
        {
            public float X, Y, Z, Fx, Fz;
            public bool Me;
            public string Name;
        }

        private readonly HashSet<IntPtr> _seen = new HashSet<IntPtr>();
        private List<RoomInfo> _rooms = new List<RoomInfo>();
        private readonly List<Dot> _players = new List<Dot>();
        private readonly List<Vector3> _idols = new List<Vector3>();
        private readonly List<Vector3> _monsters = new List<Vector3>();
        private Il2CppSystem.Collections.Generic.List<PlayerEntity> _playerBuffer;
        private Dot _me;
        private bool _active;
        private bool _big;
        private int _sceneHandle;
        private float _nextRoomRefresh;
        private bool _loggedError;

        private Texture2D _panel, _room, _roomOther, _edge, _exit, _shop, _meTex, _mateTex, _idolTex, _monsterTex, _frame;
        private GUIStyle _nameStyle;

        private void Update()
        {
            try
            {
                Collect();
            }
            catch (Exception e)
            {
                _active = false;
                LogOnce("update", e);
            }
        }

        private void Collect()
        {
            var me = EntityManager.Me;
            if (me == null || !Plugin.Enabled.Value)
            {
                _active = false;
                return;
            }

            var scene = SceneManager.GetActiveScene().handle;
            if (scene != _sceneHandle)
            {
                _sceneHandle = scene;
                _seen.Clear();
                _rooms.Clear();
                _nextRoomRefresh = 0f;
            }
            if (Time.unscaledTime >= _nextRoomRefresh)
            {
                // Rooms register as the level streams in, so the list is refreshed now and then.
                _nextRoomRefresh = Time.unscaledTime + 2f;
                _rooms = ReadRooms();
            }

            _players.Clear();
            _playerBuffer ??= new Il2CppSystem.Collections.Generic.List<PlayerEntity>();
            _playerBuffer.Clear();
            EntityManager.GetAllPlayersNoAlloc(_playerBuffer);
            for (var i = 0; i < _playerBuffer.Count; i++)
            {
                var player = _playerBuffer[i];
                if (player == null)
                    continue;
                var room = player.currentRoom;
                if (room != null)
                    _seen.Add(room.Pointer);
                var t = player.transform;
                var p = t.position;
                var f = t.forward;
                var dot = new Dot { X = p.x, Y = p.y, Z = p.z, Fx = f.x, Fz = f.z, Me = player.Pointer == me.Pointer, Name = player.playerNick };
                if (dot.Me)
                    _me = dot;
                _players.Add(dot);
            }

            _idols.Clear();
            var manager = EntityManager.Instance;
            if (manager != null && Plugin.ShowIdols.Value)
            {
                var idols = manager.Idols;
                if (idols != null)
                {
                    foreach (var idol in idols)
                    {
                        if (idol != null && idol.gameObject.activeInHierarchy)
                            _idols.Add(idol.transform.position);
                    }
                }
            }

            _monsters.Clear();
            if (manager != null && Plugin.ShowMonsters.Value)
            {
                var monsters = manager.MonsterEntities;
                for (var i = 0; monsters != null && i < monsters.Count; i++)
                {
                    var monster = monsters[i];
                    if (monster != null && monster.gameObject.activeInHierarchy)
                        _monsters.Add(monster.transform.position);
                }
            }

            _big = MapKeyHeld();
            _active = true;
        }

        private static List<RoomInfo> ReadRooms()
        {
            var result = new List<RoomInfo>();
            var rooms = Room.GetAll();
            for (var i = 0; rooms != null && i < rooms.Count; i++)
            {
                var room = rooms[i];
                if (room == null)
                    continue;
                var info = new RoomInfo { Ptr = room.Pointer, Type = room.roomType };
                var bounds = room.roomBounds;
                for (var b = 0; bounds != null && b < bounds.Count; b++)
                {
                    var box = bounds[b];
                    var min = box.min;
                    var max = box.max;
                    info.Boxes.Add(new Shape { MinX = min.x, MinZ = min.z, MaxX = max.x, MaxZ = max.z, MinY = min.y, MaxY = max.y });
                }
                if (info.Boxes.Count > 0)
                    result.Add(info);
            }
            return result;
        }

        private static bool MapKeyHeld()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return false;
            return Enum.TryParse<Key>(Plugin.MapKey.Value, true, out var key) && key != Key.None && keyboard[key].isPressed;
        }

        private void OnGUI()
        {
            if (!_active || Event.current.type != EventType.Repaint)
                return;
            if (Plugin.HideWhenCursorFree.Value && Cursor.lockState == CursorLockMode.None && Cursor.visible)
                return; // a menu, the pause screen or a shop screen is up
            try
            {
                EnsureTextures();
                var scale = Screen.height / ReferenceHeight;
                GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
                var width = Screen.width / scale;
                if (_big)
                    DrawBig(width);
                else if (Plugin.ShowMinimap.Value)
                    DrawMini(width);
            }
            catch (Exception e)
            {
                LogOnce("draw", e);
            }
            finally
            {
                GUI.matrix = Matrix4x4.identity;
            }
        }

        private void DrawMini(float width)
        {
            var size = Mathf.Clamp(Plugin.MinimapSize.Value, 120f, 500f);
            var screen = new Box(width - size - 24f, 24f, size, size);
            var radius = Mathf.Max(5f, Plugin.MinimapRange.Value);
            var view = new MapView(screen, _me.X, _me.Z, size / (2f * radius));
            DrawFrame(screen);
            DrawMap(view, names: false);
        }

        private void DrawBig(float width)
        {
            var size = ReferenceHeight * 0.8f;
            var screen = new Box((width - size) / 2f, (ReferenceHeight - size) / 2f, size, size);
            float minX = _me.X - 20f, minZ = _me.Z - 20f, maxX = _me.X + 20f, maxZ = _me.Z + 20f;
            foreach (var room in _rooms)
            {
                if (!Visible(room))
                    continue;
                foreach (var b in room.Boxes)
                {
                    minX = Math.Min(minX, b.MinX);
                    minZ = Math.Min(minZ, b.MinZ);
                    maxX = Math.Max(maxX, b.MaxX);
                    maxZ = Math.Max(maxZ, b.MaxZ);
                }
            }
            var view = MapView.Fit(screen, minX, minZ, maxX, maxZ);
            DrawFrame(screen);
            DrawMap(view, names: true);
        }

        private bool Visible(RoomInfo room) => Plugin.RevealAll.Value || _seen.Contains(room.Ptr);

        private bool SameFloor(Shape b) => _me.Y >= b.MinY - 1.5f && _me.Y <= b.MaxY + 1.5f;

        private void DrawFrame(Box screen)
        {
            GUI.DrawTexture(Rect(new Box(screen.X - 2f, screen.Y - 2f, screen.W + 4f, screen.H + 4f)), _frame);
            GUI.DrawTexture(Rect(screen), _panel);
        }

        private void DrawMap(MapView view, bool names)
        {
            // Other floors first and faint, then this floor on top.
            for (var pass = 0; pass < 2; pass++)
            {
                foreach (var room in _rooms)
                {
                    if (!Visible(room))
                        continue;
                    var fill = (room.Type & ERoomType.EndRoom) != 0 ? _exit : (room.Type & ERoomType.ShopRoom) != 0 ? _shop : _room;
                    foreach (var b in room.Boxes)
                    {
                        var same = SameFloor(b);
                        if (same != (pass == 1))
                            continue;
                        var box = view.Project(b.MinX, b.MinZ, b.MaxX, b.MaxZ);
                        if (box.Empty)
                            continue;
                        GUI.DrawTexture(Rect(box), same ? fill : _roomOther);
                        if (same)
                            Outline(view, b);
                    }
                }
            }

            foreach (var p in _monsters)
                Marker(view, p.x, p.z, 7f, _monsterTex);
            foreach (var p in _idols)
                Marker(view, p.x, p.z, 9f, _idolTex);
            foreach (var p in _players)
            {
                if (p.Me)
                    continue;
                Marker(view, p.X, p.Z, 10f, _mateTex);
                if (names && !string.IsNullOrEmpty(p.Name) && view.Screen.Contains(view.ScreenX(p.X), view.ScreenY(p.Z)))
                    GUI.Label(new Rect(view.ScreenX(p.X) + 8f, view.ScreenY(p.Z) - 11f, 220f, 22f), p.Name, _nameStyle);
            }

            // You: a dot with three smaller ones trailing out the way you face.
            var len = Mathf.Sqrt(_me.Fx * _me.Fx + _me.Fz * _me.Fz);
            if (len > 0.01f)
            {
                for (var i = 1; i <= 3; i++)
                {
                    var d = (5f + i * 5f) / view.Scale;
                    Marker(view, _me.X + _me.Fx / len * d, _me.Z + _me.Fz / len * d, 6f - i, _meTex);
                }
            }
            Marker(view, _me.X, _me.Z, 12f, _meTex);
        }

        private void Outline(MapView view, Shape b)
        {
            const float t = 1.5f;
            var x0 = view.ScreenX(b.MinX);
            var x1 = view.ScreenX(b.MaxX);
            var y0 = view.ScreenY(b.MaxZ);
            var y1 = view.ScreenY(b.MinZ);
            foreach (var edge in new[]
            {
                new Box(x0, y0, x1 - x0, t), new Box(x0, y1 - t, x1 - x0, t),
                new Box(x0, y0, t, y1 - y0), new Box(x1 - t, y0, t, y1 - y0),
            })
            {
                var clipped = edge.Intersect(view.Screen);
                if (!clipped.Empty)
                    GUI.DrawTexture(Rect(clipped), _edge);
            }
        }

        private static void Marker(MapView view, float x, float z, float size, Texture2D texture)
        {
            var sx = view.ScreenX(x);
            var sy = view.ScreenY(z);
            if (!view.Screen.Contains(sx, sy))
                return;
            GUI.DrawTexture(new Rect(sx - size / 2f, sy - size / 2f, size, size), texture);
        }

        private static Rect Rect(Box b) => new Rect(b.X, b.Y, b.W, b.H);

        private void EnsureTextures()
        {
            if (_panel != null && _nameStyle != null)
                return;
            _panel = Solid(new Color(0.05f, 0.03f, 0.06f, 0.72f));
            _frame = Solid(new Color(0.94f, 0.78f, 0.37f, 0.9f));
            _room = Solid(new Color(0.42f, 0.38f, 0.48f, 0.85f));
            _roomOther = Solid(new Color(0.42f, 0.38f, 0.48f, 0.25f));
            _edge = Solid(new Color(0.85f, 0.82f, 0.9f, 0.9f));
            _exit = Solid(new Color(0.25f, 0.62f, 0.35f, 0.9f));
            _shop = Solid(new Color(0.25f, 0.45f, 0.75f, 0.9f));
            _meTex = Solid(new Color(1f, 0.85f, 0.35f, 1f));
            _mateTex = Solid(new Color(0.35f, 0.9f, 1f, 1f));
            _idolTex = Solid(new Color(1f, 0.55f, 0.1f, 1f));
            _monsterTex = Solid(new Color(1f, 0.25f, 0.25f, 1f));
            _nameStyle = new GUIStyle { fontSize = 16, fontStyle = FontStyle.Bold };
            _nameStyle.normal.textColor = new Color(0.35f, 0.9f, 1f, 1f);
        }

        private static Texture2D Solid(Color color)
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        private void LogOnce(string where, Exception e)
        {
            if (_loggedError)
                return;
            _loggedError = true;
            Plugin.Log.LogError($"Minimap {where} failed: {e}");
        }
    }
}
