using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[RequireComponent(typeof(RectTransform))]
public class ShowroomCursor : MonoBehaviour
{
    private Image _cursorImage;
    private RectTransform _rectTransform;

    public float SmoothSpeed = 25f;
    public float NormalSize = 40f;
    public float PinchSize = 25f;
    public Color NormalColor = new Color(1f, 1f, 1f, 0.8f);
    public Color PinchColor = new Color(0.2f, 0.8f, 1f, 0.9f);

    private Vector2 _targetPosition;
    private bool _isPinching;

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        _cursorImage = gameObject.AddComponent<Image>();
        
        // Generate a smooth circular soft dot cursor
        _cursorImage.sprite = CreateCircleSprite();
        _cursorImage.color = NormalColor;
        _cursorImage.raycastTarget = false; // Important: do not block UI raycasts

        _rectTransform.sizeDelta = new Vector2(NormalSize, NormalSize);
        _rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        _rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        _rectTransform.pivot = new Vector2(0.5f, 0.5f);
    }

    private void Update()
    {
        // Always stay on top of all other Canvas elements
        transform.SetAsLastSibling();

        // Smooth position
        _rectTransform.anchoredPosition = Vector2.Lerp(_rectTransform.anchoredPosition, _targetPosition, Time.deltaTime * SmoothSpeed);

        // Smooth size
        float targetSize = _isPinching ? PinchSize : NormalSize;
        Vector2 targetSizeVec = new Vector2(targetSize, targetSize);
        _rectTransform.sizeDelta = Vector2.Lerp(_rectTransform.sizeDelta, targetSizeVec, Time.deltaTime * SmoothSpeed);

        // Smooth color
        Color targetColor = _isPinching ? PinchColor : NormalColor;
        _cursorImage.color = Color.Lerp(_cursorImage.color, targetColor, Time.deltaTime * (SmoothSpeed * 0.5f));
    }

    public void SetColors(Color baseColor, float darkenMultiplier)
    {
        NormalColor = baseColor;
        PinchColor = new Color(baseColor.r * darkenMultiplier, baseColor.g * darkenMultiplier, baseColor.b * darkenMultiplier, baseColor.a);
    }

    public void UpdateState(Vector2 screenPosition, bool isPinching)
    {
        // Scale screen position to canvas position if canvas scaler is used
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null && canvas.renderMode != RenderMode.WorldSpace)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.GetComponent<RectTransform>(), 
                screenPosition, 
                canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, 
                out Vector2 localPoint);
            
            _targetPosition = localPoint; 
        }
        else
        {
            _targetPosition = screenPosition;
        }

        _isPinching = isPinching;
    }

    private Sprite CreateCircleSprite()
    {
        int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Vector2 center = new Vector2(size / 2f, size / 2f);
        float radius = size / 2f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                float alpha = 1f - Mathf.Clamp01(dist / radius);
                // Make it sharper
                alpha = Mathf.Pow(alpha, 0.5f);
                if (dist > radius) alpha = 0f;

                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }
}
