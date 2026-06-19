// ============================================================================
// RobotController.cs — Deterministic command execution on the grid
// Holds the robot's grid position + facing direction.
// Executes a command list step-by-step with visual animation.
// This will be converted to a NetworkBehaviour on Day 5.
// ============================================================================

using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

public class RobotController : MonoBehaviour
{
    [Header("Grid Position")]
    [SerializeField] private int gridX;
    [SerializeField] private int gridZ;
    [SerializeField] private FacingDirection facing = FacingDirection.North;

    [Header("References")]
    [SerializeField] private RobotAnimator animator;

    [Header("Robot Appearance")]
    [SerializeField] private float robotYOffset = 0.3f;  // sit on top of tiles
    [SerializeField] private Color robotColor = new Color(0.2f, 0.7f, 1f); // Bright blue

    // ── Events ───────────────────────────────────────────────────────────
    /// <summary>Fired when all commands executed successfully.</summary>
    public event Action OnExecutionComplete;

    /// <summary>Fired when execution fails (wall collision). Message describes what happened.</summary>
    public event Action<string> OnExecutionFailed;

    /// <summary>Fired after each individual command executes. Param = command index (0-based).</summary>
    public event Action<int> OnCommandExecuted;

    /// <summary>Fired when the robot picks up an item.</summary>
    public event Action<GridTile> OnItemCollected;

    // ── State ────────────────────────────────────────────────────────────
    private bool isExecuting = false;
    private int startGridX, startGridZ;
    private FacingDirection startFacing;
    private readonly List<GridTile> collectedThisRun = new List<GridTile>();
    private int myTeamId;
    private int localTeamId;

    public int CollectiblesGathered { get; private set; }

    // ── Public Accessors ─────────────────────────────────────────────────
    public bool IsExecuting => isExecuting;
    public int GridX => gridX;
    public int GridZ => gridZ;
    public FacingDirection Facing => facing;

    // ── Lifecycle ────────────────────────────────────────────────────────

    void Awake()
    {
        if (animator == null)
            animator = GetComponent<RobotAnimator>();
        if (animator == null)
            animator = gameObject.AddComponent<RobotAnimator>();
    }

    void Start()
    {
        // Apply robot color
        var rend = GetComponent<Renderer>();
        if (rend != null)
        {
            rend.material = new Material(rend.material);
            rend.material.color = robotColor;
        }
    }

    // ── Initialization ───────────────────────────────────────────────────

    /// <summary>
    /// Place the robot at a grid position. Call this once when the round starts.
    /// </summary>
    public void InitializePosition(int x, int z, FacingDirection dir, int targetTeamId, int localTeamId)
    {
        gridX = x;
        gridZ = z;
        facing = dir;
        this.myTeamId = targetTeamId;
        this.localTeamId = localTeamId;

        // Save for reset
        startGridX = x;
        startGridZ = z;
        startFacing = dir;

        // Snap to world position immediately
        Vector3 worldPos = GetWorldPosition(x, z);
        animator.SnapToPosition(worldPos);
        animator.SnapToRotation(FacingToRotation(dir));
    }

    // ── Command Execution ────────────────────────────────────────────────

    /// <summary>
    /// Execute a list of commands sequentially with animation.
    /// This is the core game mechanic: deterministic tile-based movement.
    /// </summary>
    public void ExecuteCommands(List<CommandType> commands)
    {
        if (isExecuting)
        {
            Debug.LogWarning("[RobotController] Already executing!");
            return;
        }
        StartCoroutine(ExecuteCommandsCoroutine(commands));
    }

