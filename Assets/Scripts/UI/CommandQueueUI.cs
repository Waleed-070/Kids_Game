// ============================================================================
// CommandQueueUI.cs — Visual UI for building and submitting commands.
// Lets players click available commands to add them to a sequence,
// and click queued commands to remove them.
// ============================================================================

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using Unity.Netcode;

public class CommandQueueUI : MonoBehaviour
{
    [Header("UI Containers")]
    [SerializeField] private Transform queueContainer;       // Where queued commands appear
    [SerializeField] private Button runButton;               // The big SEND button
    [SerializeField] private TextMeshProUGUI statusText;     // "Waiting for other team..."

    [Header("Prefabs / Templates")]
    [SerializeField] private GameObject queueItemPrefab;     // Instantiated for each queued command

    [Header("Available Command Buttons")]
    [SerializeField] private Button btnMoveForward;
    [SerializeField] private Button btnTurnLeft;
    [SerializeField] private Button btnTurnRight;
    [SerializeField] private Button btnInteract;

    // Internal State
    private List<CommandType> currentQueue = new List<CommandType>();
    private bool hasSubmitted = false;
    private int maxCommands = 9999;

    void Start()
    {
        // Wire up the palette buttons
        if (btnMoveForward != null) btnMoveForward.onClick.AddListener(() => AddCommand(CommandType.MoveForward));
        if (btnTurnLeft != null)    btnTurnLeft.onClick.AddListener(() => AddCommand(CommandType.TurnLeft));
        if (btnTurnRight != null)   btnTurnRight.onClick.AddListener(() => AddCommand(CommandType.TurnRight));
        if (btnInteract != null)    btnInteract.onClick.AddListener(() => AddCommand(CommandType.PickUp));

        // Create an Undo button on the fly
        if (btnMoveForward != null)
        {
            GameObject undoObj = Instantiate(btnMoveForward.gameObject, btnMoveForward.transform.parent);
            undoObj.name = "Btn_Undo";
            Button btnUndo = undoObj.GetComponent<Button>();
            btnUndo.onClick.RemoveAllListeners();
            btnUndo.onClick.AddListener(OnUndoClicked);
            
            var txt = undoObj.GetComponentInChildren<TextMeshProUGUI>();
            if (txt != null) txt.text = "⏪ Undo";
            
            var img = undoObj.GetComponent<Image>();
            if (img != null) img.color = new Color(0.8f, 0.2f, 0.2f); // Red
        }

        if (runButton != null)
        {
            runButton.onClick.AddListener(OnRunClicked);
        }

        UpdateUI();

        // Listen for game phase changes to show/hide this UI
        if (GameManager.Instance != null)
        {
            GameManager.Instance.CurrentPhase.OnValueChanged += OnPhaseChanged;
            // Set initial state
            OnPhaseChanged(GamePhase.WaitingForPlayers, GameManager.Instance.CurrentPhase.Value);
        }

        // Apply new engineering console layout
        ApplyEngineeringConsoleLayout();
    }

