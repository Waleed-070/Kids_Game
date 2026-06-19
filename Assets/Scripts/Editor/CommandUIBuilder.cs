// ============================================================================
// CommandUIBuilder.cs — Editor utility to auto-create the gameplay UI (Day 5)
// Run from: Unity menu bar → Robot Rescue → Create Gameplay UI
// ============================================================================

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

public class CommandUIBuilder
{
    [MenuItem("Robot Rescue/Create Gameplay UI")]
    public static void CreateGameplayUI()
    {
        // ── Create Dedicated Gameplay Canvas ─────────────────────
        GameObject canvasObj = new GameObject("Canvas_Gameplay");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10; // Ensure it renders on top
        
        var scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        
        canvasObj.AddComponent<GraphicRaycaster>();

        Transform canvasT = canvasObj.transform;

        // ── Main Gameplay UI Container (Bottom of screen) ─────────
        GameObject gameplayPanel = new GameObject("GameplayUI");
        gameplayPanel.transform.SetParent(canvasT, false);
        var gpRect = gameplayPanel.AddComponent<RectTransform>();
        gpRect.anchorMin = new Vector2(0, 0); // Bottom left
        gpRect.anchorMax = new Vector2(1, 0); // Bottom right
        gpRect.pivot = new Vector2(0.5f, 0);
        gpRect.sizeDelta = new Vector2(0, 250); // Height of 250
        gpRect.anchoredPosition = Vector2.zero;

        var bg = gameplayPanel.AddComponent<Image>();
        bg.color = new Color(0.1f, 0.1f, 0.15f, 0.9f); // Dark background

        // ── Status Text ──────────────────────────────────────────
        GameObject statusObj = new GameObject("StatusText");
        statusObj.transform.SetParent(gameplayPanel.transform, false);
        var statusTxt = statusObj.AddComponent<TextMeshProUGUI>();
        statusTxt.text = "Commands: 0 / 10";
        statusTxt.fontSize = 18;
        statusTxt.fontStyle = FontStyles.Bold;
        statusTxt.alignment = TextAlignmentOptions.Center;
        
        var statusRect = statusObj.GetComponent<RectTransform>();
        statusRect.anchorMin = new Vector2(0, 1);
        statusRect.anchorMax = new Vector2(1, 1);
        statusRect.pivot = new Vector2(0.5f, 1);
        statusRect.sizeDelta = new Vector2(0, 30);
        statusRect.anchoredPosition = new Vector2(0, -10);

        // ── Layout Group for Queue and Palette ───────────────────
        GameObject splitPanel = new GameObject("SplitPanel");
        splitPanel.transform.SetParent(gameplayPanel.transform, false);
        var spRect = splitPanel.AddComponent<RectTransform>();
        spRect.anchorMin = new Vector2(0, 0);
        spRect.anchorMax = new Vector2(1, 1);
        spRect.offsetMin = new Vector2(20, 20);
        spRect.offsetMax = new Vector2(-20, -40);

        var hlg = splitPanel.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 20;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandHeight = true;

        // ── LEFT: Palette (Available Commands) ───────────────────
        GameObject palettePanel = new GameObject("PalettePanel");
        palettePanel.transform.SetParent(splitPanel.transform, false);
        var palLE = palettePanel.AddComponent<LayoutElement>();
        palLE.flexibleWidth = 1;

        var palGlg = palettePanel.AddComponent<GridLayoutGroup>();
        palGlg.cellSize = new Vector2(140, 60);
        palGlg.spacing = new Vector2(10, 10);
        palGlg.startAxis = GridLayoutGroup.Axis.Horizontal;
        palGlg.childAlignment = TextAnchor.MiddleCenter;

        Button btnFwd = CreateCmdBtn(palettePanel.transform, "Btn_Forward", "⬆️ Forward", new Color(0.2f, 0.6f, 1f));
        Button btnLeft = CreateCmdBtn(palettePanel.transform, "Btn_Left", "⬅️ Left", new Color(0.8f, 0.6f, 0.2f));
        Button btnRight = CreateCmdBtn(palettePanel.transform, "Btn_Right", "➡️ Right", new Color(0.8f, 0.6f, 0.2f));
        Button btnPick = CreateCmdBtn(palettePanel.transform, "Btn_Interact", "🖐️ Pick Up", new Color(0.8f, 0.2f, 0.8f));

        // ── MIDDLE: Queue Area ───────────────────────────────────
        GameObject queueArea = new GameObject("QueueArea");
        queueArea.transform.SetParent(splitPanel.transform, false);
        var qAreaLE = queueArea.AddComponent<LayoutElement>();
        qAreaLE.flexibleWidth = 2; // Takes more space

        var qaBg = queueArea.AddComponent<Image>();
        qaBg.color = new Color(0.05f, 0.05f, 0.08f, 0.5f);

        // Scroll View Setup (so queue can be long)
        var scrollRect = queueArea.AddComponent<ScrollRect>();
        scrollRect.horizontal = true;
        scrollRect.vertical = false;

        GameObject viewport = new GameObject("Viewport");
        viewport.transform.SetParent(queueArea.transform, false);
        var vpBg = viewport.AddComponent<Image>();
        var vpMask = viewport.AddComponent<Mask>();
        vpMask.showMaskGraphic = false;
        var vpRect = viewport.GetComponent<RectTransform>();
        vpRect.anchorMin = Vector2.zero;
        vpRect.anchorMax = Vector2.one;
        vpRect.offsetMin = Vector2.zero;
        vpRect.offsetMax = Vector2.zero;

        GameObject queueContent = new GameObject("QueueContent");
        queueContent.transform.SetParent(viewport.transform, false);
        var qcRect = queueContent.AddComponent<RectTransform>();
        qcRect.anchorMin = new Vector2(0, 0);
        qcRect.anchorMax = new Vector2(0, 1);
        qcRect.pivot = new Vector2(0, 0.5f);
        qcRect.sizeDelta = new Vector2(0, 0);

        var qcHlg = queueContent.AddComponent<HorizontalLayoutGroup>();
        qcHlg.spacing = 10;
        qcHlg.padding = new RectOffset(10, 10, 10, 10);
        qcHlg.childAlignment = TextAnchor.MiddleLeft;
        qcHlg.childControlWidth = false;
        qcHlg.childControlHeight = true;
        qcHlg.childForceExpandWidth = false;

        var csf = queueContent.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.content = qcRect;
        scrollRect.viewport = vpRect;

        // ── RIGHT: Run Button ────────────────────────────────────
        GameObject runArea = new GameObject("RunArea");
        runArea.transform.SetParent(splitPanel.transform, false);
        var runLE = runArea.AddComponent<LayoutElement>();
        runLE.preferredWidth = 150;
        runLE.flexibleWidth = 0;

        Button btnRun = CreateCmdBtn(runArea.transform, "Btn_Run", "▶ RUN!", new Color(0.2f, 0.8f, 0.3f));
        var btnRunRect = btnRun.GetComponent<RectTransform>();
        btnRunRect.anchorMin = new Vector2(0, 0);
        btnRunRect.anchorMax = new Vector2(1, 1);
        btnRunRect.offsetMin = Vector2.zero;
        btnRunRect.offsetMax = Vector2.zero;
        btnRun.GetComponentInChildren<TextMeshProUGUI>().fontSize = 24;

        // ── Create Template Prefab for Queue Items ───────────────
        GameObject templateObj = new GameObject("QueueItemTemplate");
        templateObj.transform.SetParent(canvasT, false);
        templateObj.SetActive(false); // Hidden template

        var tRect = templateObj.AddComponent<RectTransform>();
        tRect.sizeDelta = new Vector2(120, 60);
        var tLE = templateObj.AddComponent<LayoutElement>();
        tLE.preferredWidth = 120;

        var tImg = templateObj.AddComponent<Image>();
        tImg.color = Color.white;
        var tBtn = templateObj.AddComponent<Button>();

        GameObject tTxtObj = new GameObject("Text");
        tTxtObj.transform.SetParent(templateObj.transform, false);
        var tTxt = tTxtObj.AddComponent<TextMeshProUGUI>();
        tTxt.alignment = TextAlignmentOptions.Center;
        tTxt.color = Color.white;
        tTxt.fontSize = 18;
        tTxt.fontStyle = FontStyles.Bold;
        var tTxtRect = tTxtObj.GetComponent<RectTransform>();
        tTxtRect.anchorMin = Vector2.zero;
        tTxtRect.anchorMax = Vector2.one;
        tTxtRect.offsetMin = Vector2.zero;
        tTxtRect.offsetMax = Vector2.zero;

        // ── Attach Logic Script ──────────────────────────────────
        CommandQueueUI uiLogic = gameplayPanel.AddComponent<CommandQueueUI>();

        var so = new SerializedObject(uiLogic);
        so.FindProperty("queueContainer").objectReferenceValue = queueContent.transform;
        so.FindProperty("runButton").objectReferenceValue = btnRun;
        so.FindProperty("statusText").objectReferenceValue = statusTxt;
        so.FindProperty("queueItemPrefab").objectReferenceValue = templateObj;

        so.FindProperty("btnMoveForward").objectReferenceValue = btnFwd;
        so.FindProperty("btnTurnLeft").objectReferenceValue = btnLeft;
        so.FindProperty("btnTurnRight").objectReferenceValue = btnRight;
        so.FindProperty("btnInteract").objectReferenceValue = btnPick;
        so.ApplyModifiedProperties();

        Undo.RegisterCreatedObjectUndo(gameplayPanel, "Create Gameplay UI");
        Undo.RegisterCreatedObjectUndo(templateObj, "Create Queue Template");

        Debug.Log("<color=green>[UIBuilder] ✅ Gameplay UI created!</color>");
        Selection.activeGameObject = gameplayPanel;
    }

    private static Button CreateCmdBtn(Transform parent, string name, string label, Color color)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);

        var img = obj.AddComponent<Image>();
        img.color = color;

        var btn = obj.AddComponent<Button>();
        var colors = btn.colors;
        colors.highlightedColor = color * 1.2f;
        colors.pressedColor = color * 0.8f;
        btn.colors = colors;

        GameObject txtObj = new GameObject("Text");
        txtObj.transform.SetParent(obj.transform, false);
        var txt = txtObj.AddComponent<TextMeshProUGUI>();
        txt.text = label;
        txt.fontSize = 18;
        txt.fontStyle = FontStyles.Bold;
        txt.color = Color.white;
        txt.alignment = TextAlignmentOptions.Center;

        var txtRect = txtObj.GetComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.offsetMin = Vector2.zero;
        txtRect.offsetMax = Vector2.zero;

        return btn;
    }
}
#endif