    private IEnumerator ExecuteCommandsCoroutine(List<CommandType> commands)
    {
        isExecuting = true;
        collectedThisRun.Clear();
        CollectiblesGathered = 0;

        for (int i = 0; i < commands.Count; i++)
        {
            bool animDone = false;
            CommandType cmd = commands[i];

            switch (cmd)
            {
                // ─── MOVE FORWARD ────────────────────────────────────
                case CommandType.MoveForward:
                    Vector2Int nextPos = GetForwardPosition();

                    if (GridManager.Instance.IsPassable(nextPos.x, nextPos.y))
                    {
                        // Valid move
                        gridX = nextPos.x;
                        gridZ = nextPos.y;
                        Vector3 targetWorld = GetWorldPosition(gridX, gridZ);

                        animator.AnimateMoveTo(targetWorld, () => animDone = true);
                        yield return new WaitUntil(() => animDone);
                    }
                    else
                    {
                        // WALL COLLISION — execution fails here
                        Vector3 wallDir = FacingToVector(facing);
                        animator.AnimateWallBonk(wallDir, () => animDone = true);
                        yield return new WaitUntil(() => animDone);

                        isExecuting = false;
                        OnExecutionFailed?.Invoke(
                            $"💥 Wall collision at command #{i + 1} (MoveForward)!");
                        yield break;
                    }
                    break;

                // ─── TURN LEFT ───────────────────────────────────────
                case CommandType.TurnLeft:
                    facing = RotateLeft(facing);
                    animator.AnimateRotateTo(FacingToRotation(facing), () => animDone = true);
                    yield return new WaitUntil(() => animDone);
                    break;

                // ─── TURN RIGHT ──────────────────────────────────────
                case CommandType.TurnRight:
                    facing = RotateRight(facing);
                    animator.AnimateRotateTo(FacingToRotation(facing), () => animDone = true);
                    yield return new WaitUntil(() => animDone);
                    break;

                // ─── PICK UP ─────────────────────────────────────────
                case CommandType.PickUp:
                    GridTile currentTile = GridManager.Instance.GetTileAt(gridX, gridZ);
                    bool pickedUp = false;

                    if (currentTile != null &&
                        currentTile.TileType == TileType.Collectible &&
                        !currentTile.IsCollectedBy(myTeamId))
                    {
                        currentTile.Collect(myTeamId, localTeamId);
                        collectedThisRun.Add(currentTile);
                        CollectiblesGathered++;
                        pickedUp = true;
                    }

                    // Always play animation (even if nothing to pick up — wasted command)
                    animator.AnimatePickUp(() => animDone = true);
                    yield return new WaitUntil(() => animDone);

                    if (pickedUp)
                        OnItemCollected?.Invoke(currentTile);
                    break;
            }

            // Notify listeners which command just finished
            OnCommandExecuted?.Invoke(i);

            // Brief pause between commands so kids can follow
            yield return new WaitForSeconds(0.2f);
        }

        isExecuting = false;
        OnExecutionComplete?.Invoke();
    }

    // ── Reset ────────────────────────────────────────────────────────────

    /// <summary>
    /// Reset robot to starting position and un-collect all items gathered this run.
    /// </summary>
    public void ResetToStart()
    {
        StopAllCoroutines();
        isExecuting = false;

        gridX = startGridX;
        gridZ = startGridZ;
        facing = startFacing;

        animator.SnapToPosition(GetWorldPosition(gridX, gridZ));
        animator.SnapToRotation(FacingToRotation(facing));

        // Restore tiles collected during this run
        foreach (var tile in collectedThisRun)
        {
            if (tile != null) tile.ResetTile();
        }
        collectedThisRun.Clear();
    }

    // ── Win-Condition Helpers ────────────────────────────────────────────

    /// <summary>Is the robot currently standing on the Goal tile?</summary>
    public bool IsOnGoalTile()
    {
        GridTile tile = GridManager.Instance.GetTileAt(gridX, gridZ);
        return tile != null && tile.TileType == TileType.Goal;
    }

    /// <summary>Have all collectibles on the map been picked up by this robot?</summary>
    public bool HasCollectedAllItems()
    {
        return CollectiblesGathered >= GridManager.Instance.GetCollectibleCount();
    }

    // ── Helpers (pure math, deterministic) ───────────────────────────────

    private Vector2Int GetForwardPosition()
    {
        switch (facing)
        {
            case FacingDirection.North: return new Vector2Int(gridX, gridZ + 1);
            case FacingDirection.East:  return new Vector2Int(gridX + 1, gridZ);
            case FacingDirection.South: return new Vector2Int(gridX, gridZ - 1);
            case FacingDirection.West:  return new Vector2Int(gridX - 1, gridZ);
            default: return new Vector2Int(gridX, gridZ);
        }
    }

    private Vector3 GetWorldPosition(int x, int z)
    {
        return GridManager.Instance.GridToWorldPosition(x, z, robotYOffset);
    }

    private static Quaternion FacingToRotation(FacingDirection dir)
    {
        switch (dir)
        {
            case FacingDirection.North: return Quaternion.Euler(0f, 0f, 0f);
            case FacingDirection.East:  return Quaternion.Euler(0f, 90f, 0f);
            case FacingDirection.South: return Quaternion.Euler(0f, 180f, 0f);
            case FacingDirection.West:  return Quaternion.Euler(0f, 270f, 0f);
            default: return Quaternion.identity;
        }
    }

    private static Vector3 FacingToVector(FacingDirection dir)
    {
        switch (dir)
        {
            case FacingDirection.North: return Vector3.forward;
            case FacingDirection.East:  return Vector3.right;
            case FacingDirection.South: return Vector3.back;
            case FacingDirection.West:  return Vector3.left;
            default: return Vector3.zero;
        }
    }

    private static FacingDirection RotateLeft(FacingDirection dir)
    {
        switch (dir)
        {
            case FacingDirection.North: return FacingDirection.West;
            case FacingDirection.West:  return FacingDirection.South;
            case FacingDirection.South: return FacingDirection.East;
            case FacingDirection.East:  return FacingDirection.North;
            default: return dir;
        }
    }

    private static FacingDirection RotateRight(FacingDirection dir)
    {
        switch (dir)
        {
            case FacingDirection.North: return FacingDirection.East;
            case FacingDirection.East:  return FacingDirection.South;
            case FacingDirection.South: return FacingDirection.West;
            case FacingDirection.West:  return FacingDirection.North;
            default: return dir;
        }
    }
}
