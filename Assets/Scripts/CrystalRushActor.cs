using UnityEngine;

/// <summary>Visual data owned by a reusable player, hunter, or crystal prefab.</summary>
public sealed class CrystalRushActor : MonoBehaviour
{
    public enum Shape { Circle, Diamond }

    [SerializeField] private Shape shape;
    [SerializeField] private Color tint = Color.white;
    [SerializeField] private float baseScale = 1f;
    [SerializeField] private int sortingOrder;

    private SpriteRenderer spriteRenderer;
    private Vector3 initialPosition;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null) spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        ApplyVisuals();
        ResetVisualState();
    }

    public void ResetVisualState()
    {
        initialPosition = transform.position;
        transform.localScale = Vector3.one * baseScale;
        transform.rotation = Quaternion.identity;
    }

    public void AnimateCrystal(float time, float deltaTime)
    {
        var pulse = 1f + Mathf.Sin(time * 4f + initialPosition.x) * 0.10f;
        transform.localScale = Vector3.one * (baseScale * pulse);
        transform.Rotate(0f, 0f, 70f * deltaTime);
    }

    public static CrystalRushActor CreateRuntime(string name, Transform parent, Vector3 position, Shape shape, Color tint, float scale, int order)
    {
        var actor = new GameObject(name).AddComponent<CrystalRushActor>();
        actor.transform.SetParent(parent);
        actor.transform.position = position;
        actor.shape = shape;
        actor.tint = tint;
        actor.baseScale = scale;
        actor.sortingOrder = order;
        actor.ApplyVisuals();
        actor.ResetVisualState();
        return actor;
    }

    private void ApplyVisuals()
    {
        spriteRenderer.sprite = CreateSprite(name, tint, shape);
        spriteRenderer.sortingOrder = sortingOrder;
    }

    private static Sprite CreateSprite(string spriteName, Color color, Shape shape)
    {
        const int size = 64;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = $"Runtime_{spriteName}", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color[size * size];
        for (var y = 0; y < size; y++)
        for (var x = 0; x < size; x++)
        {
            var nx = (x + 0.5f) / size * 2f - 1f;
            var ny = (y + 0.5f) / size * 2f - 1f;
            var inside = shape == Shape.Circle ? nx * nx + ny * ny <= 0.88f : Mathf.Abs(nx) + Mathf.Abs(ny) <= 0.92f;
            pixels[y * size + x] = inside ? color : Color.clear;
        }
        texture.SetPixels(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), Vector2.one * 0.5f, size);
    }
}
