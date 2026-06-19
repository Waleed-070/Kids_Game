// ============================================================================
// LobbyManager.cs — Manages connected players, team assignment, and ready-up
// Lives on the same GameObject as NetworkManager.
// Tracks players via a NetworkList and signals GameManager when all are ready.
// ============================================================================

using Unity.Netcode;
using Unity.Collections;
using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// Network-serializable data for each connected player.
/// </summary>
public struct PlayerData : INetworkSerializable, IEquatable<PlayerData>
{
    public ulong ClientId;
    public int TeamId;           // 0 or 1 for 1v1
    public bool IsReady;
    public FixedString64Bytes PlayerName;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref ClientId);
        serializer.SerializeValue(ref TeamId);
        serializer.SerializeValue(ref IsReady);
        serializer.SerializeValue(ref PlayerName);
    }

    public bool Equals(PlayerData other)
    {
        return ClientId == other.ClientId &&
               TeamId == other.TeamId &&
               IsReady == other.IsReady &&
               PlayerName.Equals(other.PlayerName);
    }

    public override int GetHashCode()
    {
        return ClientId.GetHashCode();
    }
}

public class LobbyManager : NetworkBehaviour
{
    public static LobbyManager Instance { get; private set; }

    [Header("Settings")]
    [SerializeField] private int maxPlayers = 2;  // 1v1

    // ── Network State ────────────────────────────────────────────────────
    private NetworkList<PlayerData> players;

    // ── Events ───────────────────────────────────────────────────────────
    /// <summary>Fired on all clients when the player list changes.</summary>
    public event Action OnPlayersChanged;

    /// <summary>Fired on server when all players are ready.</summary>
    public event Action OnAllPlayersReady;

    /// <summary>Fired on all clients when a player connects.</summary>
    public event Action<ulong> OnPlayerJoined;

    /// <summary>Fired on all clients when a player disconnects.</summary>
    public event Action<ulong> OnPlayerLeft;

    // ── Properties ───────────────────────────────────────────────────────
    public int PlayerCount => players?.Count ?? 0;
    public int MaxPlayers => maxPlayers;
    public bool IsLobbyFull => PlayerCount >= maxPlayers;

    // ── Lifecycle ────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;

