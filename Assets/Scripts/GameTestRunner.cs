// ============================================================================
// GameTestRunner.cs — Offline test scene to verify Day 1–2 mechanics
// Attach this to an empty GameObject in the scene. Press Play and watch
// the robot execute a hardcoded command sequence on the auto-generated grid.
//
// This script is TEMPORARY — it will be replaced by GameManager once
// networking is integrated on Day 3–5.
// ============================================================================

using UnityEngine;
using System.Collections.Generic;

public class GameTestRunner : MonoBehaviour
{
    [Header("Test Controls")]
    [Tooltip("If true, auto-runs the test sequence on Start")]
    [SerializeField] private bool autoRunOnStart = true;

    [Header("Robot Setup")]
    [SerializeField] private GameObject robotPrefab;

    // Runtime references
    private RobotController robot;
    private CommandQueue commandQueue;
    private bool testRunning = false;

    void Start()
    {
        // 1. Build the grid (GridManager generates a default test map if none assigned)
        if (GridManager.Instance == null)
        {
            Debug.LogError("[GameTestRunner] No GridManager found in scene! " +
                           "Add an empty GameObject with the GridManager component.");
            return;
        }

        if (!GridManager.Instance.IsBuilt)
        {
            GridManager.Instance.BuildGrid();
        }

        // 2. Spawn the robot at the map's start position
        SpawnRobot();

        // 3. Set up the camera to look at the grid
        SetupCamera();

        // 4. Auto-run test if enabled
        if (autoRunOnStart)
        {
            Invoke(nameof(RunTestSequence), 1.0f); // 1 second delay so you can see the grid
        }
    }

