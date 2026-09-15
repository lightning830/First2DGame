using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

/// <summary>
/// A small playable first game built entirely from runtime-generated shapes.
/// This keeps the first prototype free from external art dependencies.
/// </summary>
public sealed class CrystalRushGame : MonoBehaviour
{
    private const int TargetScore = 10;
    private const float RoundDuration = 45f;
    private const float ArenaHalfWidth = 8.5f;
    private const float ArenaHalfHeight = 4.5f;

    private enum GameState
    {
        Title,
        Playing,
        Cleared,
        GameOver
    }

    private GameState state;
    private Camera gameCamera;
    private Transform worldRoot;
    private Transform crystalRoot;
    private PlayerController player;
    private ChaserController enemy;
    private readonly List<CrystalPickup> crystals = new List<CrystalPickup>();

    private float timeRemaining;
    private int score;

    private GameObject hudRoot;
    private GameObject overlayRoot;
    private TextMeshProUGUI scoreText;
    private TextMeshProUGUI timerText;
    private TextMeshProUGUI overlayTitle;
    private TextMeshProUGUI overlayBody;
    private TextMeshProUGUI actionButtonText;
    private UnityEngine.UI.Button actionButton;

    private static readonly Color Navy = new Color(0.025f, 0.045f, 0.10f, 1f);
    private static readonly Color PanelBlue = new Color(0.055f, 0.10f, 0.20f, 0.96f);
    private static readonly Color Cyan = new Color(0.16f, 0.90f, 1f, 1f);
    private static readonly Color Gold = new Color(1f, 0.72f, 0.18f, 1f);
    private static readonly Color Red = new Color(1f, 0.25f, 0.34f, 1f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindAnyObjectByType<CrystalRushGame>() != null)
        {
            return;
        }

