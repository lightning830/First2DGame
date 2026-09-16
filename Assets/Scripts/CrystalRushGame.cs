using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// A small playable first game assembled from a scene and reusable prefabs.
/// Runtime code owns the round state while the Unity hierarchy owns the layout.
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
    private Transform crystalSpawnRoot;
    private Transform playerSpawn;
    private Transform enemySpawn;
    private PlayerController player;
    private ChaserController enemy;
    private readonly List<CrystalPickup> crystals = new List<CrystalPickup>();

    private float timeRemaining;
    private int score;

    private GameObject hudRoot;
    private GameObject canvasRoot;
    private GameObject overlayRoot;
    private TextMeshProUGUI scoreText;
    private TextMeshProUGUI timerText;
    private TextMeshProUGUI overlayTitle;
    private TextMeshProUGUI overlayBody;
    private TextMeshProUGUI actionButtonText;
    private UnityEngine.UI.Button actionButton;
    private GameObject playerPrefab;
    private GameObject enemyPrefab;
    private GameObject crystalPrefab;

    private static readonly Color Navy = new Color(0.025f, 0.045f, 0.10f, 1f);
    private static readonly Color Cyan = new Color(0.16f, 0.90f, 1f, 1f);
    private static readonly Color Gold = new Color(1f, 0.72f, 0.18f, 1f);
    private static readonly Color Red = new Color(1f, 0.25f, 0.34f, 1f);

    private void Awake()
    {
        if (!BindSceneReferences())
        {
            enabled = false;
            return;
        }

        BuildCamera();
        BuildWorld();
        BuildUi();
        ShowTitle();
    }

    private bool BindSceneReferences()
    {
        gameCamera = GameObject.Find("Main Camera")?.GetComponent<Camera>();
        worldRoot = transform.Find("CrystalRushWorld");
        crystalRoot = worldRoot?.Find("Crystals");
        crystalSpawnRoot = worldRoot?.Find("CrystalSpawns");
        playerSpawn = worldRoot?.Find("PlayerSpawn");
        enemySpawn = worldRoot?.Find("HunterSpawn");
        canvasRoot = transform.Find("CrystalRushCanvas")?.gameObject;
        hudRoot = canvasRoot?.transform.Find("Hud")?.gameObject;
        overlayRoot = canvasRoot?.transform.Find("Overlay")?.gameObject;
        scoreText = canvasRoot?.transform.Find("Hud/Score")?.GetComponent<TextMeshProUGUI>();
        timerText = canvasRoot?.transform.Find("Hud/Timer")?.GetComponent<TextMeshProUGUI>();
        overlayTitle = canvasRoot?.transform.Find("Overlay/Card/OverlayTitle")?.GetComponent<TextMeshProUGUI>();
        overlayBody = canvasRoot?.transform.Find("Overlay/Card/OverlayBody")?.GetComponent<TextMeshProUGUI>();
        actionButton = canvasRoot?.transform.Find("Overlay/Card/ActionButton")?.GetComponent<UnityEngine.UI.Button>();
        actionButtonText = actionButton?.GetComponentInChildren<TextMeshProUGUI>();
        playerPrefab = Resources.Load<GameObject>("CrystalRush/Prefabs/Player");
        enemyPrefab = Resources.Load<GameObject>("CrystalRush/Prefabs/Hunter");
        crystalPrefab = Resources.Load<GameObject>("CrystalRush/Prefabs/Crystal");

        if (gameCamera == null || worldRoot == null || crystalRoot == null || crystalSpawnRoot == null || playerSpawn == null || enemySpawn == null || canvasRoot == null ||
            hudRoot == null || overlayRoot == null || scoreText == null || timerText == null || overlayTitle == null ||
            overlayBody == null || actionButton == null || actionButtonText == null || playerPrefab == null ||
            enemyPrefab == null || crystalPrefab == null)
        {
            Debug.LogError("CrystalRushGame scene references or prefabs are incomplete. Check the CrystalRush hierarchy and Resources/CrystalRush/Prefabs.", this);
            return false;
        }

        actionButton.onClick.RemoveAllListeners();
        actionButton.onClick.AddListener(BeginRound);
        foreach (var text in canvasRoot.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (text.font == null && TMP_Settings.defaultFontAsset != null)
            {
                text.font = TMP_Settings.defaultFontAsset;
            }
        }
        return true;
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
        gameCamera.enabled = true;
        gameCamera.orthographic = true;
        gameCamera.orthographicSize = 5.5f;
        gameCamera.clearFlags = CameraClearFlags.SolidColor;
        gameCamera.backgroundColor = Navy;
        gameCamera.tag = "MainCamera";
    }

    private void BuildWorld()
    {
        ConfigureBlock("ArenaBackground", Vector2.zero, new Vector2(18f, 11f), Navy, 2, -10);
        ConfigureBlock("TopBorder", new Vector2(0f, ArenaHalfHeight), new Vector2(17.3f, 0.10f), Cyan, 1, 10);
        ConfigureBlock("BottomBorder", new Vector2(0f, -ArenaHalfHeight), new Vector2(17.3f, 0.10f), Cyan, 1, 10);
        ConfigureBlock("LeftBorder", new Vector2(-ArenaHalfWidth, 0f), new Vector2(0.10f, 9.1f), Cyan, 1, 10);
        ConfigureBlock("RightBorder", new Vector2(ArenaHalfWidth, 0f), new Vector2(0.10f, 9.1f), Cyan, 1, 10);

        for (var x = -7; x <= 7; x += 2)
        {
            ConfigureBlock($"GridLineX{x}", new Vector2(x, 0f), new Vector2(0.035f, 9.0f), new Color(0.12f, 0.30f, 0.40f, 0.16f), 1, -5);
        }

        for (var y = -3; y <= 3; y += 2)
        {
            ConfigureBlock($"GridLineY{y}", new Vector2(0f, y), new Vector2(17.0f, 0.035f), new Color(0.12f, 0.30f, 0.40f, 0.16f), 1, -5);
        }
    }

    private void BuildUi()
    {
        var canvas = canvasRoot.GetComponent<UnityEngine.Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
    }

    private void BeginRound()
    {
        CleanupRound();
        state = GameState.Playing;
        timeRemaining = RoundDuration;
        score = 0;
        overlayRoot.SetActive(false);
        hudRoot.SetActive(true);

        var playerObject = CreateActor(playerPrefab, "Player", playerSpawn.position, Cyan, Shape.Circle, 0.72f, 20);
        player = playerObject.GetComponent<PlayerController>() ?? playerObject.AddComponent<PlayerController>();
        player.Initialize(this);

        var enemyObject = CreateActor(enemyPrefab, "Hunter", enemySpawn.position, Red, Shape.Diamond, 0.82f, 15);
        enemy = enemyObject.GetComponent<ChaserController>() ?? enemyObject.AddComponent<ChaserController>();
        enemy.Initialize(this);

        foreach (Transform spawn in crystalSpawnRoot)
        {
            var crystalObject = CreateActor(crystalPrefab, "Crystal", spawn.position, Gold, Shape.Diamond, 0.42f, 12);
            var crystal = crystalObject.GetComponent<CrystalPickup>() ?? crystalObject.AddComponent<CrystalPickup>();
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

    private GameObject CreateActor(GameObject prefab, string objectName, Vector2 position, Color color, Shape shape, float scale, int sortingOrder)
    {
        var actor = Instantiate(prefab, worldRoot);
        actor.name = objectName;
        actor.transform.position = new Vector3(position.x, position.y, 0f);
        actor.transform.localScale = Vector3.one * scale;
        var renderer = actor.GetComponent<SpriteRenderer>();
        renderer.sprite = CreateSprite(objectName, color, shape);
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;
        return actor;
    }

    private void ConfigureBlock(string objectName, Vector2 position, Vector2 scale, Color color, float z, int sortingOrder)
    {
        var block = worldRoot.Find(objectName)?.gameObject;
        if (block == null)
        {
            Debug.LogError($"Missing scene object: {objectName}", this);
            return;
        }

        block.transform.position = new Vector3(position.x, position.y, z);
        block.transform.localScale = new Vector3(scale.x, scale.y, 1f);
        var renderer = block.GetComponent<SpriteRenderer>();
        renderer.sprite = CreateSprite(objectName, color, Shape.Square);
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;
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
