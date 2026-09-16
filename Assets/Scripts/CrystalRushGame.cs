using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

/// <summary>
/// Owns a Crystal Rush round. The scene describes the level, and this class
/// instantiates only the actors belonging to an active round from prefabs.
/// </summary>
public sealed class CrystalRushGame : MonoBehaviour
{
    private const int TargetScore = 10;
    private const float RoundDuration = 45f;

    [Header("Scene layout")]
    [SerializeField] private Camera gameCamera;
    [SerializeField] private Transform runtimeActors;
    [SerializeField] private Transform playerSpawn;
    [SerializeField] private Transform hunterSpawn;
    [SerializeField] private Transform[] crystalSpawns;
    [SerializeField] private Transform uiRoot;

    [Header("Actor prefabs")]
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private GameObject hunterPrefab;
    [SerializeField] private GameObject crystalPrefab;

    private enum GameState { Title, Playing, Cleared, GameOver }
    private static readonly Color Navy = new(0.025f, 0.045f, 0.10f, 1f);
    private static readonly Color PanelBlue = new(0.055f, 0.10f, 0.20f, 0.96f);
    private static readonly Color Cyan = new(0.16f, 0.90f, 1f, 1f);
    private static readonly Color Gold = new(1f, 0.72f, 0.18f, 1f);
    private static readonly Color Red = new(1f, 0.25f, 0.34f, 1f);

    private readonly List<CrystalRushActor> crystals = new();
    private GameState state;
    private CrystalRushActor player;
    private CrystalRushActor hunter;
    private float timeRemaining;
    private int score;
    private GameObject hudRoot;
    private GameObject overlayRoot;
    private TextMeshProUGUI scoreText;
    private TextMeshProUGUI timerText;
    private TextMeshProUGUI overlayTitle;
    private TextMeshProUGUI overlayBody;
    private TextMeshProUGUI actionButtonText;

    public bool IsPlaying => state == GameState.Playing;
    public Vector2 PlayerPosition => player == null ? Vector2.zero : player.transform.position;

    private void Awake()
    {
        ValidateSceneReferences();
        gameCamera.enabled = true;
        gameCamera.orthographic = true;
        gameCamera.orthographicSize = 5.5f;
        gameCamera.clearFlags = CameraClearFlags.SolidColor;
        gameCamera.backgroundColor = Navy;
        BuildUi();
        ShowTitle();
    }

