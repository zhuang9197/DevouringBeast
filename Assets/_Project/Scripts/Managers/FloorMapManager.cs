using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using System;

namespace DevouringBeast
{
    /// <summary>Generates and runs one floor containing ten adjacent, screen-sized rooms.</summary>
    [DisallowMultipleComponent]
    public sealed class FloorMapManager : MonoBehaviour
    {
        public const int FinalFloor = 5;
        private sealed class RoomState
        {
            public Vector2Int Cell;
            public RoomKind Kind;
            public bool Cleared;
            public bool Visited;
            public bool IsDemonRoom;
            public bool HasFloorExit;
        }

        private static readonly Vector2Int[] Directions =
        {
            Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left
        };
        private const int RoomColumns = 16;
        private const int RoomRows = 9;
        private const int RequiredRoomsPerFloor = 10;

        [SerializeField] private Vector2 roomSize = new(32f, 18f);
        [SerializeField, Min(2f)] private float doorWidth = 2f;
        [SerializeField, Min(0.2f)] private float wallThickness = 1.5f;
        [SerializeField, Min(0f)] private float walkableInset = 1.5f;
        [SerializeField, Min(0.5f)] private float doorTriggerDepth = 2.2f;
        [SerializeField, Min(0f)] private float doorArrivalClearance = 0.8f;
        [SerializeField, Range(0.05f, 1f)] private float doorEnterInputThreshold = 0.35f;

        private readonly List<RoomState> _rooms = new(10);
        private readonly List<StatueController> _statues = new();
        private readonly Dictionary<Vector2Int, int> _roomByCell = new();
        private Vector2 _floorOrigin = new(40f, 40f);
        private Transform _roomCollisionRoot;
        private GameObject _floorVisualRoot;
        private Tilemap _floorTilemap;
        private TileBase _cornerTile;
        private TileBase _wallTile;
        private TileBase _openDoorTile;
        private TileBase[] _floorTiles;
        private Vector3 _tileCellSize;
        private Transform _player;
        private PlayerController _playerController;
        private CameraFollow _cameraFollow;
        private MapBounds _mapBounds;
        private WaveManager _waves;
        private EnvironmentItemSpawner _environmentItems;
        private int _currentRoom;
        private int _floor = 1;
        private int _clearedElites;
        private int _demonRoomIndex = -1;
        private bool _transitioning;
        private bool _roomCombatLocked;
        private GameObject _floorExit;

        public int CurrentFloor => _floor;
        public int CurrentRoomIndex => _currentRoom;
        public int MinimapRoomCount => _rooms.Count;
        public int LayoutVersion { get; private set; }
        public event Action MinimapChanged;

        public bool TryGetMinimapRoom(int index, out Vector2Int cell, out bool visited,
            out bool cleared, out bool current, out bool adjacent)
        {
            return TryGetMinimapRoom(index, out cell, out visited, out cleared, out current, out adjacent,
                out _, out _);
        }

        public bool TryGetMinimapRoom(int index, out Vector2Int cell, out bool visited,
            out bool cleared, out bool current, out bool adjacent, out bool demonRoom, out bool floorExit)
        {
            if (index < 0 || index >= _rooms.Count)
            {
                cell = default;
                visited = cleared = current = adjacent = demonRoom = floorExit = false;
                return false;
            }

            RoomState room = _rooms[index];
            cell = room.Cell;
            visited = room.Visited;
            cleared = room.Cleared;
            current = index == _currentRoom;
            demonRoom = room.IsDemonRoom;
            floorExit = room.HasFloorExit;
            adjacent = _currentRoom >= 0 && _currentRoom < _rooms.Count &&
                Mathf.Abs(room.Cell.x - _rooms[_currentRoom].Cell.x) +
                Mathf.Abs(room.Cell.y - _rooms[_currentRoom].Cell.y) == 1;
            return true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            EnsureForScene(SceneManager.GetActiveScene());
        }

