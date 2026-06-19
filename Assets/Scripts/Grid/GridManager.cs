// ============================================================================
// GridManager.cs — Builds and manages the 3D grid from a MapData asset
// Singleton accessible via GridManager.Instance from anywhere.
// If no MapData is assigned, generates a default test map at runtime.
// ============================================================================

using UnityEngine;
using System.Collections.Generic;

public class GridManager : MonoBehaviour
{
    public static GridManager Instance { get; private set; }

    [Header("Map Configuration")]
    [SerializeField] private MapData currentMap;

    [Header("Tile Prefab (optional — auto-generates cubes if not set)")]
    [SerializeField] private GameObject tilePrefab;

    [Header("Grid Appearance")]
    [SerializeField] private float tileSpacing = 1.0f;
    [SerializeField] private float tileHeight  = 0.2f;

    // ── Internal State ───────────────────────────────────────────────────
    private GridTile[,] grid;
    private readonly List<GridTile> collectibleTiles = new List<GridTile>();
    private bool isBuilt = false;

    // ── Public Accessors ─────────────────────────────────────────────────
    public MapData CurrentMap => currentMap;
    public bool IsBuilt => isBuilt;
    public int Width => currentMap != null ? currentMap.width : 0;
    public int Height => currentMap != null ? currentMap.height : 0;

    // ── Lifecycle ────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // Start removed to prevent auto-building during Start Menu phase

    // ── Grid Construction ────────────────────────────────────────────────

    /// <summary>
    /// Construct the 3D grid from a MapData asset.
    /// If mapData is null, uses the currently assigned one.
    /// If none assigned, generates a default test map.
    /// </summary>
    public void BuildGrid(MapData mapData = null)
    {
        if (mapData != null) currentMap = mapData;

        // Fallback: generate a default test map if nothing is assigned
        if (currentMap == null)
        {
            currentMap = GenerateDefaultTestMap();
            Debug.Log("[GridManager] No MapData assigned — using default test map.");
        }

        ClearGrid();

        grid = new GridTile[currentMap.width, currentMap.height];
        collectibleTiles.Clear();

        for (int z = 0; z < currentMap.height; z++)
        {
            for (int x = 0; x < currentMap.width; x++)
            {
                TileType type = currentMap.GetTileType(x, z);
                CreateTile(type, x, z);
            }
        }

        isBuilt = true;
        Debug.Log($"[GridManager] Built grid '{currentMap.mapName}' " +
                  $"({currentMap.width}×{currentMap.height}), " +
                  $"{collectibleTiles.Count} collectibles.");

        // Setup the 3D Orthographic Quarter-View Camera automatically
        if (Camera.main != null)
        {
            // Center the camera over the grid, pushed back and tilted down
            float centerX = (currentMap.width - 1) * 0.5f;
            Camera.main.transform.position = new Vector3(4.5f, 7.5f, -4.5f);
            Camera.main.transform.rotation = Quaternion.Euler(45f, 0f, 0f);
            
            // Set to Orthographic (no perspective distortion)
            Camera.main.orthographic = true;
            Camera.main.orthographicSize = Mathf.Max(currentMap.width, currentMap.height) * 0.75f;
            
            // Change background color to a nice dark blue/gray
            Camera.main.backgroundColor = new Color(0.15f, 0.15f, 0.2f);
            Camera.main.clearFlags = CameraClearFlags.SolidColor;
        }
    }

    private void CreateTile(TileType type, int x, int z)
    {
        // Create the tile GameObject
        GameObject tileObj;

        if (tilePrefab != null)
        {
            tileObj = Instantiate(tilePrefab, transform);
        }
        else
        {
            // Auto-generate a cube tile
            tileObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tileObj.transform.SetParent(transform);
        }

        // Position and scale
        Vector3 position = new Vector3(x * tileSpacing, 0f, z * tileSpacing);
        tileObj.transform.position = position;
        tileObj.transform.localScale = new Vector3(
            tileSpacing * 0.95f,
            tileHeight,
            tileSpacing * 0.95f
        );
        tileObj.name = $"Tile_{x}_{z}";

        // Add or get GridTile component
        GridTile tile = tileObj.GetComponent<GridTile>();
        if (tile == null) tile = tileObj.AddComponent<GridTile>();

        tile.Initialize(type, x, z);

        // Store in grid array
        grid[x, z] = tile;

        if (type == TileType.Collectible)
            collectibleTiles.Add(tile);
    }

