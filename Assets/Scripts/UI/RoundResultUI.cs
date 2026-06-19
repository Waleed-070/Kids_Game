// ============================================================================
// RoundResultUI.cs — Displays the end-of-round Win/Loss/Draw screen.
// Pops up over the screen when the GamePhase changes to RoundResult.
// ============================================================================

using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RoundResultUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject panel;
    [SerializeField] private TextMeshProUGUI resultText;
    [SerializeField] private Button playAgainButton;

    void Start()
    {
        if (playAgainButton != null)
        {
            playAgainButton.onClick.AddListener(OnPlayAgainClicked);
        }

        if (GameManager.Instance != null)
        {
            // Subscribe to phase changes so we can show/hide automatically
            GameManager.Instance.CurrentPhase.OnValueChanged += OnPhaseChanged;
            GameManager.Instance.OnRoundResult += HandleRoundResult;

            // Set initial visibility state
            OnPhaseChanged(GamePhase.WaitingForPlayers, GameManager.Instance.CurrentPhase.Value);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.CurrentPhase.OnValueChanged -= OnPhaseChanged;
            GameManager.Instance.OnRoundResult -= HandleRoundResult;
        }
    }

    private void OnPhaseChanged(GamePhase prev, GamePhase current)
    {
        // Only show this UI if the game is in the RoundResult phase!
        if (current == GamePhase.RoundResult)
        {
            if (panel != null) panel.SetActive(true);
            
            // Re-enable the button when we show the screen
            if (playAgainButton != null) playAgainButton.interactable = true;
        }
        else
        {
            if (panel != null) panel.SetActive(false);
        }
    }

    private void HandleRoundResult(int winnerTeamId, string team0Reason, string team1Reason)
    {
        int myTeam = LobbyManager.Instance.GetTeamId(Unity.Netcode.NetworkManager.Singleton.LocalClientId);
        string myReason = myTeam == 0 ? team0Reason : team1Reason;
        string oppReason = myTeam == 0 ? team1Reason : team0Reason;

        if (winnerTeamId < 0)
        {
            if (myReason == "Solved the puzzle!" && oppReason == "Solved the puzzle!")
            {
                resultText.text = "DRAW!\n<size=32>Both teams solved it perfectly!</size>";
            }
            else
            {
                resultText.text = $"DRAW!\n<size=32>Both teams failed.</size>\n<size=24><color=#ffaa00>You: {myReason}</color></size>";
            }
            resultText.color = new Color(1f, 0.8f, 0.2f); // Yellow/Orange
        }
        else if (winnerTeamId == myTeam)
        {
            resultText.text = $"🎉 YOU WIN! 🎉\n<size=32>You solved the puzzle!</size>\n<size=24><color=#ffaa00>Opponent: {oppReason}</color></size>";
            resultText.color = new Color(0.2f, 1f, 0.4f); // Neon Green
        }
        else
        {
            resultText.text = $"😞 YOU LOST.\n<size=32>Your opponent solved it first.</size>\n<size=24><color=#ffaa00>You: {myReason}</color></size>";
            resultText.color = new Color(1f, 0.3f, 0.3f); // Red
        }
    }

    private void OnPlayAgainClicked()
    {
        // Prevent clicking it twice quickly
        playAgainButton.interactable = false;
        
        // Tell the server we want to reset and start round 2!
        GameManager.Instance.RequestResetServerRpc();
    }
}