        public static void EnsureForScene(Scene scene)
        {
            if (scene.name != SceneNames.Game || !scene.isLoaded) return;

            FloorMapManager[] managers = FindObjectsOfType<FloorMapManager>(true);
            foreach (FloorMapManager manager in managers)
                if (manager != null && manager.gameObject.scene == scene) return;

            GameObject managerObject = new("FloorMapManager");
            SceneManager.MoveGameObjectToScene(managerObject, scene);
            managerObject.AddComponent<FloorMapManager>();
        }

        private IEnumerator Start()
        {
            _waves = FindObjectOfType<WaveManager>();
            _mapBounds = FindObjectOfType<MapBounds>();
            _environmentItems = FindObjectOfType<EnvironmentItemSpawner>();
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            _player = playerObject != null ? playerObject.transform : null;
            _playerController = playerObject != null ? playerObject.GetComponent<PlayerController>() : null;
            Camera camera = Camera.main;
            _cameraFollow = camera != null ? camera.GetComponent<CameraFollow>() : null;
            if (_player != null) GroundShadow.Ensure(_player.gameObject);

            while (_waves == null)
            {
                _waves = FindObjectOfType<WaveManager>();
                yield return null;
            }

            SaveGameService.Initialize();
            SaveSlotData save = SaveGameService.GetActiveSlot();
            _floor = save != null ? Mathf.Clamp(save.completedWave + 1, 1, FinalFloor) : 1;
            BuildFloor();
        }

        private void BuildFloor()
        {
            _waves.ResetForFloor();
            BloodDrop.ReleaseFloorDrops();
            EnemyRewardChest.ReleaseFloorChests();
            _environmentItems?.ResetForFloor();
            if (_floorVisualRoot != null) Destroy(_floorVisualRoot);
            if (_roomCollisionRoot != null) Destroy(_roomCollisionRoot.gameObject);
            if (_floorExit != null) Destroy(_floorExit);
            foreach (StatueController statue in _statues)
                if (statue != null) Destroy(statue.gameObject);
            _statues.Clear();
            _clearedElites = 0;
            _demonRoomIndex = -1;
            _roomCombatLocked = false;
            GenerateLayout();
            LayoutVersion++;
            CreateFloorTilemap();
            bool testMode = GameManager.Instance.IsTestMode;
            if (!testMode) CreateStatues();
            else _environmentItems?.EnableTestMode();
            FloorMinimapUI.EnsureFor(this);
            _currentRoom = 0;
            EnterRoom(0, Vector2Int.zero, true);
            if (testMode) DeveloperTestPanel.EnsureFor(this);
        }

        private void GenerateLayout()
        {
            _rooms.Clear();
            _roomByCell.Clear();
            AddRoom(Vector2Int.zero);
            if (GameManager.Instance.IsTestMode)
            {
                _rooms[0].Cleared = true;
                return;
            }
            int safety = 0;
            while (_rooms.Count < RequiredRoomsPerFloor && safety++ < 1000)
            {
                RoomState source = _rooms[UnityEngine.Random.Range(0, _rooms.Count)];
                Vector2Int candidate = source.Cell + Directions[UnityEngine.Random.Range(0, Directions.Length)];
                if (!_roomByCell.ContainsKey(candidate)) AddRoom(candidate);
            }

            int firstElite = UnityEngine.Random.Range(1, _rooms.Count);
            int secondElite;
            do secondElite = UnityEngine.Random.Range(1, _rooms.Count); while (secondElite == firstElite);
            _rooms[firstElite].Kind = RoomKind.Elite;
            _rooms[secondElite].Kind = RoomKind.Elite;
            int boss = -1;
            if (_floor >= FinalFloor)
            {
                int farthest = -1;
                for (int i = 1; i < _rooms.Count; i++)
                {
                    int distance = Mathf.Abs(_rooms[i].Cell.x) + Mathf.Abs(_rooms[i].Cell.y);
                    if (distance > farthest)
                    {
                        farthest = distance;
                        boss = i;
                    }
                }
                _rooms[firstElite].Kind = RoomKind.Normal;
                _rooms[secondElite].Kind = RoomKind.Normal;
                _rooms[boss].Kind = RoomKind.Boss;
            }
            string eliteSummary = _floor < FinalFloor ? $"{firstElite},{secondElite}" : "none";
            Debug.Log($"[FloorMapManager] Floor {_floor}: rooms={_rooms.Count}, elites={eliteSummary}, boss={boss}");
        }

