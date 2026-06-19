// ============================================================================
// NetworkSetupUI.cs — Connection Screen (Host/Join via Relay or Direct IP)
// Supports two connection modes:
//   1. Unity Relay (internet play) — Host gets a 6-char join code
//   2. Direct IP (local WiFi) — Client enters Host's IP address
//
// Attach to a Canvas with the required UI elements wired in the Inspector.
// ============================================================================

using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Multiplayer;
using Unity.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Threading.Tasks;

public class NetworkSetupUI : MonoBehaviour
{
    [Header("── Mode Toggle ──")]
    [SerializeField] private Toggle relayToggle;
    [SerializeField] private TextMeshProUGUI modeLabel;

    [Header("── Relay UI ──")]
    [SerializeField] private TMP_InputField joinCodeInput;
    [SerializeField] private TextMeshProUGUI joinCodeDisplay;  // shows code after hosting

    [Header("── Direct IP UI ──")]
    [SerializeField] private TMP_InputField ipAddressInput;
    [SerializeField] private TMP_InputField portInput;

    [Header("── Buttons ──")]
    [SerializeField] private Button hostButton;
    [SerializeField] private Button clientButton;

    [Header("── Status ──")]
    [SerializeField] private TextMeshProUGUI statusText;

    [Header("── Settings ──")]
    [SerializeField] private ushort defaultPort = 7777;

    // ── Internal State ───────────────────────────────────────────────────
    private bool useRelay = true;
    private bool isInitialized = false;
    private ISession currentSession;

    // ── Lifecycle ────────────────────────────────────────────────────────

