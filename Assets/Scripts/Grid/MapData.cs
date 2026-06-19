// ============================================================================
// MapData.cs — ScriptableObject defining a grid map layout
// Create new maps: Right-click in Project > Create > Robot Rescue > Map Data
// The tileData array is stored row-major: index = z * width + x
// ============================================================================

using UnityEngine;

[CreateAssetMenu(fileName = "NewMap", menuName = "Robot Rescue/Map Data")]
public class MapData : ScriptableObject
{
    [Header("Map Info")]
    public string mapName = "New Map";

    [Header("Dimensions")]
    [Min(2)] public int width = 5;
    [Min(2)] public int height = 5;

    [Header("Tile Layout (row-major: 0=Passable, 1=Wall, 2=Collectible, 3=Goal)")]
    [Tooltip("Length must equal width × height. Index = z * width + x")]
    public int[] tileData;

    [Header("Positions")]
    public Vector2Int robotStartPosition = new Vector2Int(0, 0);
    public Vector2Int goalPosition = new Vector2Int(4, 4);

    [Header("Robot Start Facing")]
    public FacingDirection startFacing = FacingDirection.North;

    // ── Accessors ────────────────────────────────────────────────────────

    /// <summary>
    /// Get the tile type at grid coordinates (x, z).
    /// Returns Wall for out-of-bounds coordinates.
    /// </summary>
    public TileType GetTileType(int x, int z)
    {
        if (x < 0 || x >= width || z < 0 || z >= height)
            return TileType.Wall;

        int index = z * width + x;
        if (tileData == null || index >= tileData.Length)
            return TileType.Passable;

        return (TileType)tileData[index];
    }

    /// <summary>
    /// Count how many collectible tiles exist in this map.
    /// </summary>
    public int CountCollectibles()
    {
        if (tileData == null) return 0;

        int count = 0;
        for (int i = 0; i < tileData.Length; i++)
        {
            if ((TileType)tileData[i] == TileType.Collectible)
                count++;
        }
        return count;
    }

    // ── Editor Helpers ───────────────────────────────────────────────────

    /// <summary>Initialize tile data to all-passable. Call from custom editor or test code.</summary>
    public void InitializeDefault()
    {
        tileData = new int[width * height];
        for (int i = 0; i < tileData.Length; i++)
            tileData[i] = (int)TileType.Passable;

        // Place goal at goalPosition
        int goalIndex = goalPosition.y * width + goalPosition.x;
        if (goalIndex >= 0 && goalIndex < tileData.Length)
            tileData[goalIndex] = (int)TileType.Goal;
    }

    private void OnValidate()
    {
        // Ensure tileData length matches dimensions
        if (tileData == null || tileData.Length != width * height)
        {
            Debug.LogWarning($"[MapData] tileData length ({tileData?.Length ?? 0}) " +
                             $"doesn't match {width}×{height}={width * height}. " +
                             $"Resize it in the Inspector.");
        }
    }
}