        private void CreateStatues()
        {
            Sprite angel = Resources.Load<Sprite>("Statues/angel_statue");
            Sprite angelDestroyed = Resources.Load<Sprite>("Statues/angel_statue_destory");
            Sprite demon = Resources.Load<Sprite>("Statues/demon_statue");
            Sprite demonDestroyed = Resources.Load<Sprite>("Statues/demon_statue_destory");
            Sprite pope = Resources.Load<Sprite>("Statues/pope_statue");
            Sprite popeDestroyed = Resources.Load<Sprite>("Statues/pope_statue_destory");
            if (angel == null || demon == null || pope == null)
            {
                Debug.LogWarning("[FloorMapManager] Statue sprites are missing from Resources/Statues.");
                return;
            }

            int demonRoom = UnityEngine.Random.Range(1, _rooms.Count);
            _demonRoomIndex = demonRoom;
            _rooms[demonRoom].IsDemonRoom = true;
            foreach (RoomState room in _rooms)
            {
                CreateStatue(StatueKind.Pope, room, GetRoomCenter(room.Cell) + Vector2.left * 5f, pope, popeDestroyed);
                if (room == _rooms[0])
                    CreateStatue(StatueKind.Angel, room, GetRoomCenter(room.Cell) + Vector2.up * 3f, angel, angelDestroyed);
                if (_rooms.IndexOf(room) == demonRoom)
                    CreateStatue(StatueKind.Demon, room, GetRoomCenter(room.Cell) + Vector2.right * 5f, demon, demonDestroyed);
            }
        }

        private void CreateStatue(StatueKind kind, RoomState room, Vector2 position, Sprite intact, Sprite destroyed)
        {
            GameObject statueObject = new($"{kind}Statue", typeof(SpriteRenderer), typeof(CircleCollider2D), typeof(StatueController));
            statueObject.transform.SetParent(transform, false);
            statueObject.transform.position = position;
            StatueController statue = statueObject.GetComponent<StatueController>();
            statue.Initialize(kind, room.Cell, this, _environmentItems, intact, destroyed);
            _statues.Add(statue);
        }

        private void AddRoom(Vector2Int cell)
        {
            int index = _rooms.Count;
            _rooms.Add(new RoomState { Cell = cell, Kind = RoomKind.Normal });
            _roomByCell.Add(cell, index);
        }

        private void CreateFloorTilemap()
        {
            _cornerTile = Resources.Load<TileBase>("Map/Tiles/new_map1");
            _openDoorTile = Resources.Load<TileBase>("Map/Tiles/new_map2");
            _wallTile = Resources.Load<TileBase>("Map/Tiles/new_map3");
            _floorTiles = new[]
            {
                Resources.Load<TileBase>("Map/Tiles/new_map4"),
                Resources.Load<TileBase>("Map/Tiles/new_map4_1"),
                Resources.Load<TileBase>("Map/Tiles/new_map4_2"),
                Resources.Load<TileBase>("Map/Tiles/new_map4_3"),
                Resources.Load<TileBase>("Map/Tiles/new_map4_4")
            };
            if (_cornerTile == null || _openDoorTile == null || _wallTile == null ||
                Array.Exists(_floorTiles, tile => tile == null))
            {
                Debug.LogError("[FloorMapManager] Missing new_map room tiles. Run Rebuild Room Tilemap and Clean Scene.");
                return;
            }
            _floorVisualRoot = new GameObject("FloorRooms", typeof(Grid));
            _floorVisualRoot.transform.position = new Vector3(
                _floorOrigin.x - roomSize.x * 0.5f,
                _floorOrigin.y - roomSize.y * 0.5f,
                0f);
            Grid grid = _floorVisualRoot.GetComponent<Grid>();
            _tileCellSize = new Vector3(
                roomSize.x / RoomColumns,
                roomSize.y / RoomRows,
                1f);
            grid.cellSize = _tileCellSize;
            GameObject tilemapObject = new("RoomTilemap", typeof(Tilemap), typeof(TilemapRenderer));
            tilemapObject.transform.SetParent(_floorVisualRoot.transform, false);
            _floorTilemap = tilemapObject.GetComponent<Tilemap>();
            TilemapRenderer renderer = tilemapObject.GetComponent<TilemapRenderer>();
            renderer.mode = TilemapRenderer.Mode.Chunk;
            renderer.sortingOrder = -20;
            foreach (RoomState room in _rooms)
                DrawRoom(room);
            _floorTilemap.CompressBounds();
        }