    async void Start()
    {
        // Wire button listeners
        hostButton.onClick.AddListener(OnHostClicked);
        clientButton.onClick.AddListener(OnClientClicked);

        if (relayToggle != null)
        {
            relayToggle.isOn = true;
            relayToggle.onValueChanged.AddListener(OnModeToggled);
        }

        // Default port
        if (portInput != null)
            portInput.text = defaultPort.ToString();

        UpdateModeUI();
        SetStatus("Initializing services...");

        // Initialize Unity Services (required for Relay)
        try
        {
            await UnityServices.InitializeAsync();

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }

            isInitialized = true;
            SetStatus("Ready. Choose Host or Join.");
            Debug.Log("[NetworkSetupUI] Unity Services initialized, signed in anonymously.");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[NetworkSetupUI] Unity Services init failed: {e.Message}");
            SetStatus("⚠️ Online services unavailable. Use Direct IP mode.");

            // Force direct IP mode if services fail
            useRelay = false;
            if (relayToggle != null) relayToggle.isOn = false;
            UpdateModeUI();
            isInitialized = true; // Still allow direct connections
        }
    }

    // ── Mode Toggle ──────────────────────────────────────────────────────

    private void OnModeToggled(bool isRelay)
    {
        useRelay = isRelay;
        UpdateModeUI();
    }

    private void UpdateModeUI()
    {
        if (modeLabel != null)
            modeLabel.text = useRelay ? "🌐 Internet (Relay)" : "📶 Local WiFi (Direct IP)";

        // Show/hide relevant input fields
        if (joinCodeInput != null)
            joinCodeInput.gameObject.SetActive(useRelay);
        if (joinCodeDisplay != null)
            joinCodeDisplay.gameObject.SetActive(useRelay);
        if (ipAddressInput != null)
            ipAddressInput.gameObject.SetActive(!useRelay);
        if (portInput != null)
            portInput.gameObject.SetActive(!useRelay);
    }

    // ── Host ─────────────────────────────────────────────────────────────

    private async void OnHostClicked()
    {
        if (!isInitialized)
        {
            SetStatus("⏳ Still initializing...");
            return;
        }

        SetButtonsInteractable(false);

        if (useRelay)
        {
            await HostWithRelay();
        }
        else
        {
            HostDirect();
        }
    }

    private async Task HostWithRelay()
    {
        SetStatus("Creating relay session...");

        try
        {
            // Use Unity Multiplayer Services Session API
            var sessionOptions = new SessionOptions()
            {
                MaxPlayers = 2
            }.WithRelayNetwork();

            currentSession = await MultiplayerService.Instance.CreateSessionAsync(sessionOptions);

            SetStatus($"✅ Hosting! Join Code: {currentSession.Code}");

            if (joinCodeDisplay != null)
            {
                joinCodeDisplay.text = $"Join Code: {currentSession.Code}";
                joinCodeDisplay.gameObject.SetActive(true);
            }

            Debug.Log($"[NetworkSetupUI] Relay session created. Code: {currentSession.Code}");

            // NGO Host is started automatically by the session
            // Wait for client connection
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        }
        catch (Exception e)
        {
            Debug.LogError($"[NetworkSetupUI] Relay host failed: {e.Message}");
            SetStatus($"❌ Relay failed: {e.Message}");
            SetButtonsInteractable(true);
        }
    }

    private void HostDirect()
    {
        SetStatus("Starting direct host...");

        try
        {
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();

            // Configure for WebSocket (required for WebGL)
            transport.SetConnectionData(
                "0.0.0.0",
                GetPort(),
                "0.0.0.0"
            );

            NetworkManager.Singleton.StartHost();
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;

            string localIP = GetLocalIPAddress();
            SetStatus($"✅ Hosting on {localIP}:{GetPort()}");

            Debug.Log($"[NetworkSetupUI] Direct host started on {localIP}:{GetPort()}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[NetworkSetupUI] Direct host failed: {e.Message}");
            SetStatus($"❌ Host failed: {e.Message}");
            SetButtonsInteractable(true);
        }
    }

    // ── Client ───────────────────────────────────────────────────────────

    private async void OnClientClicked()
    {
        if (!isInitialized)
        {
            SetStatus("⏳ Still initializing...");
            return;
        }

        SetButtonsInteractable(false);

        if (useRelay)
        {
            await JoinWithRelay();
        }
        else
        {
            JoinDirect();
        }
    }

    private async Task JoinWithRelay()
    {
        string code = joinCodeInput != null ? joinCodeInput.text.Trim().ToUpper() : "";

        if (string.IsNullOrEmpty(code))
        {
            SetStatus("❌ Enter a join code!");
            SetButtonsInteractable(true);
            return;
        }

        SetStatus($"Joining session {code}...");

        try
        {
            currentSession = await MultiplayerService.Instance.JoinSessionByCodeAsync(code);

            SetStatus("✅ Connected to host!");
            Debug.Log($"[NetworkSetupUI] Joined relay session with code: {code}");

            // Hide UI after short delay
            Invoke(nameof(HideUI), 1.0f);
        }
        catch (Exception e)
        {
            Debug.LogError($"[NetworkSetupUI] Relay join failed: {e.Message}");
            SetStatus($"❌ Join failed: {e.Message}");
            SetButtonsInteractable(true);
        }
    }

    private void JoinDirect()
    {
        string ip = ipAddressInput != null ? ipAddressInput.text.Trim() : "127.0.0.1";

        if (string.IsNullOrEmpty(ip))
        {
            ip = "127.0.0.1";
        }

        SetStatus($"Connecting to {ip}:{GetPort()}...");

        try
        {
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetConnectionData(ip, GetPort());

            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

            NetworkManager.Singleton.StartClient();

            Debug.Log($"[NetworkSetupUI] Connecting to {ip}:{GetPort()}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[NetworkSetupUI] Direct join failed: {e.Message}");
            SetStatus($"❌ Connection failed: {e.Message}");
            SetButtonsInteractable(true);
        }
    }

    // ── Callbacks ────────────────────────────────────────────────────────

    private void OnClientConnected(ulong clientId)
    {
        int connectedCount = NetworkManager.Singleton.ConnectedClientsIds.Count;
        SetStatus($"✅ Player connected! ({connectedCount}/2)");
        Debug.Log($"[NetworkSetupUI] Client {clientId} connected. Total: {connectedCount}");

        if (connectedCount >= 2)
        {
            SetStatus("✅ All players connected! Starting game...");
            Invoke(nameof(HideUI), 1.5f);
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        // Only handle if we're the one disconnecting (failed to connect)
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            SetStatus("❌ Disconnected from host.");
            SetButtonsInteractable(true);
        }
    }

    // ── UI Helpers ───────────────────────────────────────────────────────

    private void SetStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;
        Debug.Log($"[NetworkSetupUI] {message}");
    }

    private void SetButtonsInteractable(bool interactable)
    {
        if (hostButton != null) hostButton.interactable = interactable;
        if (clientButton != null) clientButton.interactable = interactable;
    }

    private void HideUI()
    {
        gameObject.SetActive(false);
    }

    private ushort GetPort()
    {
        if (portInput != null && ushort.TryParse(portInput.text, out ushort port))
            return port;
        return defaultPort;
    }

    private string GetLocalIPAddress()
    {
        try
        {
            var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    return ip.ToString();
            }
        }
        catch { }
        return "127.0.0.1";
    }

    void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }
}