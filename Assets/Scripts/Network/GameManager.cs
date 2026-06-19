// ============================================================================
// GameManager.cs — Server-authoritative game state machine
// Controls the full game loop: Lobby → Planning → Executing → Result
// Handles command submission, validation, execution triggering, and win/loss.
//
// Lives on a NetworkObject in the scene (same GameObject as LobbyManager
// or on a dedicated GameManager object).
// ============================================================================

using Unity.Netcode;
using Unity.Collections;
using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Map Configuration")]
    [SerializeField] private MapData mapToPlay;

    [Header("Game Settings")]
    [SerializeField] private int maxCommandsPerTurn = 10;

    // ── Network State ────────────────────────────────────────────────────

    /// <summary>Current phase, synced to all clients.</summary>
    public NetworkVariable<GamePhase> CurrentPhase = new NetworkVariable<GamePhase>(
        GamePhase.StartMenu,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    /// <summary>Which team won the round (-1 = none yet, 0 = team 0, 1 = team 1).</summary>
    public NetworkVariable<int> WinningTeam = new NetworkVariable<int>(
        -1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    /// <summary>Round number, incremented on each new round.</summary>
    public NetworkVariable<int> RoundNumber = new NetworkVariable<int>(
        1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // ── Events ───────────────────────────────────────────────────────────

    /// <summary>Fired on ALL clients when the game phase changes.</summary>
    public event Action<GamePhase> OnPhaseChanged;

    /// <summary>Fired on ALL clients when a team's commands start executing.</summary>
    public event Action<int, List<CommandType>> OnTeamExecuting;

    /// <summary>Fired on ALL clients when execution completes for a team.</summary>
    public event Action<int, bool> OnTeamExecutionDone;  // teamId, succeeded

    /// <summary>Fired on ALL clients with the round result.</summary>
    public event Action<int, string, string> OnRoundResult;  // winning team ID, reason 0, reason 1

    // ── Internal Server State ────────────────────────────────────────────
    private Dictionary<int, string> submittedCommands = new Dictionary<int, string>();
    private struct ExecutionResultData
    {
        public bool ReachedGoal;
        public int CollectedCount;
        public bool HitWall;
    }
    private Dictionary<int, ExecutionResultData> executionResults = new Dictionary<int, ExecutionResultData>();
    private Dictionary<int, string> executionReasons = new Dictionary<int, string>();
    private Dictionary<int, RobotController> teamRobots = new Dictionary<int, RobotController>();
    private int teamsFinishedExecuting = 0;
    private HashSet<ulong> playersReadyForNextRound = new HashSet<ulong>();

    // ── Lifecycle ────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;

        // Auto-bootstrap VirtualSceneManager
        if (FindObjectOfType<VirtualSceneManager>() == null)
        {
            gameObject.AddComponent<VirtualSceneManager>();
        }
    }

    public override void OnNetworkSpawn()
    {
        // Listen for phase changes on all clients
        CurrentPhase.OnValueChanged += HandlePhaseChanged;

        if (IsServer)
        {
            // Listen for lobby ready signal
            if (LobbyManager.Instance != null)
            {
                LobbyManager.Instance.OnAllPlayersReady += OnAllPlayersReady;
            }

            Debug.Log("[GameManager] Server spawned. Waiting for players...");
        }
    }

    public override void OnNetworkDespawn()
    {
        CurrentPhase.OnValueChanged -= HandlePhaseChanged;

        if (IsServer && LobbyManager.Instance != null)
        {
            LobbyManager.Instance.OnAllPlayersReady -= OnAllPlayersReady;
        }
    }

    // ── Phase Transitions (Server Only) ──────────────────────────────────

    private void OnAllPlayersReady()
    {
        if (!IsServer) return;
        StartGame();
    }

    /// <summary>Server: Initialize the game and start the first Planning phase.</summary>
    public void StartGame()
    {
        if (!IsServer) return;

        Debug.Log("[GameManager] Starting game!");

        // Build the grid on all clients
        BuildGridClientRpc();

        // Spawn robots for each team
        SpawnRobotsClientRpc();

        // Small delay for grid to build, then start planning
        StartCoroutine(DelayedStartPlanning(0.5f));
    }

    private IEnumerator DelayedStartPlanning(float delay)
    {
        yield return new WaitForSeconds(delay);
        StartPlanningPhase();
    }

    /// <summary>Server: Transition to Planning phase.</summary>
    public void StartPlanningPhase()
    {
        if (!IsServer) return;

        submittedCommands.Clear();
        executionResults.Clear();
        executionReasons.Clear();
        teamsFinishedExecuting = 0;

        CurrentPhase.Value = GamePhase.Planning;
        Debug.Log("[GameManager] → Planning Phase");
    }

    /// <summary>Server: Transition to Executing phase.</summary>
    private void StartExecutionPhase()
    {
        if (!IsServer) return;

        CurrentPhase.Value = GamePhase.Executing;
        Debug.Log("[GameManager] → Executing Phase");

        // Execute each team's commands simultaneously
        foreach (var kvp in submittedCommands)
        {
            int teamId = kvp.Key;
            string commandString = kvp.Value;

            ExecuteCommandsClientRpc(teamId, commandString);
        }
    }

    /// <summary>Server: Show the round result.</summary>
    private void ShowRoundResult(int winnerTeamId, string team0Reason, string team1Reason)
    {
        if (!IsServer) return;

        WinningTeam.Value = winnerTeamId;
        CurrentPhase.Value = GamePhase.RoundResult;

        AnnounceResultClientRpc(winnerTeamId, team0Reason, team1Reason);
        Debug.Log($"[GameManager] → Round Result. Winner: Team {winnerTeamId}");
    }

    // ── Command Submission (Client → Server) ─────────────────────────────

    /// <summary>
    /// Client sends their command queue to the server.
    /// Commands are sent as a comma-separated string: "MoveForward,TurnLeft,MoveForward"
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void SubmitCommandsServerRpc(string commandString, ServerRpcParams rpcParams = default)
    {
        if (CurrentPhase.Value != GamePhase.Planning)
        {
            Debug.LogWarning("[GameManager] Commands rejected — not in Planning phase!");
            return;
        }

        ulong clientId = rpcParams.Receive.SenderClientId;
        int teamId = LobbyManager.Instance.GetTeamId(clientId);

        if (teamId < 0)
        {
            Debug.LogWarning($"[GameManager] Unknown team for client {clientId}");
            return;
        }

        // Parse commands
        var commands = CommandQueue.FromCommaSeparatedString(commandString);

        submittedCommands[teamId] = commandString;
        Debug.Log($"[GameManager] Team {teamId} submitted: {commandString}");

        // Notify all clients that this team has submitted
        TeamSubmittedClientRpc(teamId);

        // Check if all teams have submitted
        if (submittedCommands.Count >= LobbyManager.Instance.PlayerCount)
        {
            Debug.Log("[GameManager] All teams submitted. Starting execution...");
            StartExecutionPhase();
        }
    }

    // ── Execution Complete Reporting ─────────────────────────────────────

    /// <summary>
    /// Called by RobotController (via ClientRpc callback) when a team's
    /// robot finishes executing its commands.
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void ReportExecutionCompleteServerRpc(int teamId, bool reachedGoal,
        int collectedCount, bool hitWall, ServerRpcParams rpcParams = default)
    {
        // Only process once per team per round
        if (executionResults.ContainsKey(teamId)) return;

        executionResults[teamId] = new ExecutionResultData
        {
            ReachedGoal = reachedGoal,
            CollectedCount = collectedCount,
            HitWall = hitWall
        };
        
        string reason = "Solved the puzzle!";
        if (hitWall) reason = "Crashed into an obstacle!";
        else if (!reachedGoal) reason = "Did not stop on the Goal tile.";
        
        executionReasons[teamId] = reason;

        teamsFinishedExecuting++;

        bool success = reachedGoal && !hitWall;
        Debug.Log($"[GameManager] Team {teamId} execution done. " +
                  $"Goal={reachedGoal}, Collected={collectedCount}, " +
                  $"WallHit={hitWall}, Success={success}");

        OnTeamExecutionDone?.Invoke(teamId, success);

        // Check if all teams finished
        if (teamsFinishedExecuting >= submittedCommands.Count)
        {
            EvaluateRoundResult();
        }
    }

    /// <summary>Server: Evaluate who won the round.</summary>
    private void EvaluateRoundResult()
    {
        bool team0Valid = executionResults.ContainsKey(0) && executionResults[0].ReachedGoal && !executionResults[0].HitWall;
        bool team1Valid = executionResults.ContainsKey(1) && executionResults[1].ReachedGoal && !executionResults[1].HitWall;

        int winnerId = -1; 
        string r0 = "";
        string r1 = "";

        if (team0Valid && team1Valid)
        {
            int c0 = executionResults[0].CollectedCount;
            int c1 = executionResults[1].CollectedCount;

            if (c0 > c1) { winnerId = 0; r0 = $"Won on Collectibles ({c0} vs {c1})"; r1 = $"Lost on Collectibles ({c1} vs {c0})"; }
            else if (c1 > c0) { winnerId = 1; r0 = $"Lost on Collectibles ({c0} vs {c1})"; r1 = $"Won on Collectibles ({c1} vs {c0})"; }
            else
            {
                // Tie breaker: fewest moves
                int moves0 = CommandQueue.FromCommaSeparatedString(submittedCommands[0]).Count;
                int moves1 = CommandQueue.FromCommaSeparatedString(submittedCommands[1]).Count;

                if (moves0 < moves1) { winnerId = 0; r0 = $"Won on fewer moves ({moves0} vs {moves1})"; r1 = $"Lost on moves ({moves1} vs {moves0})"; }
                else if (moves1 < moves0) { winnerId = 1; r0 = $"Lost on moves ({moves0} vs {moves1})"; r1 = $"Won on fewer moves ({moves1} vs {moves0})"; }
                else
                {
                    winnerId = -1; // Draw
                    r0 = $"Draw! Same items ({c0}) & moves ({moves0})";
                    r1 = $"Draw! Same items ({c1}) & moves ({moves1})";
                }
            }
        }
        else if (team0Valid)
        {
            winnerId = 0;
            r0 = "Reached the Goal!";
            r1 = executionReasons.ContainsKey(1) ? executionReasons[1] : "Disconnected"; 
        }
        else if (team1Valid)
        {
            winnerId = 1;
            r0 = executionReasons.ContainsKey(0) ? executionReasons[0] : "Disconnected";
            r1 = "Reached the Goal!";
        }
        else
        {
            winnerId = -1;
            r0 = executionReasons.ContainsKey(0) ? executionReasons[0] : "Disconnected.";
            r1 = executionReasons.ContainsKey(1) ? executionReasons[1] : "Disconnected.";
        }

        ShowRoundResult(winnerId, r0, r1);
    }

    // ── Client RPCs ──────────────────────────────────────────────────────

    [ClientRpc]
    private void BuildGridClientRpc()
    {
        Debug.Log("[GameManager] Building grid...");

        if (GridManager.Instance != null)
        {
            GridManager.Instance.BuildGrid(mapToPlay);
        }
    }

    [ClientRpc]
    private void SpawnRobotsClientRpc()
    {
        Debug.Log("[GameManager] Spawning robots...");

        if (GridManager.Instance == null) return;

        Vector2Int startPos = GridManager.Instance.GetRobotStartPosition();
        FacingDirection startFacing = GridManager.Instance.GetRobotStartFacing();

        int myTeam = LobbyManager.Instance.GetTeamId(NetworkManager.Singleton.LocalClientId);

        // Spawn a robot for EVERY team
        foreach (var player in LobbyManager.Instance.GetAllPlayers())
        {
            SpawnRobotForTeam(player.TeamId, myTeam, startPos, startFacing);
        }
    }

    private void SpawnRobotForTeam(int targetTeam, int localTeam, Vector2Int startPos, FacingDirection startFacing)
    {
        // Load Matthew from Resources
        GameObject matthewPrefab = Resources.Load<GameObject>("MatthewModel");
        GameObject robotObj;
        
        if (matthewPrefab != null)
        {
            robotObj = Instantiate(matthewPrefab);
        }
        else
        {
            // Fallback if not set up yet
            robotObj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            robotObj.transform.localScale = new Vector3(0.4f, 0.3f, 0.4f);
        }

        bool isOpponent = targetTeam != localTeam;
        robotObj.name = isOpponent ? $"OpponentMatthew_{targetTeam}" : "MyMatthew";

        // Fix materials (recolor suits based on team)
        var renderers = robotObj.GetComponentsInChildren<Renderer>();
        foreach (var renderer in renderers)
        {
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            Color teamColor = targetTeam == 0 ? Color.cyan : Color.red;
            if (isOpponent) teamColor.a = 0.3f;

            // Tint existing material instead of replacing it
            Material mat = renderer.material;
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", Color.Lerp(Color.white, teamColor, 0.5f));
            else if (mat.HasProperty("_Color"))
                mat.color = Color.Lerp(Color.white, teamColor, 0.5f);
        }

        var controller = robotObj.AddComponent<RobotController>();
        controller.InitializePosition(startPos.x, startPos.y, startFacing, targetTeam, localTeam);

        // Subscribe to execution events ONLY for local robot
        if (!isOpponent)
        {
            controller.OnExecutionComplete += () => OnLocalRobotDone(localTeam, controller);
            controller.OnExecutionFailed += (msg) => OnLocalRobotFailed(localTeam, msg);
        }

        teamRobots[targetTeam] = controller;

        Debug.Log($"[GameManager] Robot spawned for team {targetTeam} at ({startPos.x}, {startPos.y})");
    }

    [ClientRpc]
    private void ExecuteCommandsClientRpc(int teamId, string commandString)
    {
        int myTeam = LobbyManager.Instance.GetTeamId(NetworkManager.Singleton.LocalClientId);

        // Execute commands on the corresponding robot
        if (teamRobots.ContainsKey(teamId))
        {
            var commands = CommandQueue.FromCommaSeparatedString(commandString);
            teamRobots[teamId].ExecuteCommands(commands);

            OnTeamExecuting?.Invoke(teamId, commands);
            Debug.Log($"[GameManager] Executing commands for team {teamId}: {commandString}");
        }
    }

    [ClientRpc]
    private void TeamSubmittedClientRpc(int teamId)
    {
        Debug.Log($"[GameManager] Team {teamId} has submitted their commands.");
    }

    [ClientRpc]
    private void AnnounceResultClientRpc(int winnerTeamId, string team0Reason, string team1Reason)
    {
        int myTeam = LobbyManager.Instance.GetTeamId(NetworkManager.Singleton.LocalClientId);

        if (winnerTeamId < 0)
        {
            Debug.Log("[GameManager] 🔄 Draw! Both teams will try again.");
        }
        else if (winnerTeamId == myTeam)
        {
            Debug.Log("[GameManager] 🎉 YOU WIN!");
        }
        else
        {
            Debug.Log("[GameManager] 😞 You lost. Try again!");
        }

        OnRoundResult?.Invoke(winnerTeamId, team0Reason, team1Reason);
    }

    // ── Local Robot Callbacks ────────────────────────────────────────────

    private void OnLocalRobotDone(int teamId, RobotController robot)
    {
        bool onGoal = robot.IsOnGoalTile();
        int collectedCount = robot.CollectiblesGathered;

        // Report to server
        ReportExecutionCompleteServerRpc(teamId, onGoal, collectedCount, false);
    }

    private void OnLocalRobotFailed(int teamId, string message)
    {
        Debug.Log($"[GameManager] Team {teamId} robot failed: {message}");

        // Report wall collision to server
        ReportExecutionCompleteServerRpc(teamId, false, 0, true);
    }

    // ── Round Management ─────────────────────────────────────────────────

    /// <summary>Server: Reset the round and start a new planning phase.</summary>
    public void ResetRound()
    {
        if (!IsServer) return;

        RoundNumber.Value++;
        WinningTeam.Value = -1;

        // Reset on all clients
        ResetRoundClientRpc();

        // Start new planning phase after a short delay
        StartCoroutine(DelayedStartPlanning(0.5f));
    }

    [ClientRpc]
    private void ResetRoundClientRpc()
    {
        int myTeam = LobbyManager.Instance.GetTeamId(NetworkManager.Singleton.LocalClientId);

        // Reset robots
        foreach (var kvp in teamRobots)
        {
            if (kvp.Value != null)
            {
                kvp.Value.ResetToStart();
            }
        }

        // Reset grid tiles
        if (GridManager.Instance != null)
            GridManager.Instance.ResetAllTiles();

        Debug.Log("[GameManager] Round reset.");
    }

    /// <summary>Client requests a reset. Round restarts when ALL clients request it.</summary>
    [ServerRpc(RequireOwnership = false)]
    public void RequestResetServerRpc(ServerRpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        playersReadyForNextRound.Add(clientId);

        Debug.Log($"[GameManager] Client {clientId} clicked Play Again ({playersReadyForNextRound.Count}/{LobbyManager.Instance.PlayerCount})");

        if (playersReadyForNextRound.Count >= LobbyManager.Instance.PlayerCount)
        {
            playersReadyForNextRound.Clear();
            ResetRound();
        }
    }

    // ── Phase Change Handler ─────────────────────────────────────────────

    private void HandlePhaseChanged(GamePhase previous, GamePhase current)
    {
        Debug.Log($"[GameManager] Phase: {previous} → {current}");
        OnPhaseChanged?.Invoke(current);
    }

}