        var gameObject = new GameObject("CrystalRushGame");
        gameObject.AddComponent<CrystalRushGame>();
    }

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        BuildCamera();
        BuildWorld();
        BuildUi();
        ShowTitle();
    }

    private void Update()
    {
        if (state == GameState.Playing)
        {
            timeRemaining -= Time.deltaTime;
            RefreshHud();
            CheckPickups();
            CheckEnemyCollision();

            if (timeRemaining <= 0f)
            {
                FinishRound(false, "TIME'S UP");
            }
        }

        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame && state != GameState.Playing)
        {
            BeginRound();
        }

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame && state == GameState.Playing)
        {
            FinishRound(false, "ROUND PAUSED");
        }
    }

    public bool IsPlaying => state == GameState.Playing;

    public Vector2 PlayerPosition => player == null ? Vector2.zero : (Vector2)player.transform.position;

    private void BuildCamera()
    {
        foreach (var existingCamera in FindObjectsByType<Camera>())
        {
            existingCamera.enabled = false;
        }

        var cameraObject = new GameObject("CrystalRushCamera");
        cameraObject.transform.SetParent(transform);
        cameraObject.transform.position = new Vector3(0f, 0f, -10f);
        gameCamera = cameraObject.AddComponent<Camera>();
        gameCamera.orthographic = true;
        gameCamera.orthographicSize = 5.5f;
        gameCamera.clearFlags = CameraClearFlags.SolidColor;
        gameCamera.backgroundColor = Navy;
        gameCamera.tag = "MainCamera";
    }

    private void BuildWorld()
    {
        worldRoot = new GameObject("CrystalRushWorld").transform;
        worldRoot.SetParent(transform);
        crystalRoot = new GameObject("Crystals").transform;
        crystalRoot.SetParent(worldRoot);

        CreateBlock("ArenaBackground", Vector2.zero, new Vector2(18f, 11f), Navy, 2, -10);
        CreateBlock("TopBorder", new Vector2(0f, ArenaHalfHeight), new Vector2(17.3f, 0.10f), Cyan, 1, 10);
        CreateBlock("BottomBorder", new Vector2(0f, -ArenaHalfHeight), new Vector2(17.3f, 0.10f), Cyan, 1, 10);
        CreateBlock("LeftBorder", new Vector2(-ArenaHalfWidth, 0f), new Vector2(0.10f, 9.1f), Cyan, 1, 10);
        CreateBlock("RightBorder", new Vector2(ArenaHalfWidth, 0f), new Vector2(0.10f, 9.1f), Cyan, 1, 10);

        for (var x = -7; x <= 7; x += 2)
        {
            CreateBlock($"GridLineX{x}", new Vector2(x, 0f), new Vector2(0.035f, 9.0f), new Color(0.12f, 0.30f, 0.40f, 0.16f), 1, -5);
        }

        for (var y = -3; y <= 3; y += 2)
        {
            CreateBlock($"GridLineY{y}", new Vector2(0f, y), new Vector2(17.0f, 0.035f), new Color(0.12f, 0.30f, 0.40f, 0.16f), 1, -5);
        }
    }

    private void BuildUi()
    {
        var canvasObject = new GameObject("CrystalRushCanvas");
        canvasObject.transform.SetParent(transform);
        var canvas = canvasObject.AddComponent<UnityEngine.Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        var scaler = canvasObject.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        if (FindAnyObjectByType<EventSystem>() == null)
        {
            var eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.transform.SetParent(transform);
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<InputSystemUIInputModule>();
        }

        hudRoot = CreatePanel("Hud", canvasObject.transform, new Color(0.02f, 0.06f, 0.12f, 0.92f));
        StretchTop(hudRoot.GetComponent<RectTransform>(), 116f);

        var title = CreateText("HudTitle", hudRoot.transform, "CRYSTAL RUSH", 30f, Cyan, FontStyles.Bold);
        Anchor(title.rectTransform, new Vector2(0f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, 0.5f), new Vector2(48f, 0f), new Vector2(460f, 0f));
        title.alignment = TextAlignmentOptions.MidlineLeft;

        scoreText = CreateText("Score", hudRoot.transform, "CRYSTALS  0 / 10", 28f, Color.white, FontStyles.Bold);
        Anchor(scoreText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.75f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        scoreText.alignment = TextAlignmentOptions.Center;

        timerText = CreateText("Timer", hudRoot.transform, "TIME  45", 28f, Gold, FontStyles.Bold);
        Anchor(timerText.rectTransform, new Vector2(0.75f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(-48f, 0f), new Vector2(360f, 0f));
        timerText.alignment = TextAlignmentOptions.MidlineRight;

        var hint = CreateText("Hint", canvasObject.transform, "WASD / ARROW KEYS   •   COLLECT EVERY CRYSTAL   •   R TO RESTART", 20f, new Color(0.70f, 0.85f, 0.92f, 0.86f), FontStyles.Normal);
        Anchor(hint.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 22f), new Vector2(0f, 42f));
        hint.alignment = TextAlignmentOptions.Center;

        overlayRoot = CreatePanel("Overlay", canvasObject.transform, new Color(0.01f, 0.025f, 0.07f, 0.78f));
        StretchFull(overlayRoot.GetComponent<RectTransform>());

        var card = CreatePanel("Card", overlayRoot.transform, PanelBlue);
        Anchor(card.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(680f, 430f));

        overlayTitle = CreateText("OverlayTitle", card.transform, "CRYSTAL RUSH", 58f, Cyan, FontStyles.Bold);
        Anchor(overlayTitle.rectTransform, new Vector2(0f, 0.62f), new Vector2(1f, 0.92f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        overlayTitle.alignment = TextAlignmentOptions.Center;

        overlayBody = CreateText("OverlayBody", card.transform, "Collect all 10 crystals before the hunter catches you.\n\nMove with WASD or the arrow keys.", 25f, Color.white, FontStyles.Normal);
        Anchor(overlayBody.rectTransform, new Vector2(0.08f, 0.28f), new Vector2(0.92f, 0.64f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        overlayBody.alignment = TextAlignmentOptions.Center;
        overlayBody.textWrappingMode = TextWrappingModes.Normal;

        actionButton = CreateButton("ActionButton", card.transform, "START GAME", BeginRound);
        Anchor(actionButton.GetComponent<RectTransform>(), new Vector2(0.22f, 0.08f), new Vector2(0.78f, 0.24f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        actionButtonText = actionButton.GetComponentInChildren<TextMeshProUGUI>();
    }

    private void BeginRound()
    {
        CleanupRound();
        state = GameState.Playing;
        timeRemaining = RoundDuration;
        score = 0;
        overlayRoot.SetActive(false);
        hudRoot.SetActive(true);

        var playerObject = CreateActor("Player", new Vector2(0f, -3.25f), Cyan, Shape.Circle, 0.72f, 20);
        player = playerObject.AddComponent<PlayerController>();
        player.Initialize(this);

        var enemyObject = CreateActor("Hunter", new Vector2(0f, 3.55f), Red, Shape.Diamond, 0.82f, 15);
        enemy = enemyObject.AddComponent<ChaserController>();
        enemy.Initialize(this);

        var positions = new[]
        {
            new Vector2(-6.4f, 2.4f), new Vector2(-3.2f, 3.1f), new Vector2(0.2f, 2.7f),
            new Vector2(3.4f, 3.2f), new Vector2(6.4f, 2.1f), new Vector2(-6.1f, -1.2f),
            new Vector2(-3.1f, -2.0f), new Vector2(0.8f, -0.8f), new Vector2(3.9f, -1.8f),
            new Vector2(6.4f, -2.8f)
        };

        foreach (var position in positions)
        {
            var crystalObject = CreateActor("Crystal", position, Gold, Shape.Diamond, 0.42f, 12);
            var crystal = crystalObject.AddComponent<CrystalPickup>();
            crystal.Initialize(this);
            crystals.Add(crystal);
        }

        RefreshHud();
    }

    private void CleanupRound()
    {
        if (player != null)
        {
            Destroy(player.gameObject);
            player = null;
        }

        if (enemy != null)
        {
            Destroy(enemy.gameObject);
            enemy = null;
        }

        foreach (var crystal in crystals)
        {
            if (crystal != null)
            {
                Destroy(crystal.gameObject);
            }
        }

        crystals.Clear();
    }

    private void CheckPickups()
    {
        if (player == null)
        {
            return;
        }

        for (var index = crystals.Count - 1; index >= 0; index--)
        {
            var crystal = crystals[index];
            if (crystal == null)
            {
                crystals.RemoveAt(index);
                continue;
            }

            if (Vector2.Distance(player.transform.position, crystal.transform.position) < 0.72f)
            {
                Destroy(crystal.gameObject);
                crystals.RemoveAt(index);
                score++;
                RefreshHud();

                if (score >= TargetScore)
                {
                    FinishRound(true, "ALL CRYSTALS SECURED");
                    return;
                }
            }
        }
    }

    private void CheckEnemyCollision()
    {
        if (player != null && enemy != null && Vector2.Distance(player.transform.position, enemy.transform.position) < 0.76f)
        {
            FinishRound(false, "THE HUNTER CAUGHT YOU");
        }
    }

    private void FinishRound(bool cleared, string reason)
    {
        state = cleared ? GameState.Cleared : GameState.GameOver;
        overlayRoot.SetActive(true);
        hudRoot.SetActive(true);
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
        hudRoot.SetActive(true);
        overlayRoot.SetActive(true);
        overlayTitle.text = "CRYSTAL RUSH";
        overlayTitle.color = Cyan;
        overlayBody.text = "Collect all 10 crystals before the hunter catches you.\n\nMove with WASD or the arrow keys.\nPress R anytime to start or restart.";
        actionButtonText.text = "START GAME";
        RefreshHud();
    }

    private void RefreshHud()
    {
        if (scoreText != null)
        {
            scoreText.text = $"CRYSTALS  {score:00} / {TargetScore:00}";
        }

        if (timerText != null)
        {
            timerText.text = $"TIME  {Mathf.CeilToInt(Mathf.Max(0f, timeRemaining)):00}";
            timerText.color = timeRemaining <= 10f && state == GameState.Playing ? Red : Gold;
        }
    }

    private GameObject CreateActor(string objectName, Vector2 position, Color color, Shape shape, float scale, int sortingOrder)
    {
        var actor = new GameObject(objectName);
        actor.transform.SetParent(worldRoot);
        actor.transform.position = new Vector3(position.x, position.y, 0f);
        actor.transform.localScale = Vector3.one * scale;
        var renderer = actor.AddComponent<SpriteRenderer>();
        renderer.sprite = CreateSprite(objectName, color, shape);
        renderer.sortingOrder = sortingOrder;
        return actor;
    }

    private GameObject CreateBlock(string objectName, Vector2 position, Vector2 scale, Color color, float z, int sortingOrder)
    {
        var block = new GameObject(objectName);
        block.transform.SetParent(worldRoot);
        block.transform.position = new Vector3(position.x, position.y, z);
        block.transform.localScale = new Vector3(scale.x, scale.y, 1f);
        var renderer = block.AddComponent<SpriteRenderer>();
        renderer.sprite = CreateSprite(objectName, color, Shape.Square);
        renderer.sortingOrder = sortingOrder;
        return block;
    }

    private static Sprite CreateSprite(string spriteName, Color color, Shape shape)
    {
        const int textureSize = 64;
        var texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false)
        {
            name = $"Runtime_{spriteName}",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        var pixels = new Color[textureSize * textureSize];
        for (var y = 0; y < textureSize; y++)
        {
            for (var x = 0; x < textureSize; x++)
            {
                var nx = (x + 0.5f) / textureSize * 2f - 1f;
                var ny = (y + 0.5f) / textureSize * 2f - 1f;
                var inside = shape switch
                {
                    Shape.Circle => nx * nx + ny * ny <= 0.88f,
                    Shape.Diamond => Mathf.Abs(nx) + Mathf.Abs(ny) <= 0.92f,
                    _ => true
                };

                pixels[y * textureSize + x] = inside ? color : new Color(0f, 0f, 0f, 0f);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, textureSize, textureSize), new Vector2(0.5f, 0.5f), textureSize);
    }

    private GameObject CreatePanel(string objectName, Transform parent, Color color)
    {
        var panel = new GameObject(objectName);
        panel.transform.SetParent(parent, false);
        var image = panel.AddComponent<UnityEngine.UI.Image>();
        image.color = color;
        image.raycastTarget = objectName == "Card";
        return panel;
    }

    private TextMeshProUGUI CreateText(string objectName, Transform parent, string content, float size, Color color, FontStyles style)
    {
        var textObject = new GameObject(objectName);
        textObject.transform.SetParent(parent, false);
        var text = textObject.AddComponent<TextMeshProUGUI>();
        text.text = content;
        text.fontSize = size;
        text.color = color;
        text.fontStyle = style;
        text.enableAutoSizing = false;
        text.raycastTarget = false;
        if (TMP_Settings.defaultFontAsset != null)
        {
            text.font = TMP_Settings.defaultFontAsset;
        }

        return text;
    }

    private UnityEngine.UI.Button CreateButton(string objectName, Transform parent, string label, Action onClick)
    {
        var buttonObject = new GameObject(objectName);
        buttonObject.transform.SetParent(parent, false);
        var image = buttonObject.AddComponent<UnityEngine.UI.Image>();
        image.color = Cyan;
        var button = buttonObject.AddComponent<UnityEngine.UI.Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(() => onClick());
        button.transition = UnityEngine.UI.Selectable.Transition.ColorTint;

        var colors = button.colors;
        colors.normalColor = Cyan;
        colors.highlightedColor = Color.white;
        colors.pressedColor = Gold;
        colors.selectedColor = Cyan;
        button.colors = colors;

        var text = CreateText("Label", buttonObject.transform, label, 25f, Navy, FontStyles.Bold);
        StretchFull(text.rectTransform);
        text.alignment = TextAlignmentOptions.Center;
        return button;
    }

    private static void StretchFull(RectTransform rectTransform)
    {
        Anchor(rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
    }

    private static void StretchTop(RectTransform rectTransform, float height)
    {
        Anchor(rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, height));
    }

    private static void Anchor(RectTransform rectTransform, Vector2 min, Vector2 max, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rectTransform.anchorMin = min;
        rectTransform.anchorMax = max;
        rectTransform.pivot = pivot;
        rectTransform.anchoredPosition = position;
        rectTransform.sizeDelta = size;
    }

    private enum Shape
    {
        Square,
        Circle,
        Diamond
    }
}

public sealed class PlayerController : MonoBehaviour
{
    private CrystalRushGame game;
    private const float MoveSpeed = 5.5f;

    public void Initialize(CrystalRushGame owner)
    {
        game = owner;
    }

    private void Update()
    {
        if (game == null || !game.IsPlaying)
        {
            return;
        }

        var input = ReadInput();
        if (input.sqrMagnitude > 1f)
        {
            input.Normalize();
        }

        var position = transform.position + (Vector3)(input * (MoveSpeed * Time.deltaTime));
        position.x = Mathf.Clamp(position.x, -8.05f, 8.05f);
        position.y = Mathf.Clamp(position.y, -4.05f, 4.05f);
        transform.position = position;
    }

    private static Vector2 ReadInput()
    {
        if (Keyboard.current == null)
        {
            return Vector2.zero;
        }

        var horizontal = 0f;
        var vertical = 0f;
        if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) horizontal -= 1f;
        if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) horizontal += 1f;
        if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) vertical -= 1f;
        if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) vertical += 1f;
        return new Vector2(horizontal, vertical);
    }
}

public sealed class ChaserController : MonoBehaviour
{
    private CrystalRushGame game;
    private const float ChaseSpeed = 1.65f;

    public void Initialize(CrystalRushGame owner)
    {
        game = owner;
    }

    private void Update()
    {
        if (game == null || !game.IsPlaying)
        {
            return;
        }

        var direction = game.PlayerPosition - (Vector2)transform.position;
        if (direction.sqrMagnitude > 0.01f)
        {
            transform.position += (Vector3)(direction.normalized * (ChaseSpeed * Time.deltaTime));
        }
    }
}

public sealed class CrystalPickup : MonoBehaviour
{
    private CrystalRushGame game;
    private Vector3 startPosition;

    public void Initialize(CrystalRushGame owner)
    {
        game = owner;
        startPosition = transform.position;
    }

    private void Update()
    {
        if (game == null || !game.IsPlaying)
        {
            return;
        }

        var pulse = 1f + Mathf.Sin(Time.time * 4f + startPosition.x) * 0.10f;
        transform.localScale = Vector3.one * (0.42f * pulse);
        transform.Rotate(0f, 0f, 70f * Time.deltaTime);
    }
}
