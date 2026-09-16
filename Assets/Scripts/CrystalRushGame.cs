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

    private Sprite playerSprite;
    private Sprite enemySprite;
    private Sprite crystalSprite;
    private Sprite glowSprite;
    private Sprite burstSprite;
    private Sprite starSmallSprite;
    private Sprite starMediumSprite;
    private Sprite starLargeSprite;
    private Sprite stationSprite;
    private Sprite meteorSprite;
    private Sprite uiPanelSprite;
    private Sprite uiButtonSprite;
    private Sprite uiBarSprite;

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
        LoadVisualAssets();
        BuildCamera();
        BuildWorld();
        BuildUi();
        ShowTitle();
    }

    private void LoadVisualAssets()
    {
        playerSprite = LoadSprite("CrystalRush/Art/Space/ship_A");
        enemySprite = LoadSprite("CrystalRush/Art/Space/enemy_A");
        crystalSprite = LoadSprite("CrystalRush/Art/Space/effect_yellow");
        starSmallSprite = LoadSprite("CrystalRush/Art/Space/star_small");
        starMediumSprite = LoadSprite("CrystalRush/Art/Space/star_medium");
        starLargeSprite = LoadSprite("CrystalRush/Art/Space/star_large");
        stationSprite = LoadSprite("CrystalRush/Art/Space/station_A");
        meteorSprite = LoadSprite("CrystalRush/Art/Space/meteor_detailedSmall");
        glowSprite = LoadSprite("CrystalRush/Art/VFX/light_03");
        burstSprite = LoadSprite("CrystalRush/Art/VFX/star_05");
        uiPanelSprite = LoadSprite("CrystalRush/Art/UI/button_large");
        uiButtonSprite = LoadSprite("CrystalRush/Art/UI/button_small");
        uiBarSprite = LoadSprite("CrystalRush/Art/UI/bar_large");
    }

    private static Sprite LoadSprite(string resourcePath)
    {
        var texture = Resources.Load<Texture2D>(resourcePath);
        if (texture == null)
        {
            return null;
        }

        texture.filterMode = FilterMode.Bilinear;
        return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
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
        CreateBlock("ArenaCore", Vector2.zero, new Vector2(17.25f, 9.15f), new Color(0.035f, 0.09f, 0.17f, 1f), 1, -9);
        BuildBackgroundDecorations();
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

    private void BuildBackgroundDecorations()
    {
        var random = new System.Random(2409);
        for (var index = 0; index < 30; index++)
        {
            var position = new Vector2(
                Mathf.Lerp(-8.0f, 8.0f, (float)random.NextDouble()),
                Mathf.Lerp(-3.85f, 3.85f, (float)random.NextDouble()));
            var sprite = index % 5 == 0 ? starLargeSprite : index % 2 == 0 ? starMediumSprite : starSmallSprite;
            var size = index % 5 == 0 ? 0.22f : index % 2 == 0 ? 0.14f : 0.09f;
            var alpha = index % 3 == 0 ? 0.55f : 0.30f;
            CreateDecorSprite($"Star{index:00}", position, sprite, size, new Color(0.46f, 0.88f, 1f, alpha), -8);
        }

        CreateDecorSprite("StationLeft", new Vector2(-7.0f, 3.45f), stationSprite, 0.72f, new Color(0.25f, 0.76f, 1f, 0.20f), -7);
        CreateDecorSprite("StationRight", new Vector2(6.9f, -3.25f), stationSprite, 0.56f, new Color(0.86f, 0.30f, 1f, 0.18f), -7);
        CreateDecorSprite("MeteorLeft", new Vector2(-7.5f, -2.75f), meteorSprite, 0.42f, new Color(1f, 0.55f, 0.22f, 0.24f), -7);
        CreateDecorSprite("MeteorRight", new Vector2(7.65f, 2.55f), meteorSprite, 0.34f, new Color(0.55f, 0.72f, 1f, 0.20f), -7);
    }

    private void CreateDecorSprite(string objectName, Vector2 position, Sprite sprite, float worldSize, Color color, int sortingOrder)
    {
        if (sprite == null)
        {
            return;
        }

        var decoration = new GameObject(objectName);
        decoration.transform.SetParent(worldRoot);
        decoration.transform.position = new Vector3(position.x, position.y, 0.5f);
        var renderer = decoration.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;
        SetWorldSize(decoration.transform, sprite, worldSize);
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

        var hudAccent = CreatePanel("HudAccent", hudRoot.transform, Cyan);
        Anchor(hudAccent.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 4f));

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
        overlayBody.color = Navy;

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

        var playerObject = CreateActor("Player", new Vector2(0f, -3.25f), Cyan, Shape.Circle, 0.86f, 20);
        player = playerObject.AddComponent<PlayerController>();
        player.Initialize(this);

        var enemyObject = CreateActor("Hunter", new Vector2(0f, 3.55f), Red, Shape.Diamond, 0.92f, 15);
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
            var crystalObject = CreateActor("Crystal", position, Gold, Shape.Diamond, 0.55f, 12);
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
                SpawnBurst(crystal.transform.position, Gold, 9);
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
            SpawnBurst(player.transform.position, Red, 14);
            FinishRound(false, "THE HUNTER CAUGHT YOU");
        }
    }

    private void FinishRound(bool cleared, string reason)
    {
        state = cleared ? GameState.Cleared : GameState.GameOver;
        SpawnBurst(player == null ? Vector2.zero : player.transform.position, cleared ? Gold : Red, 18);
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
        var renderer = actor.AddComponent<SpriteRenderer>();
        renderer.sprite = GetActorSprite(objectName) ?? CreateSprite(objectName, color, shape);
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;

        SetWorldSize(actor.transform, renderer.sprite, scale);

        if (glowSprite != null && objectName != "Crystal")
        {
            var glow = new GameObject("Glow");
            glow.transform.SetParent(actor.transform, false);
            var glowRenderer = glow.AddComponent<SpriteRenderer>();
            glowRenderer.sprite = glowSprite;
            glowRenderer.color = new Color(color.r, color.g, color.b, 0.22f);
            glowRenderer.sortingOrder = sortingOrder - 1;
            SetWorldSize(glow.transform, glowSprite, scale * 1.65f);
        }

        return actor;
    }

    private Sprite GetActorSprite(string objectName)
    {
        switch (objectName)
        {
            case "Player":
                return playerSprite;
            case "Hunter":
                return enemySprite;
            case "Crystal":
                return crystalSprite;
            default:
                return null;
        }
    }

    private static void SetWorldSize(Transform target, Sprite sprite, float worldSize)
    {
        if (target == null || sprite == null)
        {
            return;
        }

        var sourceSize = Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
        if (sourceSize > 0.001f)
        {
            target.localScale = Vector3.one * (worldSize / sourceSize);
        }
    }

    private void SpawnBurst(Vector2 position, Color color, int count)
    {
        var sprite = burstSprite ?? crystalSprite;
        if (sprite == null)
        {
            return;
        }

        for (var index = 0; index < count; index++)
        {
            var particleObject = new GameObject("BurstParticle");
            particleObject.transform.SetParent(worldRoot);
            particleObject.transform.position = new Vector3(position.x, position.y, -0.2f);
            var renderer = particleObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = 35;

            var angle = (Mathf.PI * 2f * index / count) + UnityEngine.Random.Range(-0.22f, 0.22f);
            var velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * UnityEngine.Random.Range(1.0f, 2.5f);
            var particle = particleObject.AddComponent<BurstParticle>();
            particle.Initialize(velocity, UnityEngine.Random.Range(0.30f, 0.55f), UnityEngine.Random.Range(0.10f, 0.22f));
        }
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
        if (objectName == "Card" && uiPanelSprite != null)
        {
            image.sprite = uiPanelSprite;
            image.color = Color.white;
        }
        else if (objectName == "Hud" && uiBarSprite != null)
        {
            image.sprite = uiBarSprite;
            image.color = new Color(0.16f, 0.50f, 0.72f, 0.92f);
        }
        else
        {
            image.color = color;
        }

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
        image.sprite = uiButtonSprite;
        image.color = uiButtonSprite == null ? Cyan : Color.white;
        var button = buttonObject.AddComponent<UnityEngine.UI.Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(() => onClick());
        button.transition = UnityEngine.UI.Selectable.Transition.ColorTint;

        var colors = button.colors;
        colors.normalColor = uiButtonSprite == null ? Cyan : new Color(0.44f, 0.88f, 1f, 1f);
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

        if (input.sqrMagnitude > 0.01f)
        {
            var angle = Mathf.Atan2(input.y, input.x) * Mathf.Rad2Deg - 90f;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }
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
            var angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }
    }
}

