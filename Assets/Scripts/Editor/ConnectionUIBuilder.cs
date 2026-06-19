// ============================================================================
// ConnectionUIBuilder.cs — Editor utility to auto-create the Connection UI
// Run from: Unity menu bar → Robot Rescue → Create Connection UI
// This creates a Canvas with all required TMPro elements and wires them
// to the NetworkSetupUI component automatically.
// ============================================================================

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

public class ConnectionUIBuilder
{
    [MenuItem("Robot Rescue/Create Connection UI")]
    public static void CreateConnectionUI()
    {
        // ── Find or create Canvas ────────────────────────────────────────
        Canvas existingCanvas = Object.FindFirstObjectByType<Canvas>();
        GameObject canvasObj;

        if (existingCanvas != null)
        {
            canvasObj = existingCanvas.gameObject;
            Debug.Log("[UIBuilder] Using existing Canvas.");
        }
        else
        {
            canvasObj = new GameObject("Canvas");
            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
            Debug.Log("[UIBuilder] Created new Canvas.");
        }

        // Set up CanvasScaler for responsive UI
        var scaler = canvasObj.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
        }

        // ── Create EventSystem if missing ────────────────────────────────
        if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystem.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            Undo.RegisterCreatedObjectUndo(eventSystem, "Create EventSystem");
        }

        // ── Main Panel (dark semi-transparent background) ────────────────
        GameObject panel = CreatePanel(canvasObj.transform, "ConnectionPanel",
            new Color(0.12f, 0.12f, 0.18f, 0.95f));

        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(520, 580);
        panelRect.anchoredPosition = Vector2.zero;

        // Add vertical layout
        var vlg = panel.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 12;
        vlg.padding = new RectOffset(30, 30, 25, 25);
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        // ── Title ────────────────────────────────────────────────────────
        var title = CreateTMPText(panel.transform, "Title",
            "🤖 Robot Rescue Team", 28, FontStyles.Bold,
            new Color(0.4f, 0.85f, 1f), 45);

        // ── Subtitle ─────────────────────────────────────────────────────
        CreateTMPText(panel.transform, "Subtitle",
            "Multiplayer Connection", 16, FontStyles.Normal,
            new Color(0.7f, 0.7f, 0.8f), 25);

        // ── Spacer ───────────────────────────────────────────────────────
        CreateSpacer(panel.transform, 10);

        // ── Mode Toggle Row ──────────────────────────────────────────────
        GameObject toggleRow = CreateRow(panel.transform, "ToggleRow", 35);

        var modeLabel = CreateTMPText(toggleRow.transform, "ModeLabel",
            "🌐 Internet (Relay)", 15, FontStyles.Normal,
            new Color(0.9f, 0.9f, 0.9f), 35);
        var modeLabelRect = modeLabel.GetComponent<RectTransform>();
        modeLabelRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 300);

        GameObject toggleObj = CreateToggle(toggleRow.transform, "RelayToggle", true);

        // ── Join Code Input (Relay mode) ─────────────────────────────────
        var joinCodeInput = CreateInputField(panel.transform, "JoinCodeInput",
            "Enter Join Code...", 45);

        // ── Join Code Display (shows code after hosting) ─────────────────
        var joinCodeDisplay = CreateTMPText(panel.transform, "JoinCodeDisplay",
            "", 20, FontStyles.Bold,
            new Color(0.3f, 1f, 0.5f), 35);

        // ── IP Address Input (Direct mode) ───────────────────────────────
        var ipInput = CreateInputField(panel.transform, "IPAddressInput",
            "Enter IP Address (e.g. 192.168.1.5)...", 45);

        // ── Port Input (Direct mode) ─────────────────────────────────────
        var portInput = CreateInputField(panel.transform, "PortInput",
            "Port (default: 7777)", 45);
        portInput.GetComponent<TMP_InputField>().text = "7777";

        // ── Spacer ───────────────────────────────────────────────────────
        CreateSpacer(panel.transform, 8);

        // ── Buttons Row ──────────────────────────────────────────────────
        GameObject buttonRow = CreateRow(panel.transform, "ButtonRow", 55);

        var hostBtn = CreateButton(buttonRow.transform, "HostButton",
            "🎮 HOST", new Color(0.2f, 0.65f, 0.35f), 55);

        var clientBtn = CreateButton(buttonRow.transform, "ClientButton",
            "🔗 JOIN", new Color(0.25f, 0.5f, 0.85f), 55);

        // ── Status Text ──────────────────────────────────────────────────
        CreateSpacer(panel.transform, 5);
        var statusText = CreateTMPText(panel.transform, "StatusText",
            "Initializing...", 14, FontStyles.Italic,
            new Color(0.8f, 0.8f, 0.5f), 30);

        // ── Wire up NetworkSetupUI component ─────────────────────────────
        NetworkSetupUI setupUI = canvasObj.GetComponent<NetworkSetupUI>();
        if (setupUI == null)
            setupUI = canvasObj.AddComponent<NetworkSetupUI>();

        // Use SerializedObject to set private [SerializeField] references
        var so = new SerializedObject(setupUI);

        so.FindProperty("relayToggle").objectReferenceValue =
            toggleObj.GetComponent<Toggle>();
        so.FindProperty("modeLabel").objectReferenceValue =
            modeLabel.GetComponent<TextMeshProUGUI>();
        so.FindProperty("joinCodeInput").objectReferenceValue =
            joinCodeInput.GetComponent<TMP_InputField>();
        so.FindProperty("joinCodeDisplay").objectReferenceValue =
            joinCodeDisplay.GetComponent<TextMeshProUGUI>();
        so.FindProperty("ipAddressInput").objectReferenceValue =
            ipInput.GetComponent<TMP_InputField>();
        so.FindProperty("portInput").objectReferenceValue =
            portInput.GetComponent<TMP_InputField>();
        so.FindProperty("hostButton").objectReferenceValue =
            hostBtn.GetComponent<Button>();
        so.FindProperty("clientButton").objectReferenceValue =
            clientBtn.GetComponent<Button>();
        so.FindProperty("statusText").objectReferenceValue =
            statusText.GetComponent<TextMeshProUGUI>();

        so.ApplyModifiedProperties();

        // ── Register Undo ────────────────────────────────────────────────
        Undo.RegisterCreatedObjectUndo(panel, "Create Connection UI");

        Debug.Log("<color=green>[UIBuilder] ✅ Connection UI created and wired!</color>");
        Selection.activeGameObject = panel;
    }

    // ════════════════════════════════════════════════════════════════════
    // UI FACTORY METHODS
    // ════════════════════════════════════════════════════════════════════

    private static GameObject CreatePanel(Transform parent, string name, Color bgColor)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);

        var image = obj.AddComponent<Image>();
        image.color = bgColor;

        // Rounded corners effect (subtle)
        image.type = Image.Type.Sliced;

        return obj;
    }

    private static GameObject CreateTMPText(Transform parent, string name,
        string text, int fontSize, FontStyles fontStyle, Color color, float height)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);

        var tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = fontStyle;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = true;

        var rect = obj.GetComponent<RectTransform>();
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);

        var le = obj.AddComponent<LayoutElement>();
        le.preferredHeight = height;

        return obj;
    }

    private static GameObject CreateInputField(Transform parent, string name,
        string placeholder, float height)
    {
        // Container
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);

        var rect = obj.AddComponent<RectTransform>();
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);

        var le = obj.AddComponent<LayoutElement>();
        le.preferredHeight = height;

        // Background
        var bg = obj.AddComponent<Image>();
        bg.color = new Color(0.18f, 0.18f, 0.25f, 1f);

        // Text Area
        var textArea = new GameObject("Text Area");
        textArea.transform.SetParent(obj.transform, false);
        var taRect = textArea.AddComponent<RectTransform>();
        taRect.anchorMin = Vector2.zero;
        taRect.anchorMax = Vector2.one;
        taRect.offsetMin = new Vector2(10, 5);
        taRect.offsetMax = new Vector2(-10, -5);

        // Placeholder
        var phObj = new GameObject("Placeholder");
        phObj.transform.SetParent(textArea.transform, false);
        var phTMP = phObj.AddComponent<TextMeshProUGUI>();
        phTMP.text = placeholder;
        phTMP.fontSize = 14;
        phTMP.fontStyle = FontStyles.Italic;
        phTMP.color = new Color(0.5f, 0.5f, 0.6f);
        phTMP.alignment = TextAlignmentOptions.MidlineLeft;

        var phRect = phObj.GetComponent<RectTransform>();
        phRect.anchorMin = Vector2.zero;
        phRect.anchorMax = Vector2.one;
        phRect.offsetMin = Vector2.zero;
        phRect.offsetMax = Vector2.zero;

        // Input Text
        var txtObj = new GameObject("Text");
        txtObj.transform.SetParent(textArea.transform, false);
        var txtTMP = txtObj.AddComponent<TextMeshProUGUI>();
        txtTMP.fontSize = 16;
        txtTMP.color = Color.white;
        txtTMP.alignment = TextAlignmentOptions.MidlineLeft;

        var txtRect = txtObj.GetComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.offsetMin = Vector2.zero;
        txtRect.offsetMax = Vector2.zero;

        // TMP_InputField component
        var inputField = obj.AddComponent<TMP_InputField>();
        inputField.textViewport = taRect;
        inputField.textComponent = txtTMP;
        inputField.placeholder = phTMP;
        inputField.fontAsset = txtTMP.font;
        inputField.pointSize = 16;

        return obj;
    }

    private static GameObject CreateButton(Transform parent, string name,
        string label, Color bgColor, float height)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);

        var image = obj.AddComponent<Image>();
        image.color = bgColor;

        var button = obj.AddComponent<Button>();
        var colors = button.colors;
        colors.highlightedColor = bgColor * 1.2f;
        colors.pressedColor = bgColor * 0.8f;
        button.colors = colors;

        var le = obj.AddComponent<LayoutElement>();
        le.preferredHeight = height;
        le.flexibleWidth = 1;

        // Button label
        var txtObj = new GameObject("Label");
        txtObj.transform.SetParent(obj.transform, false);

        var tmp = txtObj.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 20;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;

        var txtRect = txtObj.GetComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.offsetMin = Vector2.zero;
        txtRect.offsetMax = Vector2.zero;

        return obj;
    }

    private static GameObject CreateToggle(Transform parent, string name, bool isOn)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);

        var le = obj.AddComponent<LayoutElement>();
        le.preferredWidth = 80;
        le.preferredHeight = 30;

        // Background
        var bg = new GameObject("Background");
        bg.transform.SetParent(obj.transform, false);
        var bgImage = bg.AddComponent<Image>();
        bgImage.color = new Color(0.3f, 0.3f, 0.4f);
        var bgRect = bg.GetComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0.5f, 0.5f);
        bgRect.anchorMax = new Vector2(0.5f, 0.5f);
        bgRect.sizeDelta = new Vector2(50, 26);

        // Checkmark
        var checkmark = new GameObject("Checkmark");
        checkmark.transform.SetParent(bg.transform, false);
        var cmImage = checkmark.AddComponent<Image>();
        cmImage.color = new Color(0.3f, 0.85f, 0.5f);
        var cmRect = checkmark.GetComponent<RectTransform>();
        cmRect.anchorMin = new Vector2(0, 0);
        cmRect.anchorMax = new Vector2(1, 1);
        cmRect.offsetMin = new Vector2(3, 3);
        cmRect.offsetMax = new Vector2(-3, -3);

        // Toggle component
        var toggle = obj.AddComponent<Toggle>();
        toggle.isOn = isOn;
        toggle.targetGraphic = bgImage;
        toggle.graphic = cmImage;

        return obj;
    }

    private static GameObject CreateRow(Transform parent, string name, float height)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);

        var le = obj.AddComponent<LayoutElement>();
        le.preferredHeight = height;

        var hlg = obj.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 15;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;

        return obj;
    }

    private static void CreateSpacer(Transform parent, float height)
    {
        var obj = new GameObject("Spacer");
        obj.transform.SetParent(parent, false);

        var rect = obj.AddComponent<RectTransform>();
        var le = obj.AddComponent<LayoutElement>();
        le.preferredHeight = height;
    }
}
#endif