        private void DrawRoom(RoomState room)
        {
            for (int y = 0; y < RoomRows; y++)
            for (int x = 0; x < RoomColumns; x++)
            {
                TileBase tile;
                float rotation = 0f;
                bool flipX = false;
                bool flipY = false;
                bool horizontalEdge = y == 0 || y == RoomRows - 1;
                bool verticalEdge = x == 0 || x == RoomColumns - 1;
                if (horizontalEdge && verticalEdge)
                {
                    tile = _cornerTile;
                    rotation = GetCornerRotation(x, y);
                }
                else if (horizontalEdge || verticalEdge)
                {
                    Vector2Int direction = horizontalEdge
                        ? (y == 0 ? Vector2Int.down : Vector2Int.up)
                        : (x == 0 ? Vector2Int.left : Vector2Int.right);
                    bool openDoor = room.Cleared && _roomByCell.ContainsKey(room.Cell + direction) &&
                                    new Vector2Int(x, y) == GetDoorCell(direction);
                    tile = openDoor ? _openDoorTile : _wallTile;
                    rotation = openDoor ? GetDoorRotation(direction) : GetWallRotation(direction);
                }
                else
                {
                    GetFloorTile(x - 1, y - 1, out tile, out flipX, out flipY);
                }

                SetRoomTile(room, x, y, tile, rotation, flipX, flipY);
            }
        }

        /// <summary>
        /// Uses a continuous strip cut from new_map.png, then ping-pongs it with mirrored copies.
        /// Every shared edge therefore samples the same source pixels instead of exposing the
        /// hard seam produced by repeating one non-tileable hand-painted crop.
        /// </summary>
        private void GetFloorTile(int x, int y, out TileBase tile, out bool flipX, out bool flipY)
        {
            int count = _floorTiles.Length;
            int phase = x % (count * 2);
            if (phase < count)
            {
                tile = _floorTiles[phase];
                flipX = false;
            }
            else
            {
                tile = _floorTiles[count * 2 - 1 - phase];
                flipX = true;
            }

            // Alternate the vertical direction. Adjacent rows then meet on an identical source edge.
            flipY = (y & 1) != 0;
        }

        private static float GetCornerRotation(int x, int y)
        {
            if (x == 0 && y == RoomRows - 1) return 0f;
            if (x == 0 && y == 0) return 90f;
            if (x == RoomColumns - 1 && y == 0) return 180f;
            return -90f;
        }

        private static float GetWallRotation(Vector2Int direction)
        {
            if (direction == Vector2Int.up) return 0f;
            if (direction == Vector2Int.left) return 90f;
            if (direction == Vector2Int.down) return 180f;
            return -90f;
        }

        private static float GetDoorRotation(Vector2Int direction)
        {
            if (direction == Vector2Int.down) return 0f;
            if (direction == Vector2Int.right) return 90f;
            if (direction == Vector2Int.up) return 180f;
            return -90f;
        }

        private static Vector2Int GetDoorCell(Vector2Int direction)
        {
            if (direction == Vector2Int.up) return new Vector2Int(RoomColumns / 2, RoomRows - 1);
            if (direction == Vector2Int.down) return new Vector2Int(RoomColumns / 2, 0);
            if (direction == Vector2Int.right) return new Vector2Int(RoomColumns - 1, RoomRows / 2);
            return new Vector2Int(0, RoomRows / 2);
        }