    private void Update()
    {
        if (state == GameState.Playing)
        {
            timeRemaining -= Time.deltaTime;
            MovePlayer();
            MoveHunter();
            foreach (var crystal in crystals) crystal?.AnimateCrystal(Time.time, Time.deltaTime);
            CheckPickups();
            CheckHunterCollision();
            RefreshHud();
            if (timeRemaining <= 0f) FinishRound(false, "TIME'S UP");
        }

        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame && state != GameState.Playing) BeginRound();
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame && state == GameState.Playing) FinishRound(false, "ROUND PAUSED");
    }

    private void ValidateSceneReferences()
    {
        if (gameCamera == null) gameCamera = Camera.main;
        if (runtimeActors == null) runtimeActors = FindTransform("RuntimeActors");
        if (playerSpawn == null) playerSpawn = FindTransform("PlayerSpawn");
        if (hunterSpawn == null) hunterSpawn = FindTransform("HunterSpawn");
        if (uiRoot == null) uiRoot = FindTransform("UI");
        if (crystalSpawns == null || crystalSpawns.Length < TargetScore)
        {
            crystalSpawns = new Transform[TargetScore];
            for (var i = 0; i < TargetScore; i++) crystalSpawns[i] = FindTransform($"CrystalSpawn{i + 1:00}");
        }
        if (gameCamera == null || runtimeActors == null || playerSpawn == null || hunterSpawn == null || uiRoot == null || crystalSpawns.Length < TargetScore)
        {
            throw new InvalidOperationException("Crystal Rush needs GameRoot's scene references assigned.");
        }
    }

    private static Transform FindTransform(string objectName) => GameObject.Find(objectName)?.transform;

    private void BeginRound()
    {
        CleanupRound();
        state = GameState.Playing;
        timeRemaining = RoundDuration;
        score = 0;
        overlayRoot.SetActive(false);
        hudRoot.SetActive(true);
        player = Spawn(playerPrefab, playerSpawn, "Player");
        hunter = Spawn(hunterPrefab, hunterSpawn, "Hunter");
        for (var i = 0; i < TargetScore; i++) crystals.Add(Spawn(crystalPrefab, crystalSpawns[i], "Crystal"));
        RefreshHud();
    }

    private CrystalRushActor Spawn(GameObject prefab, Transform spawn, string objectName)
    {
        if (prefab == null)
        {
            return objectName switch
            {
                "Player" => CrystalRushActor.CreateRuntime(objectName, runtimeActors, spawn.position, CrystalRushActor.Shape.Circle, Cyan, 0.72f, 20),
                "Hunter" => CrystalRushActor.CreateRuntime(objectName, runtimeActors, spawn.position, CrystalRushActor.Shape.Diamond, Red, 0.82f, 15),
                _ => CrystalRushActor.CreateRuntime(objectName, runtimeActors, spawn.position, CrystalRushActor.Shape.Diamond, Gold, 0.42f, 12)
            };
        }
        var actor = Instantiate(prefab, spawn.position, Quaternion.identity, runtimeActors).GetComponent<CrystalRushActor>();
        actor.name = objectName;
        actor.ResetVisualState();
        return actor;
    }

    private void CleanupRound()
    {
        if (player != null) Destroy(player.gameObject);
        if (hunter != null) Destroy(hunter.gameObject);
        foreach (var crystal in crystals) if (crystal != null) Destroy(crystal.gameObject);
        player = null;
        hunter = null;
        crystals.Clear();
    }

    private void MovePlayer()
    {
        if (player == null) return;
        var input = ReadInput();
        if (input.sqrMagnitude > 1f) input.Normalize();
        var position = player.transform.position + (Vector3)(input * (5.5f * Time.deltaTime));
        position.x = Mathf.Clamp(position.x, -8.05f, 8.05f);
        position.y = Mathf.Clamp(position.y, -4.05f, 4.05f);
        player.transform.position = position;
    }

    private void MoveHunter()
    {
        if (player == null || hunter == null) return;
        var direction = PlayerPosition - (Vector2)hunter.transform.position;
        if (direction.sqrMagnitude > 0.01f) hunter.transform.position += (Vector3)(direction.normalized * (1.65f * Time.deltaTime));
    }

    private void CheckPickups()
    {
        if (player == null) return;
        for (var i = crystals.Count - 1; i >= 0; i--)
        {
            var crystal = crystals[i];
            if (crystal == null) { crystals.RemoveAt(i); continue; }
            if (Vector2.Distance(player.transform.position, crystal.transform.position) >= 0.72f) continue;
            Destroy(crystal.gameObject);
            crystals.RemoveAt(i);
            score++;
            if (score >= TargetScore) { FinishRound(true, "ALL CRYSTALS SECURED"); return; }
        }
    }

    private void CheckHunterCollision()
    {
        if (player != null && hunter != null && Vector2.Distance(player.transform.position, hunter.transform.position) < 0.76f)
            FinishRound(false, "THE HUNTER CAUGHT YOU");
    }

    private void FinishRound(bool cleared, string reason)
    {
        state = cleared ? GameState.Cleared : GameState.GameOver;
        overlayRoot.SetActive(true);
        overlayTitle.text = cleared ? "ROUND CLEAR!" : "GAME OVER";
        overlayTitle.color = cleared ? Gold : Red;
        overlayBody.text = $"{reason}\n\nScore  {score} / {TargetScore}\nTime   {Mathf.CeilToInt(Mathf.Max(0f, timeRemaining)):00}\n\nPress R or the button to try again.";
        actionButtonText.text = "RESTART";
        RefreshHud();
    }

    private void ShowTitle()
    {
        state = GameState.Title;
        timeRemaining = RoundDuration;
        score = 0;
        overlayRoot.SetActive(true);
        overlayTitle.text = "CRYSTAL RUSH";
        overlayTitle.color = Cyan;
        overlayBody.text = "Collect all 10 crystals before the hunter catches you.\n\nMove with WASD or the arrow keys.\nPress R anytime to start or restart.";
        actionButtonText.text = "START GAME";
        RefreshHud();
    }

    private void RefreshHud()
    {
        scoreText.text = $"CRYSTALS  {score:00} / {TargetScore:00}";
        timerText.text = $"TIME  {Mathf.CeilToInt(Mathf.Max(0f, timeRemaining)):00}";
        timerText.color = timeRemaining <= 10f && state == GameState.Playing ? Red : Gold;
    }

    private void BuildUi()
    {
        var canvasObject = new GameObject("CrystalRushCanvas", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
        canvasObject.transform.SetParent(uiRoot, false);
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        if (FindAnyObjectByType<EventSystem>() == null)
        {
            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystem.transform.SetParent(uiRoot, false);
        }

        hudRoot = CreatePanel("Hud", canvasObject.transform, new Color(0.02f, 0.06f, 0.12f, 0.92f));
        StretchTop(hudRoot.GetComponent<RectTransform>(), 116f);
        var title = CreateText("HudTitle", hudRoot.transform, "CRYSTAL RUSH", 30f, Cyan, FontStyles.Bold);
        Anchor(title.rectTransform, Vector2.zero, new Vector2(0.5f, 1f), new Vector2(0f, 0.5f), new Vector2(48f, 0f), new Vector2(460f, 0f));
        title.alignment = TextAlignmentOptions.MidlineLeft;
        scoreText = CreateText("Score", hudRoot.transform, "CRYSTALS  0 / 10", 28f, Color.white, FontStyles.Bold);
        Anchor(scoreText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.75f, 1f), Vector2.one * 0.5f, Vector2.zero, Vector2.zero);
        scoreText.alignment = TextAlignmentOptions.Center;
        timerText = CreateText("Timer", hudRoot.transform, "TIME  45", 28f, Gold, FontStyles.Bold);
        Anchor(timerText.rectTransform, new Vector2(0.75f, 0f), Vector2.one, new Vector2(1f, 0.5f), new Vector2(-48f, 0f), new Vector2(360f, 0f));
        timerText.alignment = TextAlignmentOptions.MidlineRight;
        var hint = CreateText("Hint", canvasObject.transform, "WASD / ARROW KEYS   •   COLLECT EVERY CRYSTAL   •   R TO RESTART", 20f, new Color(0.70f, 0.85f, 0.92f, 0.86f), FontStyles.Normal);
        Anchor(hint.rectTransform, Vector2.zero, new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 22f), new Vector2(0f, 42f));
        hint.alignment = TextAlignmentOptions.Center;
        overlayRoot = CreatePanel("Overlay", canvasObject.transform, new Color(0.01f, 0.025f, 0.07f, 0.78f));
        StretchFull(overlayRoot.GetComponent<RectTransform>());
        var card = CreatePanel("Card", overlayRoot.transform, PanelBlue);
        Anchor(card.GetComponent<RectTransform>(), Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, new Vector2(680f, 430f));
        overlayTitle = CreateText("OverlayTitle", card.transform, "CRYSTAL RUSH", 58f, Cyan, FontStyles.Bold);
        Anchor(overlayTitle.rectTransform, new Vector2(0f, 0.62f), new Vector2(1f, 0.92f), Vector2.one * 0.5f, Vector2.zero, Vector2.zero);
        overlayTitle.alignment = TextAlignmentOptions.Center;
        overlayBody = CreateText("OverlayBody", card.transform, string.Empty, 25f, Color.white, FontStyles.Normal);
        Anchor(overlayBody.rectTransform, new Vector2(0.08f, 0.28f), new Vector2(0.92f, 0.64f), Vector2.one * 0.5f, Vector2.zero, Vector2.zero);
        overlayBody.alignment = TextAlignmentOptions.Center;
        overlayBody.textWrappingMode = TextWrappingModes.Normal;
        var button = CreateButton("ActionButton", card.transform, "START GAME", BeginRound);
        Anchor(button.GetComponent<RectTransform>(), new Vector2(0.22f, 0.08f), new Vector2(0.78f, 0.24f), Vector2.one * 0.5f, Vector2.zero, Vector2.zero);
        actionButtonText = button.GetComponentInChildren<TextMeshProUGUI>();
    }

    private static Vector2 ReadInput()
    {
        if (Keyboard.current == null) return Vector2.zero;
        var x = (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed ? 1f : 0f) - (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed ? 1f : 0f);
        var y = (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed ? 1f : 0f) - (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed ? 1f : 0f);
        return new Vector2(x, y);
    }

    private static GameObject CreatePanel(string name, Transform parent, Color color)
    {
        var panel = new GameObject(name, typeof(UnityEngine.UI.Image));
        panel.transform.SetParent(parent, false);
        panel.GetComponent<UnityEngine.UI.Image>().color = color;
        return panel;
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent, string content, float size, Color color, FontStyles style)
    {
        var text = new GameObject(name, typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        text.transform.SetParent(parent, false);
        text.text = content;
        text.fontSize = size;
        text.color = color;
        text.fontStyle = style;
        text.raycastTarget = false;
        if (TMP_Settings.defaultFontAsset != null) text.font = TMP_Settings.defaultFontAsset;
        return text;
    }

    private static UnityEngine.UI.Button CreateButton(string name, Transform parent, string label, Action onClick)
    {
        var buttonObject = new GameObject(name, typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
        buttonObject.transform.SetParent(parent, false);
        var image = buttonObject.GetComponent<UnityEngine.UI.Image>();
        image.color = Cyan;
        var button = buttonObject.GetComponent<UnityEngine.UI.Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(() => onClick());
        var text = CreateText("Label", buttonObject.transform, label, 25f, Navy, FontStyles.Bold);
        StretchFull(text.rectTransform);
        text.alignment = TextAlignmentOptions.Center;
        return button;
    }

    private static void StretchFull(RectTransform rect) => Anchor(rect, Vector2.zero, Vector2.one, Vector2.one * 0.5f, Vector2.zero, Vector2.zero);
    private static void StretchTop(RectTransform rect, float height) => Anchor(rect, new Vector2(0f, 1f), Vector2.one, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, height));
    private static void Anchor(RectTransform rect, Vector2 min, Vector2 max, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }
}