    // ── Queries ──────────────────────────────────────────────────────────

    /// <summary>Get the GridTile at (x, z). Returns null if out of bounds.</summary>
    public GridTile GetTileAt(int x, int z)
    {
        if (!isBuilt || x < 0 || x >= currentMap.width || z < 0 || z >= currentMap.height)
            return null;
        return grid[x, z];
    }

    /// <summary>Check if the tile at (x, z) is passable (not a wall, not out of bounds).</summary>
    public bool IsPassable(int x, int z)
    {
        GridTile tile = GetTileAt(x, z);
        if (tile == null) return false;
        return tile.TileType != TileType.Wall;
    }



    public Vector2Int GetRobotStartPosition()
    {
        return currentMap != null ? currentMap.robotStartPosition : Vector2Int.zero;
    }

    public FacingDirection GetRobotStartFacing()
    {
        return currentMap != null ? currentMap.startFacing : FacingDirection.North;
    }

    public Vector2Int GetGoalPosition()
    {
        return currentMap != null ? currentMap.goalPosition : Vector2Int.zero;
    }

    public int GetCollectibleCount() => collectibleTiles.Count;



    /// <summary>Convert grid coordinates to world position (for robot placement).</summary>
    public Vector3 GridToWorldPosition(int x, int z, float yOffset = 0f)
    {
        return new Vector3(x * tileSpacing, yOffset, z * tileSpacing);
    }

    // ── Reset ────────────────────────────────────────────────────────────

    /// <summary>Reset all tiles to their original state (un-collect items).</summary>
    public void ResetAllTiles()
    {
        if (grid == null) return;
        foreach (var tile in grid)
        {
            if (tile != null) tile.ResetTile();
        }
    }

    // ── Cleanup ──────────────────────────────────────────────────────────

    private void ClearGrid()
    {
        isBuilt = false;

        if (grid != null)
        {
            foreach (var tile in grid)
            {
                if (tile != null) Destroy(tile.gameObject);
            }
        }

        // Clear any orphaned children
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Destroy(transform.GetChild(i).gameObject);
        }
    }

    // ── Default Test Map ─────────────────────────────────────────────────

    /// <summary>
    /// Generates a simple 5×5 test map at runtime (no .asset file needed).
    /// Layout:
    ///   Row 4: [ ][G][ ][ ][ ]
    ///   Row 3: [ ][ ][ ][W][ ]
    ///   Row 2: [ ][C][ ][W][ ]
    ///   Row 1: [ ][ ][ ][ ][ ]
    ///   Row 0: [S][ ][C][ ][ ]
    ///
    /// S=Start, G=Goal, C=Collectible, W=Wall
    /// </summary>
    private MapData GenerateDefaultTestMap()
    {
        MapData map = ScriptableObject.CreateInstance<MapData>();
        map.mapName = "Default Test Map";
        map.width = 5;
        map.height = 5;
        map.robotStartPosition = new Vector2Int(0, 0);
        map.goalPosition = new Vector2Int(1, 4);
        map.startFacing = FacingDirection.North;

        // All passable by default
        map.tileData = new int[25];

        // Walls at (3,2) and (3,3)
        map.tileData[2 * 5 + 3] = (int)TileType.Wall;  // (3, 2)
        map.tileData[3 * 5 + 3] = (int)TileType.Wall;  // (3, 3)

        // Collectibles at (2,0) and (1,2)
        map.tileData[0 * 5 + 2] = (int)TileType.Collectible;  // (2, 0)
        map.tileData[2 * 5 + 1] = (int)TileType.Collectible;  // (1, 2)

        // Goal at (1,4)
        map.tileData[4 * 5 + 1] = (int)TileType.Goal;  // (1, 4)

        return map;
    }
}
