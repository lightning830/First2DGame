using UnityEngine;

/// <summary>Creates the arena artwork under the scene's Arena object.</summary>
public sealed class ArenaPresentation : MonoBehaviour
{
    private static readonly Color Navy = new(0.025f, 0.045f, 0.10f, 1f);
    private static readonly Color Cyan = new(0.16f, 0.90f, 1f, 1f);
    private Sprite square;

    private void Awake()
    {
        square = CreateSquare();
        CreateBlock("ArenaBackground", Vector2.zero, new Vector2(18f, 11f), Navy, 2, -10);
        CreateBlock("TopBorder", new Vector2(0f, 4.5f), new Vector2(17.3f, 0.10f), Cyan, 1, 10);
        CreateBlock("BottomBorder", new Vector2(0f, -4.5f), new Vector2(17.3f, 0.10f), Cyan, 1, 10);
        CreateBlock("LeftBorder", new Vector2(-8.5f, 0f), new Vector2(0.10f, 9.1f), Cyan, 1, 10);
        CreateBlock("RightBorder", new Vector2(8.5f, 0f), new Vector2(0.10f, 9.1f), Cyan, 1, 10);
        for (var x = -7; x <= 7; x += 2) CreateBlock($"GridLineX{x}", new Vector2(x, 0f), new Vector2(0.035f, 9.0f), new Color(0.12f, 0.30f, 0.40f, 0.16f), 1, -5);
        for (var y = -3; y <= 3; y += 2) CreateBlock($"GridLineY{y}", new Vector2(0f, y), new Vector2(17.0f, 0.035f), new Color(0.12f, 0.30f, 0.40f, 0.16f), 1, -5);
    }

    private void CreateBlock(string name, Vector2 position, Vector2 scale, Color color, float z, int order)
    {
        var block = new GameObject(name, typeof(SpriteRenderer));
        block.transform.SetParent(transform);
        block.transform.localPosition = new Vector3(position.x, position.y, z);
        block.transform.localScale = new Vector3(scale.x, scale.y, 1f);
        var renderer = block.GetComponent<SpriteRenderer>();
        renderer.sprite = square;
        renderer.color = color;
        renderer.sortingOrder = order;
    }

    private static Sprite CreateSquare()
    {
        var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), Vector2.one * 0.5f, 1f);
    }
}