    void Update()
    {
        // Manual test controls
        if (!testRunning)
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                RunTestSequence();
            }
            if (Input.GetKeyDown(KeyCode.R))
            {
                ResetTest();
            }
        }
    }

    private void SpawnRobot()
    {
        Vector2Int startPos = GridManager.Instance.GetRobotStartPosition();
        FacingDirection startFacing = GridManager.Instance.GetRobotStartFacing();

        GameObject matthewPrefab = Resources.Load<GameObject>("MatthewModel");
        GameObject robotObj;
        
        if (matthewPrefab != null)
        {
            robotObj = Instantiate(matthewPrefab);
        }
        else if (robotPrefab != null)
        {
            robotObj = Instantiate(robotPrefab);
        }
        else
        {
            // Create a simple capsule as fallback
            robotObj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            robotObj.transform.localScale = new Vector3(0.4f, 0.3f, 0.4f);
        }

        robotObj.name = "TestRobot";

        // Add required components
        robot = robotObj.GetComponent<RobotController>();
        if (robot == null) robot = robotObj.AddComponent<RobotController>();

        // Subscribe to events
        robot.OnExecutionComplete += OnRobotFinished;
        robot.OnExecutionFailed += OnRobotFailed;
        robot.OnCommandExecuted += OnCommandStep;
        robot.OnItemCollected += OnItemPickedUp;

        // Place on grid
        robot.InitializePosition(startPos.x, startPos.y, startFacing, 0, 0);

        Debug.Log($"[GameTestRunner] Robot spawned at ({startPos.x}, {startPos.y}) " +
                  $"facing {startFacing}");
    }

    private void SetupCamera()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        // 3D Orthographic Quarter-View
        cam.orthographic = true;

        int w = GridManager.Instance.Width;
        int h = GridManager.Instance.Height;

        // Tilted down 45 degrees, looking north
        cam.transform.position = new Vector3(4.5f, 7.5f, -4.5f);
        cam.transform.rotation = Quaternion.Euler(45f, 0f, 0f);

        // Adjust ortho size to fit the grid with some padding
        cam.orthographicSize = Mathf.Max(w, h) * 0.65f;
    }

    /// <summary>
    /// Run a test command sequence that navigates the default test map:
    /// Start at (0,0) facing North.
    /// Goal at (1,4) with collectibles at (2,0) and (1,2).
    ///
    /// Sequence: Right, Forward, Forward, PickUp, Left, Forward, Forward,
    ///           PickUp, Forward, Forward → should reach (1,4) with all items.
    /// </summary>
    private void RunTestSequence()
    {
        if (testRunning) return;
        testRunning = true;

        commandQueue = new CommandQueue();

        // Navigate: (0,0)N → turn right → face East
        commandQueue.Add(CommandType.TurnRight);
        // (0,0)E → forward → (1,0)E
        commandQueue.Add(CommandType.MoveForward);
        // (1,0)E → forward → (2,0)E
        commandQueue.Add(CommandType.MoveForward);
        // (2,0)E → pick up collectible
        commandQueue.Add(CommandType.PickUp);
        // Turn left → face North
        commandQueue.Add(CommandType.TurnLeft);
        // (2,0)N → forward → (2,1)N
        commandQueue.Add(CommandType.MoveForward);
        // (2,1)N → forward → (2,2)N
        commandQueue.Add(CommandType.MoveForward);
        // Turn left → face West
        commandQueue.Add(CommandType.TurnLeft);
        // (2,2)W → forward → (1,2)W
        commandQueue.Add(CommandType.MoveForward);
        // (1,2)W → pick up collectible
        commandQueue.Add(CommandType.PickUp);

        Debug.Log("[GameTestRunner] Executing test sequence: " +
                  commandQueue.ToCommaSeparatedString());

        robot.ExecuteCommands(commandQueue.ToList());
    }

    private void ResetTest()
    {
        testRunning = false;
        robot.ResetToStart();
        GridManager.Instance.ResetAllTiles();
        Debug.Log("[GameTestRunner] Reset complete. Press SPACE to run again.");
    }

    // ── Event Handlers ───────────────────────────────────────────────────

    private void OnRobotFinished()
    {
        testRunning = false;

        bool onGoal = robot.IsOnGoalTile();
        bool allCollected = robot.HasCollectedAllItems();

        if (onGoal && allCollected)
        {
            Debug.Log("<color=green>[GameTestRunner] ✅ SUCCESS! " +
                      "Robot reached the goal with all items collected!</color>");
        }
        else if (!allCollected)
        {
            Debug.Log($"<color=yellow>[GameTestRunner] ⚠️ Robot finished but " +
                      $"missed {GridManager.Instance.GetCollectibleCount() - robot.CollectiblesGathered} " +
                      $"collectible(s). LOSS!</color>");
        }
        else
        {
            Debug.Log($"<color=yellow>[GameTestRunner] ⚠️ Robot finished at " +
                      $"({robot.GridX}, {robot.GridZ}) but not on the goal tile.</color>");
        }

        Debug.Log("[GameTestRunner] Press R to reset, SPACE to run again.");
    }

    private void OnRobotFailed(string message)
    {
        testRunning = false;
        Debug.Log($"<color=red>[GameTestRunner] ❌ FAILED: {message}</color>");
        Debug.Log("[GameTestRunner] Press R to reset, SPACE to run again.");
    }

    private void OnCommandStep(int index)
    {
        Debug.Log($"[GameTestRunner] Command #{index + 1} executed. " +
                  $"Robot at ({robot.GridX}, {robot.GridZ}) facing {robot.Facing}");
    }

    private void OnItemPickedUp(GridTile tile)
    {
        Debug.Log($"<color=cyan>[GameTestRunner] 📦 Picked up item at " +
                  $"({tile.GridX}, {tile.GridZ})! " +
                  $"({robot.CollectiblesGathered}/{GridManager.Instance.GetCollectibleCount()})</color>");
    }

    void OnGUI()
    {
        // Simple on-screen instructions
        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            fontStyle = FontStyle.Bold
        };
        style.normal.textColor = Color.white;

        float y = 10;
        GUI.Label(new Rect(10, y, 400, 30), "🤖 Robot Rescue — Test Mode", style);
        y += 25;

        style.fontSize = 14;
        style.fontStyle = FontStyle.Normal;

        if (testRunning)
        {
            GUI.Label(new Rect(10, y, 400, 25), "⏳ Executing commands...", style);
        }
        else
        {
            GUI.Label(new Rect(10, y, 400, 25), "SPACE = Run Test  |  R = Reset", style);
        }

        y += 20;
        if (robot != null)
        {
            GUI.Label(new Rect(10, y, 400, 25),
                $"Position: ({robot.GridX}, {robot.GridZ})  Facing: {robot.Facing}", style);
            y += 20;

            int collected = robot.CollectiblesGathered;
            int total = GridManager.Instance.GetCollectibleCount();
            GUI.Label(new Rect(10, y, 400, 25),
                $"Collected: {collected}/{total}", style);
        }
    }
}
