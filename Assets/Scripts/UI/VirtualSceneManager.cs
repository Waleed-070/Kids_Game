using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class VirtualSceneManager : MonoBehaviour
{
    public static VirtualSceneManager Instance;

    private GameObject startMenuMatthew;
    private Dictionary<ulong, GameObject> lobbyAvatars = new Dictionary<ulong, GameObject>();

    private bool showStartMenu = true;
    private bool showLobbyUI = false;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        // Initial setup for Start Menu
        SetupStartMenu();

        if (LobbyManager.Instance != null)
        {
            LobbyManager.Instance.OnPlayersChanged += SyncLobbyAvatars;
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.CurrentPhase.OnValueChanged += HandlePhaseChange;
        }
    }

    void OnDestroy()
    {
        if (LobbyManager.Instance != null)
        {
            LobbyManager.Instance.OnPlayersChanged -= SyncLobbyAvatars;
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.CurrentPhase.OnValueChanged -= HandlePhaseChange;
        }
    }

    private void HandlePhaseChange(GamePhase prev, GamePhase current)
    {
        if (current == GamePhase.Planning)
        {
            ClearLobby();
            showStartMenu = false;
        }
    }

    private void SetupStartMenu()
    {
        showStartMenu = true;
        showLobbyUI = false;

        // Spawn Dummy Matthew
        GameObject prefab = Resources.Load<GameObject>("MatthewModel");
        if (prefab != null)
        {
            startMenuMatthew = Instantiate(prefab, Vector3.zero, Quaternion.Euler(0, 180, 0));
        }

        // Setup Camera for Start Menu
        Camera.main.orthographic = false;
        Camera.main.transform.position = new Vector3(0, 1.5f, -4f); // Looking from front (negative Z)
        Camera.main.transform.rotation = Quaternion.Euler(0, 0, 0); // Face forward (positive Z)
        Camera.main.backgroundColor = new Color(0.1f, 0.1f, 0.15f);

        // Hide NetworkSetupUI initially if it exists
        var netUI = FindObjectOfType<NetworkSetupUI>();
        if (netUI != null) netUI.gameObject.SetActive(false);
    }

    private void EnterLobbyMode()
    {
        showStartMenu = false;
        showLobbyUI = true;

        // Show NetworkSetupUI
        var netUI = FindObjectOfType<NetworkSetupUI>(true);
        if (netUI != null) netUI.gameObject.SetActive(true);

        // Move camera slightly to make room for UI
        Camera.main.transform.position = new Vector3(2f, 1.5f, -4f);
    }

    private void SyncLobbyAvatars()
    {
        if (!showLobbyUI) return;

        var allPlayers = LobbyManager.Instance.GetAllPlayers();
        
        // Remove disconnected
        List<ulong> toRemove = new List<ulong>();
        foreach (var clientId in lobbyAvatars.Keys)
        {
            if (allPlayers.FindIndex(p => p.ClientId == clientId) == -1)
                toRemove.Add(clientId);
        }
        foreach (var id in toRemove) RemoveLobbyAvatar(id);

        // Add connected
        foreach (var player in allPlayers)
        {
            SpawnLobbyAvatar(player.ClientId, player.TeamId);
        }
    }

    public void SpawnLobbyAvatar(ulong clientId, int teamId)
    {
        if (lobbyAvatars.ContainsKey(clientId)) return;

        // Destroy dummy if host
        if (startMenuMatthew != null)
        {
            Destroy(startMenuMatthew);
        }

        GameObject prefab = Resources.Load<GameObject>("MatthewModel");
        if (prefab != null)
        {
            // Position players side by side
            float xOffset = teamId == 0 ? -1f : 1f;
            Vector3 pos = new Vector3(xOffset, 0, 0);

            GameObject avatar = Instantiate(prefab, pos, Quaternion.Euler(0, 180, 0));
            
            // Colorize
            var renderers = avatar.GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
            {
                Color c = teamId == 0 ? Color.cyan : Color.red;
                Material mat = r.material;
                if (mat.HasProperty("_BaseColor"))
                    mat.SetColor("_BaseColor", Color.Lerp(Color.white, c, 0.5f));
                else if (mat.HasProperty("_Color"))
                    mat.color = Color.Lerp(Color.white, c, 0.5f);
            }

            lobbyAvatars[clientId] = avatar;
        }
    }

    public void RemoveLobbyAvatar(ulong clientId)
    {
        if (lobbyAvatars.TryGetValue(clientId, out GameObject avatar))
        {
            Destroy(avatar);
            lobbyAvatars.Remove(clientId);
        }
    }

    public void ClearLobby()
    {
        foreach (var avatar in lobbyAvatars.Values)
        {
            Destroy(avatar);
        }
        lobbyAvatars.Clear();
        showLobbyUI = false;
    }

    void OnGUI()
    {
        if (showStartMenu)
        {
            GUIStyle btnStyle = new GUIStyle(GUI.skin.button);
            btnStyle.fontSize = 24;

            int width = 200;
            int height = 60;
            int padding = 20;

            // Bottom Right Corner
            Rect playRect = new Rect(Screen.width - width - padding, Screen.height - (height * 2) - padding * 2, width, height);
            Rect multiRect = new Rect(Screen.width - width - padding, Screen.height - height - padding, width, height);

            if (GUI.Button(playRect, "Play (Solo)", btnStyle))
            {
                Debug.Log("Solo play not implemented yet!");
            }

            if (GUI.Button(multiRect, "Multiplayer", btnStyle))
            {
                EnterLobbyMode();
            }

            // Title
            GUIStyle titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.fontSize = 48;
            titleStyle.alignment = TextAnchor.UpperCenter;
            titleStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(0, 50, Screen.width, 100), "ROBOT RESCUE TEAM", titleStyle);
        }
        else if (showLobbyUI && NetworkManager.Singleton.IsConnectedClient)
        {
            // Once connected, show Ready button
            GUIStyle btnStyle = new GUIStyle(GUI.skin.button);
            btnStyle.fontSize = 32;

            Rect readyRect = new Rect(Screen.width / 2 - 150, Screen.height - 100, 300, 80);
            
            // Determine if local player is ready
            bool isReady = LobbyManager.Instance.IsPlayerReady(NetworkManager.Singleton.LocalClientId);
            
            if (isReady)
            {
                GUI.color = Color.green;
                if (GUI.Button(readyRect, "READY ✓", btnStyle))
                {
                    LobbyManager.Instance.SetReadyServerRpc(false); // Toggle off
                }
                GUI.color = Color.white;
            }
            else
            {
                GUI.color = Color.yellow;
                if (GUI.Button(readyRect, "READY UP", btnStyle))
                {
                    LobbyManager.Instance.SetReadyServerRpc(true); // Toggle on
                }
                GUI.color = Color.white;
            }
        }
    }
}
