using System;
using System.Collections.Generic;
using _200_Dev.Entities;
using Entities.Player;
using EnvironmentElements;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace InvokyrMinimap
{
    /// <summary>
    /// A corner minimap and a large map while the map key is held. Rooms come from the game's room boxes
    /// (<see cref="Room.roomBounds"/>); once someone on the team has been in a room (or with RevealAll) its box is scanned
    /// with downward raycasts on a 0.4 m grid against the level's floor layers, so the map shows the real floor and walls
    /// instead of the oversized trigger boxes. The map is painted on the CPU into a texture (<see cref="MapPainter"/>), so
    /// it can turn with your view; OnGUI only draws textures and labels (IMGUI controls may be stripped in IL2CPP).
    /// </summary>
    public sealed class MinimapController : MonoBehaviour
    {
        public MinimapController(IntPtr ptr) : base(ptr) { }

        private const float ReferenceHeight = 1080f;
        private const int CellsPerFrame = 400;
        private const float WallRadius = 0.22f;

        private struct Shape
        {
            public float MinX, MinZ, MaxX, MaxZ, MinY, MaxY;
        }

        private sealed class RoomInfo
        {
            public IntPtr Ptr;
            public byte Flags;
            public readonly List<Shape> Boxes = new List<Shape>();
        }

        private struct Sample
        {
            public int Ix, Iz, Start, Count;
        }

        /// <summary>One room being scanned: each box is sampled cell by cell, then its samples are sorted into floor/pit/wall.</summary>
        private sealed class ScanJob
        {
            public RoomInfo Room;
            public int Box, Ix, Iz, Next, Found;
            public bool Started, Sampled;
            public float Level;
            public readonly List<Sample> Samples = new List<Sample>();
            public readonly List<float> Ys = new List<float>();
            public readonly List<float> Lowest = new List<float>();
        }

        private struct Dot
        {
            public float X, Y, Z, Fx, Fz;
            public bool Me;
            public string Name;
        }

        private readonly HashSet<IntPtr> _seen = new HashSet<IntPtr>();
        private readonly HashSet<IntPtr> _queued = new HashSet<IntPtr>();
        private readonly Queue<ScanJob> _jobs = new Queue<ScanJob>();
        private readonly FloorGrid _grid = new FloorGrid();
        private List<RoomInfo> _rooms = new List<RoomInfo>();
        private readonly List<Dot> _players = new List<Dot>();
        private readonly List<Vector3> _idols = new List<Vector3>();
        private readonly List<Vector3> _monsters = new List<Vector3>();
        private Il2CppSystem.Collections.Generic.List<PlayerEntity> _playerBuffer;
        private Il2CppStructArray<RaycastHit> _hits;
        private int _mask = -1;
        private Dot _me;
        private bool _active;
        private bool _big;
        private bool _revealAll;
        private int _sceneHandle;
        private float _nextRoomRefresh;
        private bool _loggedError, _loggedFallback;

        private Raster _miniRaster, _bigRaster;
        private Texture2D _miniTex, _bigTex;
        private Texture2D _panel, _frame;
        private GUIStyle _nameStyle;

        private static readonly Rgba MeColor = Rgba.Of(1f, 0.85f, 0.35f, 1f);
        private static readonly Rgba Outline = Rgba.Of(0.05f, 0.03f, 0.06f, 0.95f);
        private static readonly Rgba MateColor = Rgba.Of(0.35f, 0.9f, 1f, 1f);
        private static readonly Rgba IdolColor = Rgba.Of(1f, 0.55f, 0.1f, 1f);
        private static readonly Rgba MonsterColor = Rgba.Of(1f, 0.25f, 0.25f, 1f);

        private void Update()
        {
            try
            {
                Collect();
                Scan();
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
            if (scene != _sceneHandle || _revealAll != Plugin.RevealAll.Value)
            {
                if (scene != _sceneHandle)
                    _seen.Clear();
                _sceneHandle = scene;
                _revealAll = Plugin.RevealAll.Value;
                _rooms.Clear();
                _queued.Clear();
                _jobs.Clear();
                _grid.Clear();
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
                var p = player.transform.position;
                var dot = new Dot { X = p.x, Y = p.y, Z = p.z, Me = player.Pointer == me.Pointer, Name = player.playerNick };
                if (dot.Me)
                {
                    var f = LookDirection(player);
                    dot.Fx = f.x;
                    dot.Fz = f.z;
                    _me = dot;
                }
                _players.Add(dot);
            }

            foreach (var room in _rooms)
            {
                if (Visible(room) && _queued.Add(room.Ptr))
                    _jobs.Enqueue(new ScanJob { Room = room });
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

        /// <summary>Where you are looking, flattened: the camera if there is one, else the player's look transform, else the body.</summary>
        private static Vector3 LookDirection(PlayerEntity player)
        {
            var cam = Camera.main;
            Vector3 f;
            if (cam != null)
                f = cam.transform.forward;
            else
            {
                var look = player.playerCameraController?.PlayerLook;
                f = look != null ? look.forward : player.transform.forward;
            }
            f.y = 0f;
            return f.sqrMagnitude > 1e-6f ? f.normalized : Vector3.forward;
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
                var type = room.roomType;
                var flags = (type & ERoomType.EndRoom) != 0 ? MapPainter.Exit : (type & ERoomType.ShopRoom) != 0 ? MapPainter.Shop : (byte)0;
                var info = new RoomInfo { Ptr = room.Pointer, Flags = flags };
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

        // Layers the level's floors and walls are on; props, entities and triggers are left out so furniture reads as floor.
        private int Mask()
        {
            if (_mask != -1)
                return _mask;
            var mask = 0;
            foreach (var name in new[] { "Default", "Ground", "Wood", "Carpet", "Tile" })
            {
                var layer = LayerMask.NameToLayer(name);
                if (layer >= 0)
                    mask |= 1 << layer;
            }
            _mask = mask == 0 ? Physics.DefaultRaycastLayers : mask;
            return _mask;
        }

        /// <summary>Works through the queued rooms a few hundred cells per frame, writing the results into the floor grid.</summary>
        private void Scan()
        {
            if (!_active)
                return;
            _hits ??= new Il2CppStructArray<RaycastHit>(16);
            var budget = CellsPerFrame;
            while (budget > 0 && _jobs.Count > 0)
            {
                var job = _jobs.Peek();
                var boxes = job.Room.Boxes;
                if (job.Box >= boxes.Count)
                {
                    _jobs.Dequeue();
                    if (job.Found == 0)
                        FillBoxes(job.Room);
                    continue;
                }
                var b = boxes[job.Box];
                if (!job.Started)
                {
                    job.Started = true;
                    job.Sampled = false;
                    job.Ix = FloorGrid.Index(b.MinX);
                    job.Iz = FloorGrid.Index(b.MinZ);
                    job.Next = 0;
                    job.Samples.Clear();
                    job.Ys.Clear();
                    job.Lowest.Clear();
                }
                if (!job.Sampled)
                {
                    if (job.Iz > FloorGrid.Index(b.MaxZ))
                    {
                        job.Sampled = true;
                        job.Level = FloorClassifier.Level(job.Lowest);
                        continue;
                    }
                    SampleCell(job, b);
                    budget--;
                    if (++job.Ix > FloorGrid.Index(b.MaxX))
                    {
                        job.Ix = FloorGrid.Index(b.MinX);
                        job.Iz++;
                    }
                    continue;
                }
                if (job.Next >= job.Samples.Count)
                {
                    job.Box++;
                    job.Started = false;
                    continue;
                }
                if (ClassifyCell(job, b, job.Samples[job.Next++]))
                    job.Found++;
                budget--;
            }
        }

        // Every upward-facing surface under the cell's centre, from the top of the box to a metre below it.
        private void SampleCell(ScanJob job, Shape b)
        {
            var x = FloorGrid.Center(job.Ix);
            var z = FloorGrid.Center(job.Iz);
            if (x < b.MinX || x > b.MaxX || z < b.MinZ || z > b.MaxZ)
                return;
            var top = b.MaxY + 0.1f;
            var n = Physics.RaycastNonAlloc(new Ray(new Vector3(x, top, z), Vector3.down), _hits, top - b.MinY + 1f, Mask(), QueryTriggerInteraction.Ignore);
            var start = job.Ys.Count;
            var lowest = float.MaxValue;
            for (var i = 0; i < n; i++)
            {
                var hit = _hits[i];
                if (hit.normal.y < 0.7f)
                    continue;
                job.Ys.Add(hit.point.y);
                lowest = Math.Min(lowest, hit.point.y);
            }
            job.Samples.Add(new Sample { Ix = job.Ix, Iz = job.Iz, Start = start, Count = job.Ys.Count - start });
            if (lowest < float.MaxValue)
                job.Lowest.Add(lowest);
        }

        private bool ClassifyCell(ScanJob job, Shape b, Sample s)
        {
            var kind = FloorClassifier.Classify(job.Ys, s.Start, s.Count, job.Level, out var y);
            if (kind == CellKind.None)
                return false;
            var x = FloorGrid.Center(s.Ix);
            var z = FloorGrid.Center(s.Iz);
            // Something solid at chest height over the floor is a wall (props and entities are on other layers).
            if (kind == CellKind.Floor &&
                Physics.CheckCapsule(new Vector3(x, y + 0.7f, z), new Vector3(x, y + 1.6f, z), WallRadius, Mask(), QueryTriggerInteraction.Ignore))
                kind = CellKind.Wall;
            if (kind != CellKind.Floor)
                y = job.Level;
            var depth = Math.Min(Math.Min(x - b.MinX, b.MaxX - x), Math.Min(z - b.MinZ, b.MaxZ - z));
            _grid.Add(s.Ix, s.Iz, new Cell { Kind = kind, Flags = job.Room.Flags, Y = y, Depth = depth });
            return true;
        }

        // A room whose scan found no floor at all (unexpected layers) still shows, as its boxes.
        private void FillBoxes(RoomInfo room)
        {
            if (!_loggedFallback)
            {
                _loggedFallback = true;
                Plugin.Log.LogWarning("Minimap: a room's floor scan found nothing; showing its room box instead.");
            }
            foreach (var b in room.Boxes)
            {
                for (var iz = FloorGrid.Index(b.MinZ); iz <= FloorGrid.Index(b.MaxZ); iz++)
                    for (var ix = FloorGrid.Index(b.MinX); ix <= FloorGrid.Index(b.MaxX); ix++)
                        _grid.Add(ix, iz, new Cell { Kind = CellKind.Floor, Flags = room.Flags, Y = b.MinY, Depth = 0f });
            }
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
                    DrawBig(width, scale);
                else if (Plugin.ShowMinimap.Value)
                    DrawMini(width, scale);
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

        private bool Rotate => Plugin.RotateWithView.Value;

        private void DrawMini(float width, float scale)
        {
            var size = Mathf.Clamp(Plugin.MinimapSize.Value, 120f, 500f);
            var screen = Layout.Corner(Plugin.MinimapCorner.Value, size, Plugin.OffsetX.Value, Plugin.OffsetY.Value, width, ReferenceHeight);
            var px = Mathf.Clamp(Mathf.RoundToInt(size * scale), 64, 600);
            EnsureRaster(ref _miniRaster, ref _miniTex, px);
            var radius = Mathf.Max(5f, Plugin.MinimapRange.Value);
            var view = Rotate
                ? new MapView(new Box(0, 0, px, px), _me.X, _me.Z, px / (2f * radius), _me.Fx, _me.Fz)
                : new MapView(new Box(0, 0, px, px), _me.X, _me.Z, px / (2f * radius));
            Draw(screen, view, _miniRaster, _miniTex, names: false);
        }

        private void DrawBig(float width, float scale)
        {
            var size = ReferenceHeight * 0.8f;
            var screen = new Box((width - size) / 2f, (ReferenceHeight - size) / 2f, size, size);
            var px = Mathf.Clamp(Mathf.RoundToInt(size * scale), 128, 720);
            EnsureRaster(ref _bigRaster, ref _bigTex, px);
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
            var target = new Box(0, 0, px, px);
            MapView view;
            if (Rotate)
            {
                // Fit the bounds' circle so the zoom stays put while you turn.
                var half = Mathf.Sqrt((maxX - minX) * (maxX - minX) + (maxZ - minZ) * (maxZ - minZ)) / 2f;
                view = new MapView(target, (minX + maxX) / 2f, (minZ + maxZ) / 2f, px / (2f * Math.Max(1f, half)) * 0.94f, _me.Fx, _me.Fz);
            }
            else
                view = MapView.Fit(target, minX, minZ, maxX, maxZ);
            Draw(screen, view, _bigRaster, _bigTex, names: true);
        }

        private void Draw(Box screen, MapView view, Raster raster, Texture2D texture, bool names)
        {
            MapPainter.Paint(raster, _grid, view, _me.Y);
            var k = raster.Width / screen.W; // raster pixels per GUI pixel

            foreach (var p in _monsters)
                Mark(view, p.x, p.z, (cx, cy) => raster.FillSquare(cx, cy, 3.5f * k, MonsterColor));
            foreach (var p in _idols)
                Mark(view, p.x, p.z, (cx, cy) =>
                {
                    raster.FillDiamond(cx, cy, 7f * k, Outline);
                    raster.FillDiamond(cx, cy, 5.5f * k, IdolColor);
                });
            foreach (var p in _players)
            {
                if (p.Me)
                    continue;
                Mark(view, p.X, p.Z, (cx, cy) =>
                {
                    raster.FillCircle(cx, cy, 6.5f * k, Outline);
                    raster.FillCircle(cx, cy, 5f * k, MateColor);
                });
            }

            // You: an arrow the way you look (always up when the map turns with you).
            view.Direction(_me.Fx, _me.Fz, out var dx, out var dy);
            var mx = view.ScreenX(_me.X, _me.Z);
            var my = view.ScreenY(_me.X, _me.Z);
            raster.FillArrow(mx, my, dx, dy, 22f * k, Outline);
            raster.FillArrow(mx, my, dx, dy, 16f * k, MeColor);

            Upload(raster, texture);
            GUI.DrawTexture(new Rect(screen.X - 2f, screen.Y - 2f, screen.W + 4f, screen.H + 4f), _frame);
            GUI.DrawTexture(new Rect(screen.X, screen.Y, screen.W, screen.H), _panel);
            GUI.DrawTexture(new Rect(screen.X, screen.Y, screen.W, screen.H), texture);

            if (!names)
                return;
            foreach (var p in _players)
            {
                if (p.Me || string.IsNullOrEmpty(p.Name))
                    continue;
                var sx = view.ScreenX(p.X, p.Z);
                var sy = view.ScreenY(p.X, p.Z);
                if (view.Screen.Contains(sx, sy))
                    GUI.Label(new Rect(screen.X + sx / k + 9f, screen.Y + sy / k - 11f, 220f, 22f), p.Name, _nameStyle);
            }
        }

        private static void Mark(MapView view, float x, float z, Action<float, float> draw)
        {
            var sx = view.ScreenX(x, z);
            var sy = view.ScreenY(x, z);
            if (view.Screen.Contains(sx, sy))
                draw(sx, sy);
        }

        private bool Visible(RoomInfo room) => _revealAll || _seen.Contains(room.Ptr);

        private static void EnsureRaster(ref Raster raster, ref Texture2D texture, int size)
        {
            if (raster != null && raster.Width == size && texture != null)
                return;
            raster = new Raster(size, size);
            if (texture != null)
                Destroy(texture);
            texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
        }

        private static unsafe void Upload(Raster raster, Texture2D texture)
        {
            fixed (byte* p = raster.Pixels)
                texture.LoadRawTextureData((IntPtr)p, raster.Pixels.Length);
            texture.Apply(false);
        }

        private void EnsureTextures()
        {
            if (_panel != null && _nameStyle != null)
                return;
            _panel = Solid(new Color(0.05f, 0.03f, 0.06f, 0.72f));
            _frame = Solid(new Color(0.94f, 0.78f, 0.37f, 0.9f));
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