    void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.CurrentPhase.OnValueChanged -= OnPhaseChanged;
        }
    }

    private void OnPhaseChanged(GamePhase prev, GamePhase current)
    {
        if (current == GamePhase.Planning)
        {
            // Reset for a new round
            gameObject.SetActive(true);
            currentQueue.Clear();
            hasSubmitted = false;
            RefreshQueueDisplay();
            UpdateUI();
        }
        else if (current == GamePhase.Executing || current == GamePhase.WaitingForPlayers || current == GamePhase.StartMenu)
        {
            // Hide the palette during execution, lobby, or start menu
            gameObject.SetActive(false);
        }
    }

    // ── UI Auto-Layout ────────────────────────────────────────────────────

    private void ApplyEngineeringConsoleLayout()
    {
        // 1. Command Deck (Left Sidebar)
        if (btnMoveForward != null)
        {
            RectTransform paletteRect = btnMoveForward.transform.parent.GetComponent<RectTransform>();
            if (paletteRect != null)
            {
                // Anchor to Middle-Left
                paletteRect.anchorMin = new Vector2(0, 0.5f);
                paletteRect.anchorMax = new Vector2(0, 0.5f);
                paletteRect.pivot = new Vector2(0, 0.5f);
                paletteRect.anchoredPosition = new Vector2(20, 0); // 20px padding from left edge
                
                // Change HorizontalLayoutGroup to VerticalLayoutGroup if it exists
                var hlg = paletteRect.GetComponent<HorizontalLayoutGroup>();
                if (hlg != null)
                {
                    float spacing = hlg.spacing;
                    DestroyImmediate(hlg);
                    var vlg = paletteRect.gameObject.AddComponent<VerticalLayoutGroup>();
                    vlg.spacing = spacing;
                    vlg.childAlignment = TextAnchor.MiddleCenter;
                    vlg.childControlHeight = false;
                    vlg.childControlWidth = false;
                }
            }

            // Update button texts to icons
            UpdateIcon(btnMoveForward, "⬆️\nFwd");
            UpdateIcon(btnTurnLeft, "↩️\nLeft");
            UpdateIcon(btnTurnRight, "↪️\nRight");
            UpdateIcon(btnInteract, "🖐️\nInteract");
        }

        // 2. Pipeline Display (Bottom Center)
        if (queueContainer != null)
        {
            RectTransform queueRect = queueContainer.GetComponent<RectTransform>();
            if (queueRect != null)
            {
                // Anchor to Bottom-Center
                queueRect.anchorMin = new Vector2(0.5f, 0);
                queueRect.anchorMax = new Vector2(0.5f, 0);
                queueRect.pivot = new Vector2(0.5f, 0);
                queueRect.anchoredPosition = new Vector2(0, 40); // 40px padding from bottom
            }
        }

        // 3. Launch Deck (Right Sidebar)
        if (runButton != null)
        {
            RectTransform runRect = runButton.GetComponent<RectTransform>();
            if (runRect != null)
            {
                // Extract from parent if it's currently inside the palette or bottom bar
                if (runRect.parent == queueContainer || (btnMoveForward != null && runRect.parent == btnMoveForward.transform.parent))
                {
                    runRect.SetParent(this.transform); 
                }

                // Anchor to Bottom-Right
                runRect.anchorMin = new Vector2(1, 0);
                runRect.anchorMax = new Vector2(1, 0);
                runRect.pivot = new Vector2(1, 0);
                runRect.anchoredPosition = new Vector2(-40, 40); // Padding from corner
                
                // Make it massive
                runRect.sizeDelta = new Vector2(150, 150);
                var img = runButton.GetComponent<Image>();
                if (img != null) img.color = new Color(0.2f, 0.8f, 0.2f); // Bright green
                
                var txt = runButton.GetComponentInChildren<TextMeshProUGUI>();
                if (txt != null)
                {
                    txt.text = "LAUNCH\n🚀";
                    txt.fontSize = 24;
                }
            }
        }
    }

    private void UpdateIcon(Button btn, string iconText)
    {
        if (btn == null) return;
        var txt = btn.GetComponentInChildren<TextMeshProUGUI>();
        if (txt != null)
        {
            txt.text = iconText;
            txt.fontSize = 20;
            txt.alignment = TextAlignmentOptions.Center;
        }
        var rect = btn.GetComponent<RectTransform>();
        if (rect != null)
        {
            // Make palette buttons square
            rect.sizeDelta = new Vector2(80, 80);
        }
    }

    // ── Queue Management ──────────────────────────────────────────────────

    private void AddCommand(CommandType type)
    {
        if (hasSubmitted) return;
        if (currentQueue.Count >= maxCommands)
        {
            SetStatus($"Max commands reached!", Color.yellow);
            return;
        }

        currentQueue.Add(type);
        RefreshQueueDisplay();
        UpdateUI();
    }

    private void OnUndoClicked()
    {
        if (hasSubmitted || currentQueue.Count == 0) return;
        currentQueue.RemoveAt(currentQueue.Count - 1);
        RefreshQueueDisplay();
        UpdateUI();
    }

    // ── Submission ────────────────────────────────────────────────────────

    private void OnRunClicked()
    {
        if (hasSubmitted || currentQueue.Count == 0) return;

        hasSubmitted = true;
        UpdateUI();
        SetStatus("Commands Sent! Waiting for opponent...", Color.yellow);

        // Convert list to comma-separated string
        string commandString = string.Join(",", currentQueue);

        // Send to Server
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SubmitCommandsServerRpc(commandString);
        }
    }

    // ── Visual Updates ────────────────────────────────────────────────────

    private void RefreshQueueDisplay()
    {
        // 1. Hide UI boxes (we don't use them anymore, user wants 3D grid highlights)
        if (queueContainer != null)
        {
            foreach (Transform child in queueContainer) Destroy(child.gameObject);
        }

        // 2. Clear all previous 3D grid highlights
        if (GridManager.Instance != null && GridManager.Instance.IsBuilt)
        {
            for (int z = 0; z < GridManager.Instance.Height; z++)
            {
                for (int x = 0; x < GridManager.Instance.Width; x++)
                {
                    var tile = GridManager.Instance.GetTileAt(x, z);
                    if (tile != null) tile.SetHighlight(false);
                }
            }
        }

        if (currentQueue.Count == 0) return;

        // 3. Simulate Path on the 3D Grid
        int currentX = GridManager.Instance.GetRobotStartPosition().x;
        int currentZ = GridManager.Instance.GetRobotStartPosition().y;
        FacingDirection facing = GridManager.Instance.GetRobotStartFacing();

        for (int i = 0; i < currentQueue.Count; i++)
        {
            CommandType cmd = currentQueue[i];

            if (cmd == CommandType.MoveForward)
            {
                int nextX = currentX;
                int nextZ = currentZ;
                switch (facing)
                {
                    case FacingDirection.North: nextZ++; break;
                    case FacingDirection.East:  nextX++; break;
                    case FacingDirection.South: nextZ--; break;
                    case FacingDirection.West:  nextX--; break;
                }

                var tile = GridManager.Instance.GetTileAt(nextX, nextZ);
                if (tile != null)
                {
                    if (tile.TileType == TileType.Wall)
                    {
                        tile.SetHighlight(true, new Color(1f, 0.2f, 0.2f)); // Red for Wall collision!
                        break; // Stop predicting
                    }
                    else
                    {
                        tile.SetHighlight(true, new Color(0.2f, 0.8f, 1f)); // Blue path
                    }
                }
                
                currentX = nextX;
                currentZ = nextZ;
            }
            else if (cmd == CommandType.TurnLeft)
            {
                facing = RotateLeft(facing);
            }
            else if (cmd == CommandType.TurnRight)
            {
                facing = RotateRight(facing);
            }
            else if (cmd == CommandType.PickUp)
            {
                var tile = GridManager.Instance.GetTileAt(currentX, currentZ);
                if (tile != null) tile.SetHighlight(true, Color.magenta); // Highlight purple for interact
            }
        }
    }

    private FacingDirection RotateLeft(FacingDirection dir)
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

    private FacingDirection RotateRight(FacingDirection dir)
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

    private void UpdateUI()
    {
        // Disable Run button if queue is empty or already submitted
        if (runButton != null)
        {
            runButton.interactable = (!hasSubmitted && currentQueue.Count > 0);
        }

        // Disable palette buttons if max reached or submitted
        bool canAdd = !hasSubmitted && currentQueue.Count < maxCommands;
        if (btnMoveForward != null) btnMoveForward.interactable = canAdd;
        if (btnTurnLeft != null)    btnTurnLeft.interactable = canAdd;
        if (btnTurnRight != null)   btnTurnRight.interactable = canAdd;
        if (btnInteract != null)    btnInteract.interactable = canAdd;

        if (!hasSubmitted)
        {
            SetStatus($"Commands: {currentQueue.Count}", Color.white);
        }
    }

    private void SetStatus(string msg, Color col)
    {
        if (statusText != null)
        {
            statusText.text = msg;
            statusText.color = col;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private string GetCommandLabel(CommandType type)
    {
        return type switch
        {
            CommandType.MoveForward => "⬆️ Forward",
            CommandType.TurnLeft => "⬅️ Left",
            CommandType.TurnRight => "➡️ Right",
            CommandType.PickUp => "🖐️ Pick Up",
            _ => type.ToString()
        };
    }

    private Color GetCommandColor(CommandType type)
    {
        return type switch
        {
            CommandType.MoveForward => new Color(0.2f, 0.6f, 1f),  // Blue
            CommandType.TurnLeft => new Color(0.8f, 0.6f, 0.2f),   // Orange
            CommandType.TurnRight => new Color(0.8f, 0.6f, 0.2f),  // Orange
            CommandType.PickUp => new Color(0.8f, 0.2f, 0.8f),   // Purple
            _ => Color.gray
        };
    }
}