public sealed class CrystalPickup : MonoBehaviour
{
    private CrystalRushGame game;
    private Vector3 startPosition;
    private Vector3 baseScale;

    public void Initialize(CrystalRushGame owner)
    {
        game = owner;
        startPosition = transform.position;
        baseScale = transform.localScale;
    }

    private void Update()
    {
        if (game == null || !game.IsPlaying)
        {
            return;
        }

        var pulse = 1f + Mathf.Sin(Time.time * 4f + startPosition.x) * 0.10f;
        transform.localScale = baseScale * pulse;
        transform.Rotate(0f, 0f, 70f * Time.deltaTime);
    }
}

public sealed class BurstParticle : MonoBehaviour
{
    private Vector2 velocity;
    private float remaining;
    private float lifetime;
    private SpriteRenderer spriteRenderer;
    private Color baseColor;

    public void Initialize(Vector2 particleVelocity, float particleLifetime, float worldSize)
    {
        velocity = particleVelocity;
        lifetime = particleLifetime;
        remaining = particleLifetime;
        spriteRenderer = GetComponent<SpriteRenderer>();
        baseColor = spriteRenderer.color;

        if (spriteRenderer.sprite != null)
        {
            var sourceSize = Mathf.Max(spriteRenderer.sprite.bounds.size.x, spriteRenderer.sprite.bounds.size.y);
            if (sourceSize > 0.001f)
            {
                transform.localScale = Vector3.one * (worldSize / sourceSize);
            }
        }
    }

    private void Update()
    {
        transform.position += (Vector3)(velocity * Time.deltaTime);
        velocity *= 0.94f;
        transform.Rotate(0f, 0f, 180f * Time.deltaTime);
        remaining -= Time.deltaTime;

        if (spriteRenderer != null)
        {
            var alpha = Mathf.Clamp01(remaining / lifetime);
            spriteRenderer.color = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * alpha);
        }

        if (remaining <= 0f)
        {
            Destroy(gameObject);
        }
    }
}
