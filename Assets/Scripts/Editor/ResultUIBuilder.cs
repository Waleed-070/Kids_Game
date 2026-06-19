// ============================================================================
// ResultUIBuilder.cs — Editor tool to auto-generate the Round Result UI.
// ============================================================================

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

public class ResultUIBuilder
{
    [MenuItem("Robot Rescue/Create Result UI")]
    public static void CreateResultUI()
    {
        // Must put this on Canvas_Gameplay so it renders over the game
        GameObject canvasObj = GameObject.Find("Canvas_Gameplay");
        if (canvasObj == null)
        {
            Debug.LogError("[ResultUIBuilder] Could not find Canvas_Gameplay. Did you run 'Create Gameplay UI' first?");
            return;
        }

        Transform canvasT = canvasObj.transform;

        // ── Main Popup Panel ─────────────────────────────────────────
        GameObject panelObj = new GameObject("RoundResultPanel");
        panelObj.transform.SetParent(canvasT, false);
        
        // Center the panel
        var panelRect = panelObj.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.25f, 0.3f);
        panelRect.anchorMax = new Vector2(0.75f, 0.7f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        var img = panelObj.AddComponent<Image>();
        img.color = new Color(0.08f, 0.08f, 0.12f, 0.98f); // Sleek dark panel

        // ── Title Text (Win/Loss) ────────────────────────────────────
        GameObject titleObj = new GameObject("ResultText");
        titleObj.transform.SetParent(panelObj.transform, false);
        
        var titleRect = titleObj.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0, 0.4f);
        titleRect.anchorMax = new Vector2(1, 1);
        titleRect.offsetMin = new Vector2(20, 20);
        titleRect.offsetMax = new Vector2(-20, -20);

        var titleText = titleObj.AddComponent<TextMeshProUGUI>();
        titleText.text = "Result";
        titleText.fontSize = 72;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.fontStyle = FontStyles.Bold;

        // ── Play Again Button ────────────────────────────────────────
        GameObject btnObj = new GameObject("PlayAgainButton");
        btnObj.transform.SetParent(panelObj.transform, false);
        
        var btnRect = btnObj.AddComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(0.3f, 0.1f);
        btnRect.anchorMax = new Vector2(0.7f, 0.3f);
        btnRect.offsetMin = Vector2.zero;
        btnRect.offsetMax = Vector2.zero;

        var btnImg = btnObj.AddComponent<Image>();
        btnImg.color = new Color(0.2f, 0.6f, 1.0f); // Bright blue button

        var btn = btnObj.AddComponent<Button>();

        GameObject btnTextObj = new GameObject("Text");
        btnTextObj.transform.SetParent(btnObj.transform, false);
        
        var btnTextRect = btnTextObj.AddComponent<RectTransform>();
        btnTextRect.anchorMin = Vector2.zero;
        btnTextRect.anchorMax = Vector2.one;
        btnTextRect.offsetMin = Vector2.zero;
        btnTextRect.offsetMax = Vector2.zero;

        var btnText = btnTextObj.AddComponent<TextMeshProUGUI>();
        btnText.text = "PLAY AGAIN";
        btnText.fontSize = 36;
        btnText.alignment = TextAlignmentOptions.Center;
        btnText.color = Color.white;
        btnText.fontStyle = FontStyles.Bold;

        // ── Attach Logic ─────────────────────────────────────────────
        var logic = canvasObj.AddComponent<RoundResultUI>();
        
        var serializedObject = new SerializedObject(logic);
        serializedObject.FindProperty("panel").objectReferenceValue = panelObj;
        serializedObject.FindProperty("resultText").objectReferenceValue = titleText;
        serializedObject.FindProperty("playAgainButton").objectReferenceValue = btn;
        serializedObject.ApplyModifiedProperties();

        // Hide panel by default
        panelObj.SetActive(false);

        Debug.Log("<color=green>[ResultUIBuilder] Created RoundResultUI successfully!</color>");
        
        // Mark scene dirty so user doesn't lose it
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
    }
}
#endif