        private void SetRoomTile(
            RoomState room,
            int x,
            int y,
            TileBase tile,
            float rotation,
            bool flipX = false,
            bool flipY = false)
        {
            Vector3Int position = new(
                room.Cell.x * RoomColumns + x,
                room.Cell.y * RoomRows + y,
                0);
            _floorTilemap.SetTile(position, tile);
            _floorTilemap.SetTransformMatrix(position, CreateTileMatrix(tile, rotation, flipX, flipY));
        }

        private Matrix4x4 CreateTileMatrix(TileBase tile, float rotation, bool flipX, bool flipY)
        {
            Vector2 spriteSize = Vector2.one;
            if (tile is Tile unityTile && unityTile.sprite != null)
                spriteSize = unityTile.sprite.bounds.size;
            Vector3 scale = new(
                (flipX ? -1f : 1f) * _tileCellSize.x / Mathf.Max(0.001f, spriteSize.x),
                (flipY ? -1f : 1f) * _tileCellSize.y / Mathf.Max(0.001f, spriteSize.y),
                1f);
            return Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0f, 0f, rotation), scale);
        }

        private void SetRoomDoorsVisual(RoomState room, bool locked)
        {
            if (_floorTilemap == null) return;
            foreach (Vector2Int direction in Directions)
            {
                if (!_roomByCell.ContainsKey(room.Cell + direction)) continue;
                Vector2Int cell = GetDoorCell(direction);
                TileBase tile = locked ? _wallTile : _openDoorTile;
                float rotation = locked ? GetWallRotation(direction) : GetDoorRotation(direction);
                SetRoomTile(room, cell.x, cell.y, tile, rotation);
            }
        }

        private void EnterRoom(int roomIndex, Vector2Int travelDirection, bool initial)
        {
            if (roomIndex < 0 || roomIndex >= _rooms.Count) return;
            if (!initial && _currentRoom == 0 && roomIndex != 0)
                foreach (StatueController statue in _statues)
                    if (statue != null && statue.Kind == StatueKind.Angel && statue.Room == _rooms[0].Cell)
                        statue.DestroyWhenLeavingStartRoom();
            _transitioning = true;
            _currentRoom = roomIndex;
            RoomState room = _rooms[roomIndex];
            room.Visited = true;
            Vector2 center = GetRoomCenter(room.Cell);
            if (_player != null)
            {
                float arrivalInset = walkableInset + doorTriggerDepth * 0.5f + doorArrivalClearance;
                Vector2 entryOffset = initial ? Vector2.zero : new Vector2(
                    -travelDirection.x * Mathf.Max(0f, roomSize.x * 0.5f - arrivalInset),
                    -travelDirection.y * Mathf.Max(0f, roomSize.y * 0.5f - arrivalInset));
                _player.position = center + entryOffset;
            }
            _mapBounds?.ConfigureRoom(center, roomSize, false, walkableInset);
            _cameraFollow?.SetRoom(center, roomSize);
            RebuildRoomCollisions(room);
            _environmentItems?.SetCurrentRoom(room.Cell, center, roomSize, room.Cleared);

            if (room.Cleared)
            {
                _roomCombatLocked = false;
                _waves.LeaveClearedRoom();
            }
            else
            {
                _roomCombatLocked = true;
                SetDoorsLocked(true);
                _waves.BeginRoom(room.Kind, _floor, center, roomSize, HandleRoomCleared);
            }
            MinimapChanged?.Invoke();
            StartCoroutine(ReleaseTransitionLock());
        }

        private IEnumerator ReleaseTransitionLock()
        {
            yield return null;
            _transitioning = false;
        }

        private void HandleRoomCleared()
        {
            RoomState room = _rooms[_currentRoom];
            room.Cleared = true;
            _roomCombatLocked = false;
            if (room.Kind == RoomKind.Elite) _clearedElites++;
            SetDoorsLocked(false);
            _environmentItems?.SetCurrentRoomCleared(true);
            if (_floor >= FinalFloor && room.Kind == RoomKind.Boss)
            {
                CompleteRun();
            }
            else if (_floor < FinalFloor && _clearedElites >= 2)
            {
                CreateFloorExit(GetRoomCenter(room.Cell));
            }
            MinimapChanged?.Invoke();
        }

        public bool TryStartDemonChallenge(int touchCount, Action completed)
        {
            if (_waves == null || _waves.IsEncounterActive || _currentRoom < 0 || _currentRoom >= _rooms.Count ||
                !_rooms[_currentRoom].Cleared || _roomCombatLocked) return false;
            int normal = 1;
            int elite = 0;
            int boss = 0;
            if (touchCount >= 36)
            {
                normal = 0;
                boss = 2;
            }
            else if (touchCount % 15 == 0)
            {
                normal = 1;
                boss = 1;
            }
            else if (touchCount % 5 == 0)
            {
                normal = 1;
                elite = 1;
            }
            else if (touchCount % 3 == 0)
            {
                normal = UnityEngine.Random.Range(1, 5);
            }

            SetDoorsLocked(true);
            _roomCombatLocked = true;
            _environmentItems?.SetCurrentRoomCleared(false);
            Vector2 center = GetRoomCenter(_rooms[_currentRoom].Cell);
            bool started = _waves.BeginStatueEncounter(_floor, center, roomSize, normal, elite, boss, () =>
            {
                SetDoorsLocked(false);
                _roomCombatLocked = false;
                _environmentItems?.SetCurrentRoomCleared(true);
                completed?.Invoke();
            });
            if (!started) { SetDoorsLocked(false); _roomCombatLocked = false; }
            return started;
        }

        public void TryTravel(Vector2Int direction)
        {
            if (_transitioning || _roomCombatLocked || !_rooms[_currentRoom].Cleared ||
                (_waves != null && _waves.IsEncounterActive)) return;
            if (_playerController == null ||
                Vector2.Dot(_playerController.MoveDirection, direction) < doorEnterInputThreshold) return;
            Vector2Int targetCell = _rooms[_currentRoom].Cell + direction;
            if (_roomByCell.TryGetValue(targetCell, out int target)) EnterRoom(target, direction, false);
        }

        private void RebuildRoomCollisions(RoomState room)
        {
            if (_roomCollisionRoot != null) Destroy(_roomCollisionRoot.gameObject);
            GameObject root = new("ActiveRoomCollisions");
            _roomCollisionRoot = root.transform;
            Vector2 center = GetRoomCenter(room.Cell);
            foreach (Vector2Int direction in Directions)
            {
                bool connected = _roomByCell.ContainsKey(room.Cell + direction);
                BuildSide(center, direction, connected);
            }
            SetDoorsLocked(!room.Cleared);
        }

        private void BuildSide(Vector2 center, Vector2Int direction, bool connected)
        {
            bool horizontal = direction.y != 0;
            float length = horizontal ? roomSize.x : roomSize.y;
            Vector2 normal = direction;
            Vector2 tangent = horizontal ? Vector2.right : Vector2.up;
            Vector2 edge = center + new Vector2(direction.x * roomSize.x * 0.5f, direction.y * roomSize.y * 0.5f);
            if (!connected)
            {
                CreateWall(edge, horizontal ? new Vector2(length + wallThickness, wallThickness)
                    : new Vector2(wallThickness, length + wallThickness), false);
                return;
            }

            float segmentLength = Mathf.Max(0.5f, (length - doorWidth) * 0.5f);
            float tangentOffset = doorWidth * 0.5f + segmentLength * 0.5f;
            Vector2 segmentSize = horizontal ? new Vector2(segmentLength, wallThickness)
                : new Vector2(wallThickness, segmentLength);
            CreateWall(edge + tangent * tangentOffset, segmentSize, false);
            CreateWall(edge - tangent * tangentOffset, segmentSize, false);
            CreateWall(edge, horizontal ? new Vector2(doorWidth, wallThickness)
                : new Vector2(wallThickness, doorWidth), true);

            GameObject trigger = new($"Door_{direction.x}_{direction.y}", typeof(BoxCollider2D), typeof(RoomDoorTrigger));
            trigger.transform.SetParent(_roomCollisionRoot, false);
            trigger.transform.position = edge - normal * walkableInset;
            BoxCollider2D collider = trigger.GetComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = horizontal ? new Vector2(doorWidth, doorTriggerDepth) : new Vector2(doorTriggerDepth, doorWidth);
            trigger.GetComponent<RoomDoorTrigger>().Initialize(this, direction);
        }

        private void CreateWall(Vector2 position, Vector2 size, bool doorBlocker)
        {
            GameObject wall = new(doorBlocker ? "DoorBlocker" : "Wall", typeof(BoxCollider2D));
            wall.transform.SetParent(_roomCollisionRoot, false);
            wall.transform.position = position;
            wall.tag = "Wall";
            wall.GetComponent<BoxCollider2D>().size = size;
        }

        private void SetDoorsLocked(bool locked)
        {
            if (_currentRoom >= 0 && _currentRoom < _rooms.Count)
                SetRoomDoorsVisual(_rooms[_currentRoom], locked);
            if (_roomCollisionRoot == null) return;
            for (int i = 0; i < _roomCollisionRoot.childCount; i++)
            {
                Transform child = _roomCollisionRoot.GetChild(i);
                if (child.name == "DoorBlocker") child.gameObject.SetActive(locked);
            }
        }

        private void CreateFloorExit(Vector2 center)
        {
            if (_floorExit != null) return;
            if (_currentRoom >= 0 && _currentRoom < _rooms.Count)
                _rooms[_currentRoom].HasFloorExit = true;
            _floorExit = new GameObject("NextFloorEntrance", typeof(SpriteRenderer), typeof(CircleCollider2D), typeof(FloorExitTrigger));
            _floorExit.transform.position = center + Vector2.up * 2f;
            SpriteRenderer renderer = _floorExit.GetComponent<SpriteRenderer>();
            renderer.sprite = GroundShadowSpriteFactory.CreatePortalSprite();
            renderer.color = new Color(0.2f, 0.9f, 0.85f, 1f);
            renderer.sortingOrder = 8;
            CircleCollider2D collider = _floorExit.GetComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = 0.75f;
            _floorExit.GetComponent<FloorExitTrigger>().Initialize(this);
            GroundShadow.Ensure(_floorExit).BeginLanding(0.45f);
        }

        public void EnterNextFloor()
        {
            if (_transitioning || _floorExit == null || _floor >= FinalFloor) return;
            _transitioning = true;
            SaveGameService.SaveCompletedWave(_floor);
            _floor++;
            BuildFloor();
            _transitioning = false;
        }

        private void CompleteRun()
        {
            if (_transitioning) return;
            _transitioning = true;
            CompletedRunData history = SaveGameService.CompleteActiveRun();
            GameManager.Instance.CompleteRun(history);
        }

        private Vector2 GetRoomCenter(Vector2Int cell)
        {
            return _floorOrigin + new Vector2(cell.x * roomSize.x, cell.y * roomSize.y);
        }

    }

    public sealed class RoomDoorTrigger : MonoBehaviour
    {
        private FloorMapManager _owner;
        private Vector2Int _direction;
        public void Initialize(FloorMapManager owner, Vector2Int direction)
        {
            _owner = owner;
            _direction = direction;
        }
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Player")) _owner.TryTravel(_direction);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (other.CompareTag("Player")) _owner.TryTravel(_direction);
        }
    }

    public sealed class FloorExitTrigger : MonoBehaviour
    {
        private FloorMapManager _owner;
        public void Initialize(FloorMapManager owner) => _owner = owner;
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Player")) _owner.EnterNextFloor();
        }
    }

    internal static class GroundShadowSpriteFactory
    {
        private static Sprite _portal;
        public static Sprite CreatePortalSprite()
        {
            if (_portal != null) return _portal;
            Texture2D texture = new(32, 32, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[32 * 32];
            for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), new Vector2(15.5f, 15.5f));
                pixels[x + y * 32] = new Color(1f, 1f, 1f, distance < 13f && distance > 7f ? 1f : 0f);
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            _portal = Sprite.Create(texture, new Rect(0f, 0f, 32f, 32f), new Vector2(0.5f, 0.5f), 16f);
            _portal.name = "NextFloorEntrance";
            return _portal;
        }
    }
}