        // NetworkList must be initialized in Awake (before NetworkObject spawns)
        players = new NetworkList<PlayerData>();
    }

    public override void OnNetworkSpawn()
    {
        // Subscribe to list changes on ALL clients
        players.OnListChanged += OnPlayerListChanged;

        if (IsServer)
        {
            // Server tracks connections
            NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnected;

            // Add the host as the first player
            AddPlayer(NetworkManager.Singleton.LocalClientId, "Host");
        }

        Debug.Log($"[LobbyManager] Spawned. IsServer={IsServer}, IsClient={IsClient}");
    }

    public override void OnNetworkDespawn()
    {
        players.OnListChanged -= OnPlayerListChanged;

        if (IsServer && NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected;
        }
    }

    // ── Server: Connection Handling ──────────────────────────────────────

    private void HandleClientConnected(ulong clientId)
    {
        if (!IsServer) return;

        // Don't double-add the host
        if (clientId == NetworkManager.Singleton.LocalClientId) return;

        if (IsLobbyFull)
        {
            Debug.LogWarning($"[LobbyManager] Lobby full! Disconnecting client {clientId}");
            NetworkManager.Singleton.DisconnectClient(clientId);
            return;
        }

        AddPlayer(clientId, $"Player {PlayerCount + 1}");
        Debug.Log($"[LobbyManager] Client {clientId} joined. Players: {PlayerCount}/{maxPlayers}");
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        if (!IsServer) return;

        RemovePlayer(clientId);
        Debug.Log($"[LobbyManager] Client {clientId} left. Players: {PlayerCount}/{maxPlayers}");
    }

    private void AddPlayer(ulong clientId, string name)
    {
        int teamId = PlayerCount; // First player = team 0, second = team 1

        players.Add(new PlayerData
        {
            ClientId = clientId,
            TeamId = teamId,
            IsReady = false,
            PlayerName = new FixedString64Bytes(name)
        });

        // Notify via ClientRpc
        NotifyPlayerJoinedClientRpc(clientId);
    }

    private void RemovePlayer(ulong clientId)
    {
        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].ClientId == clientId)
            {
                players.RemoveAt(i);
                NotifyPlayerLeftClientRpc(clientId);
                break;
            }
        }
    }

    // ── Ready System ─────────────────────────────────────────────────────

    /// <summary>Client calls this to toggle ready state.</summary>
    [ServerRpc(RequireOwnership = false)]
    public void SetReadyServerRpc(bool ready, ServerRpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;

        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].ClientId == clientId)
            {
                var data = players[i];
                data.IsReady = ready;
                players[i] = data;

                Debug.Log($"[LobbyManager] Player {clientId} ready={ready}");
                break;
            }
        }

        // Check if all players are ready
        CheckAllReady();
    }

    /// <summary>Client calls this to set their display name.</summary>
    [ServerRpc(RequireOwnership = false)]
    public void SetPlayerNameServerRpc(FixedString64Bytes name, ServerRpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;

        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].ClientId == clientId)
            {
                var data = players[i];
                data.PlayerName = name;
                players[i] = data;
                break;
            }
        }
    }

    private void CheckAllReady()
    {
        if (!IsServer) return;
        if (PlayerCount < maxPlayers) return; // Need all players first

        bool allReady = true;
        for (int i = 0; i < players.Count; i++)
        {
            if (!players[i].IsReady)
            {
                allReady = false;
                break;
            }
        }

        if (allReady)
        {
            Debug.Log("[LobbyManager] All players ready! Starting game...");
            OnAllPlayersReady?.Invoke();
        }
    }

    // ── Queries ──────────────────────────────────────────────────────────

    /// <summary>Get player data by client ID.</summary>
    public PlayerData? GetPlayerData(ulong clientId)
    {
        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].ClientId == clientId)
                return players[i];
        }
        return null;
    }

    /// <summary>Get player data by team ID.</summary>
    public PlayerData? GetPlayerByTeam(int teamId)
    {
        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].TeamId == teamId)
                return players[i];
        }
        return null;
    }

    /// <summary>Get the team ID for a given client.</summary>
    public int GetTeamId(ulong clientId)
    {
        var data = GetPlayerData(clientId);
        return data?.TeamId ?? -1;
    }

    /// <summary>Get all player data as a list.</summary>
    public List<PlayerData> GetAllPlayers()
    {
        var list = new List<PlayerData>();
        for (int i = 0; i < players.Count; i++)
            list.Add(players[i]);
        return list;
    }

    /// <summary>Check if a specific client is ready.</summary>
    public bool IsPlayerReady(ulong clientId)
    {
        var data = GetPlayerData(clientId);
        return data?.IsReady ?? false;
    }

    // ── Client Notifications ─────────────────────────────────────────────

    private void OnPlayerListChanged(NetworkListEvent<PlayerData> changeEvent)
    {
        OnPlayersChanged?.Invoke();
    }

    [ClientRpc]
    private void NotifyPlayerJoinedClientRpc(ulong clientId)
    {
        OnPlayerJoined?.Invoke(clientId);
        Debug.Log($"[LobbyManager] Player {clientId} joined the lobby.");
    }

    [ClientRpc]
    private void NotifyPlayerLeftClientRpc(ulong clientId)
    {
        OnPlayerLeft?.Invoke(clientId);
        Debug.Log($"[LobbyManager] Player {clientId} left the lobby.");
    }

    // ── Debug UI (temporary, will be replaced by proper UI on Day 8) ─────

    void OnGUI()
    {
        if (!IsSpawned) return;
        if (GameManager.Instance != null && GameManager.Instance.CurrentPhase.Value != GamePhase.WaitingForPlayers) return;

        GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
            fontStyle = FontStyle.Bold
        };
        titleStyle.normal.textColor = Color.white;

        GUIStyle infoStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14
        };
        infoStyle.normal.textColor = Color.white;

        float x = Screen.width - 320;
        float y = 10;

        GUI.Label(new Rect(x, y, 300, 30), $"🎮 Lobby ({PlayerCount}/{maxPlayers})", titleStyle);
        y += 30;

        for (int i = 0; i < players.Count; i++)
        {
            var p = players[i];
            string readyIcon = p.IsReady ? "✅" : "⬜";
            string label = $"{readyIcon} Team {p.TeamId}: {p.PlayerName}";

            if (p.ClientId == NetworkManager.Singleton.LocalClientId)
                label += " (You)";

            GUI.Label(new Rect(x, y, 300, 25), label, infoStyle);
            y += 22;
        }

        y += 10;

        // Ready button for local player
        if (!IsPlayerReady(NetworkManager.Singleton.LocalClientId))
        {
            if (GUI.Button(new Rect(x, y, 150, 35), "Ready Up!"))
            {
                SetReadyServerRpc(true);
            }
        }
        else
        {
            if (GUI.Button(new Rect(x, y, 150, 35), "Not Ready"))
            {
                SetReadyServerRpc(false);
            }
        }
    }
}
