// ============================================================================
// GridTile.cs — MonoBehaviour attached to each tile GameObject
// Stores its type, grid coordinates, and collection state.
// Handles its own visual appearance (color-coded for kids).
// ============================================================================

using UnityEngine;

public class GridTile : MonoBehaviour
{
    [Header("Tile State (set via Initialize)")]
    [SerializeField] private TileType tileType;
    [SerializeField] private int gridX;
    [SerializeField] private int gridZ;

    // Track which teams have collected this tile
    private System.Collections.Generic.HashSet<int> collectedByTeams = new System.Collections.Generic.HashSet<int>();

    // ── Kid-friendly color palette ──────────────────────────────────────
    private static readonly Color PassableColor    = new Color(0.42f, 0.82f, 0.44f);  // Bright green
    private static readonly Color WallColor        = new Color(0.55f, 0.27f, 0.07f);  // Warm brown
    private static readonly Color CollectibleColor = new Color(1.00f, 0.84f, 0.00f);  // Gold
    private static readonly Color GoalColor        = new Color(0.25f, 0.60f, 1.00f);  // Sky blue
    private static readonly Color CollectedColor   = new Color(0.60f, 0.85f, 0.60f);  // Faded green

    private Renderer tileRenderer;
    private Color originalColor;
    private Color highlightColor;
    private bool isHighlighted = false;

    // Collectible visual (child object that floats above the tile)
    private GameObject collectibleVisual;

    // ── Public Accessors ─────────────────────────────────────────────────
    public TileType TileType => tileType;
    public int GridX => gridX;
    public int GridZ => gridZ;
    public bool IsCollectedBy(int teamId) => collectedByTeams.Contains(teamId);

    // ── Initialization ───────────────────────────────────────────────────

    /// <summary>
    /// Called by GridManager after instantiation.
    /// Sets the tile type, position, and initial visuals.
    /// </summary>
    public void Initialize(TileType type, int x, int z)
    {
        tileType = type;
        gridX = x;
        gridZ = z;
        collectedByTeams.Clear();

        tileRenderer = GetComponent<Renderer>();
        if (tileRenderer == null)
            tileRenderer = GetComponentInChildren<Renderer>();

        ApplyVisuals();
    }

    private void ApplyVisuals()
    {
        // Set tile color
        switch (tileType)
        {
            case TileType.Passable:
                originalColor = PassableColor;
                break;
            case TileType.Wall:
                originalColor = WallColor;
                // Walls are taller for 3D depth
                transform.localScale = new Vector3(1f, 1.5f, 1f);
                transform.position = new Vector3(transform.position.x, 0.75f, transform.position.z);
                break;
            case TileType.Collectible:
                originalColor = PassableColor; // Base tile is green
                CreateCollectibleVisual();
                break;
            case TileType.Goal:
                originalColor = GoalColor;
                break;
        }

        if (tileRenderer != null)
        {
            tileRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            URPFixer.FixMaterial(tileRenderer, originalColor);
        }
    }

    /// <summary>
    /// Creates a small floating golden sphere above collectible tiles.
    /// </summary>
    private void CreateCollectibleVisual()
    {
        collectibleVisual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        collectibleVisual.name = "CollectibleOrb";
        collectibleVisual.transform.SetParent(transform);
        // Elevate slightly off the ground
        collectibleVisual.transform.localPosition = new Vector3(0f, 1.5f, 0f);
        collectibleVisual.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);

        // Remove collider from visual so it doesn't interfere
        var collider = collectibleVisual.GetComponent<Collider>();
        if (collider != null) Object.Destroy(collider);

        // Gold material
        var renderer = collectibleVisual.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            URPFixer.FixMaterial(renderer, CollectibleColor);
        }

        // Add a gentle spin + bob animation
        var spinner = collectibleVisual.AddComponent<CollectibleSpinner>();
        spinner.Initialize();
    }

    // ── State Changes ────────────────────────────────────────────────────

    /// <summary>
    /// Mark this collectible as picked up by a specific team.
    /// Only hides the orb visually if the local player collected it.
    /// </summary>
    public void Collect(int teamId, int localTeamId)
    {
        if (tileType != TileType.Collectible) return;

        collectedByTeams.Add(teamId);

        // Only disappear if the LOCAL player picked it up!
        if (teamId == localTeamId)
        {
            if (collectibleVisual != null) collectibleVisual.SetActive(false);

            if (tileRenderer != null)
            {
                originalColor = CollectedColor;
                UpdateMaterialColor();
            }
        }
    }

    public void SetHighlight(bool active, Color color = default)
    {
        isHighlighted = active;
        if (active) highlightColor = color;
        UpdateMaterialColor();
    }

    private void UpdateMaterialColor()
    {
        if (tileRenderer == null) return;
        
        Color baseColor = originalColor;
        // If it's a collectible and local player collected it, it's green. But that's handled by setting originalColor to CollectedColor in Collect().
        
        Color finalColor = isHighlighted ? highlightColor : baseColor;
        tileRenderer.material.color = finalColor;
    }

    /// <summary>
    /// Restore the tile to its original state (for round reset).
    /// </summary>
    public void ResetTile()
    {
        collectedByTeams.Clear();

        if (tileRenderer != null)
        {
            // If it was a collectible, its base color was PassableColor
            if (tileType == TileType.Collectible) originalColor = PassableColor;
            UpdateMaterialColor();
        }

        if (collectibleVisual != null)
            collectibleVisual.SetActive(true);
    }
}

// ============================================================================
// CollectibleSpinner — Simple animation for the floating collectible orbs.
// Spins and bobs gently to catch kids' attention.
// ============================================================================

public class CollectibleSpinner : MonoBehaviour
{
    private float bobOffset;
    private Vector3 startLocalPos;
    private float spinSpeed = 90f;     // degrees per second
    private float bobHeight = 0.1f;
    private float bobSpeed = 2f;

    public void Initialize()
    {
        startLocalPos = transform.localPosition;
        bobOffset = Random.Range(0f, Mathf.PI * 2f); // randomize so orbs don't all bob in sync
    }

    void Update()
    {
        // Spin
        transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);

        // Bob up and down
        float bob = Mathf.Sin((Time.time + bobOffset) * bobSpeed) * bobHeight;
        transform.localPosition = startLocalPos + Vector3.up * bob;
    }
}
