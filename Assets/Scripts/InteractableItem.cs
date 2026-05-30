using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using UnityEngine.Rendering;

public enum ObservationPresentationMode
{
    Object,
    Image
}

public enum ObservationFacingAxis
{
    PositiveZ,
    NegativeZ,
    PositiveX,
    NegativeX,
    PositiveY,
    NegativeY
}

/// <summary>
/// Core component for any interactable 3D item in the showroom.
/// Implements IHandInteractable to provide a clean contract for the interaction manager.
/// </summary>
public class InteractableItem : MonoBehaviour, IHandInteractable
{
    private const string DefaultPinchOverlayShaderName = "AliSpawn/Interaction/Pinch Overlay";
    private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");
    private static readonly int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");
    private static readonly int EdgePowerPropertyId = Shader.PropertyToID("_EdgePower");
    private static readonly int EdgeIntensityPropertyId = Shader.PropertyToID("_EdgeIntensity");

    [Header("Identity")]
    public string ItemName = "Mystery Object";
    
    [Header("Interaction")]
    public float RotationSpeed = 0.5f;
    public float ScaleSensitivity = 0.005f;

    [Header("Observation Presentation")]
    [Tooltip("Object keeps the current free-look behavior. Image stays front-facing in observation mode with a limited tilt range.")]
    public ObservationPresentationMode ObservationMode = ObservationPresentationMode.Object;
    [Tooltip("For Image mode, choose which local axis should be treated as the picture's front face.")]
    public ObservationFacingAxis ImageFrontAxis = ObservationFacingAxis.PositiveZ;
    [Range(0.0f, 45.0f)]
    public float ImageObservationMaxAngle = 15.0f;

    [Header("Rotation Inertia")]
    public float InertiaStrength = 12.0f;
    public float MinInertiaSpeed = 2.0f;
    public float InertiaDuration = 0.45f;
    public Ease InertiaEase = Ease.OutCubic;

    [Header("Pinch Feedback Overlay")]
    public bool UsePinchFeedbackOverlay = true;
    [Tooltip("Optional override. Leave empty to use the built-in pinch overlay shader.")]
    public Shader PinchFeedbackShader;
    public Color PinchFeedbackColor = new Color(1.0f, 0.84f, 0.18f, 0.38f);
    [Range(1.0f, 1.1f)] public float PinchFeedbackScale = 1.015f;
    [Range(0.5f, 8.0f)] public float PinchFeedbackEdgePower = 3.0f;
    [Range(0.0f, 2.0f)] public float PinchFeedbackEdgeIntensity = 1.0f;

    // --- IHandInteractable ---
    public string DisplayName => ItemName;
    public bool UsesImageObservationMode => ObservationMode == ObservationPresentationMode.Image;

    // --- C# Events ---
    public event System.Action OnClick;
    public event System.Action OnDoubleClick;

    public void TriggerClick()        => OnClick?.Invoke();
    public void TriggerDoubleClick()  => OnDoubleClick?.Invoke();

    // --- Private State ---
    private Color _originalColor;
    private Renderer _renderer;
    private Vector3 _originalScale;
    private Vector2 _angularVelocity;
    private bool _isGrabbed;
    private Tween _inertiaTween;
    private ItemObserver _observer;
    private int _colorPropertyId = -1;
    private Material _pinchFeedbackMaterial;
    private readonly List<Renderer> _pinchOverlayRenderers = new List<Renderer>();
    private bool _pinchOverlayInitialized;

    private void Awake()
    {
        _observer = GetComponent<ItemObserver>();
    }

    private void Start()
    {
        _renderer = GetComponent<Renderer>();
        if (_renderer != null)
        {
            Material material = _renderer.material;
            if (TryResolveColorProperty(material))
            {
                _originalColor = material.GetColor(_colorPropertyId);
            }
        }

        _originalScale = transform.localScale;
        EnsurePinchFeedbackOverlay();
        SetPinchFeedbackVisible(false);
        
        if (ShowroomRegistry.Instance != null)
            ShowroomRegistry.Instance.Register(this);
    }

    private void Update()
    {
        if (_isGrabbed)
        {
            return;
        }

        if (_angularVelocity.sqrMagnitude <= MinInertiaSpeed * MinInertiaSpeed)
        {
            _angularVelocity = Vector2.zero;
            return;
        }

        ApplyRotation(_angularVelocity * Time.deltaTime);
    }

    private void OnDestroy()
    {
        _inertiaTween?.Kill();
        DisposePinchFeedbackResources();

        if (ShowroomRegistry.Instance != null)
            ShowroomRegistry.Instance.Unregister(this);
    }

    // --- IHandInteractable implementation ---

    public void OnHoverEnter()
    {
        SetRendererColor(Color.Lerp(_originalColor, Color.white, 0.4f));
    }

    public void OnHoverExit()
    {
        SetRendererColor(_originalColor);
    }

    public void OnGrabbed()
    {
        _isGrabbed = true;
        _inertiaTween?.Kill();
        SetRendererColor(Color.Lerp(_originalColor, Color.yellow, 0.5f));
        SetPinchFeedbackVisible(true);
    }

    public void OnReleased()
    {
        _isGrabbed = false;
        bool keepFrontFacing = _observer != null && _observer.IsObserving && UsesImageObservationMode;
        if (!keepFrontFacing)
        {
            StartInertiaTween();
        }
        else
        {
            _angularVelocity = Vector2.zero;
        }

        SetRendererColor(_originalColor);
        SetPinchFeedbackVisible(false);
    }

    public void OnRotate(Vector2 deltaScreenPosition)
    {
        Vector2 rotationDelta = new Vector2(deltaScreenPosition.y * RotationSpeed, -deltaScreenPosition.x * RotationSpeed);
        _inertiaTween?.Kill();

        if (_observer != null && _observer.IsObserving && UsesImageObservationMode)
        {
            _observer.ApplyImageObservationRotation(rotationDelta);
            _angularVelocity = Vector2.zero;
            return;
        }

        ApplyRotation(rotationDelta);

        float deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
        Vector2 targetAngularVelocity = rotationDelta / deltaTime;
        _angularVelocity = Vector2.Lerp(_angularVelocity, targetAngularVelocity, Mathf.Clamp01(InertiaStrength * deltaTime));
    }

    public void OnScale(float deltaDistance)
    {
        if (_observer != null && _observer.IsObserving)
        {
            _observer.AdjustZoom(deltaDistance);
        }
        else
        {
            float scaleDelta = deltaDistance * ScaleSensitivity;
            Vector3 newScale = transform.localScale + _originalScale * scaleDelta;
            newScale.x = Mathf.Clamp(newScale.x, _originalScale.x * 0.2f, _originalScale.x * 5.0f);
            newScale.y = Mathf.Clamp(newScale.y, _originalScale.y * 0.2f, _originalScale.y * 5.0f);
            newScale.z = Mathf.Clamp(newScale.z, _originalScale.z * 0.2f, _originalScale.z * 5.0f);
            transform.localScale = newScale;
            _originalScale = newScale;
        }
    }

    public void OnSingleClick()     => TriggerClick();
    public void OnHandDoubleClick()
    {
        if (_observer == null)
        {
            _observer = GetComponent<ItemObserver>();
        }

        if (_observer != null)
        {
            _observer.ToggleObservation();
            return;
        }

        TriggerDoubleClick();
    }

    // --- Legacy shim methods ---
    public void OnSelected()           => OnGrabbed();
    public void OnDeselected()         => OnReleased();
    public void ScaleItem(float delta) => OnScale(delta);
    public void Rotate(Vector2 delta)  => OnRotate(delta);

    private void ApplyRotation(Vector2 rotationDelta)
    {
        transform.Rotate(Vector3.up, rotationDelta.y, Space.World);
        transform.Rotate(Vector3.right, rotationDelta.x, Space.World);
    }

    private void OnValidate()
    {
        if (_pinchFeedbackMaterial != null)
        {
            ConfigurePinchFeedbackMaterial();
            UpdatePinchOverlayScales();
        }
    }

    private void StartInertiaTween()
    {
        if (_angularVelocity.sqrMagnitude <= MinInertiaSpeed * MinInertiaSpeed)
        {
            _angularVelocity = Vector2.zero;
            return;
        }

        _inertiaTween = DOTween.To(() => _angularVelocity, value => _angularVelocity = value, Vector2.zero, InertiaDuration)
        .SetEase(InertiaEase)
        .SetTarget(this)
        .OnComplete(() => _angularVelocity = Vector2.zero);
    }

    private bool TryResolveColorProperty(Material material)
    {
        if (material == null)
        {
            _colorPropertyId = -1;
            return false;
        }

        if (material.HasProperty(ColorPropertyId))
        {
            _colorPropertyId = ColorPropertyId;
            return true;
        }

        if (material.HasProperty(BaseColorPropertyId))
        {
            _colorPropertyId = BaseColorPropertyId;
            return true;
        }

        _colorPropertyId = -1;
        return false;
    }

    private void SetRendererColor(Color color)
    {
        if (_renderer == null || _colorPropertyId == -1)
        {
            return;
        }

        _renderer.material.SetColor(_colorPropertyId, color);
    }

    private void EnsurePinchFeedbackOverlay()
    {
        if (_pinchOverlayInitialized || !UsePinchFeedbackOverlay)
        {
            return;
        }

        Shader shader = PinchFeedbackShader != null ? PinchFeedbackShader : Shader.Find(DefaultPinchOverlayShaderName);
        if (shader == null)
        {
            Debug.LogWarning($"[InteractableItem] Pinch feedback shader not found on {name}.");
            return;
        }

        _pinchFeedbackMaterial = new Material(shader)
        {
            name = $"{name} Pinch Feedback"
        };
        ConfigurePinchFeedbackMaterial();

        Renderer[] sourceRenderers = GetComponentsInChildren<Renderer>(true);
        foreach (Renderer sourceRenderer in sourceRenderers)
        {
            Renderer overlayRenderer = CreatePinchOverlayRenderer(sourceRenderer);
            if (overlayRenderer != null)
            {
                _pinchOverlayRenderers.Add(overlayRenderer);
            }
        }

        _pinchOverlayInitialized = true;
    }

    private Renderer CreatePinchOverlayRenderer(Renderer sourceRenderer)
    {
        if (sourceRenderer == null || sourceRenderer.gameObject.name == "__PinchFeedbackOverlay")
        {
            return null;
        }

        int subMeshCount = 0;
        GameObject overlayObject = new GameObject("__PinchFeedbackOverlay");
        overlayObject.layer = sourceRenderer.gameObject.layer;
        overlayObject.transform.SetParent(sourceRenderer.transform, false);
        overlayObject.transform.localPosition = Vector3.zero;
        overlayObject.transform.localRotation = Quaternion.identity;
        overlayObject.transform.localScale = Vector3.one * PinchFeedbackScale;

        Renderer overlayRenderer = null;
        if (sourceRenderer is MeshRenderer)
        {
            MeshFilter sourceFilter = sourceRenderer.GetComponent<MeshFilter>();
            if (sourceFilter == null || sourceFilter.sharedMesh == null)
            {
                Destroy(overlayObject);
                return null;
            }

            MeshFilter overlayFilter = overlayObject.AddComponent<MeshFilter>();
            overlayFilter.sharedMesh = sourceFilter.sharedMesh;

            MeshRenderer meshRenderer = overlayObject.AddComponent<MeshRenderer>();
            overlayRenderer = meshRenderer;
            subMeshCount = Mathf.Max(1, sourceFilter.sharedMesh.subMeshCount);
        }
        else if (sourceRenderer is SkinnedMeshRenderer sourceSkinned)
        {
            if (sourceSkinned.sharedMesh == null)
            {
                Destroy(overlayObject);
                return null;
            }

            SkinnedMeshRenderer overlaySkinned = overlayObject.AddComponent<SkinnedMeshRenderer>();
            overlaySkinned.sharedMesh = sourceSkinned.sharedMesh;
            overlaySkinned.rootBone = sourceSkinned.rootBone;
            overlaySkinned.bones = sourceSkinned.bones;
            overlaySkinned.localBounds = sourceSkinned.localBounds;
            overlayRenderer = overlaySkinned;
            subMeshCount = Mathf.Max(1, sourceSkinned.sharedMesh.subMeshCount);
        }
        else if (sourceRenderer is SpriteRenderer sourceSprite)
        {
            SpriteRenderer overlaySprite = overlayObject.AddComponent<SpriteRenderer>();
            overlaySprite.sprite = sourceSprite.sprite;
            overlaySprite.drawMode = sourceSprite.drawMode;
            overlaySprite.size = sourceSprite.size;
            overlaySprite.tileMode = sourceSprite.tileMode;
            overlaySprite.maskInteraction = sourceSprite.maskInteraction;
            overlaySprite.flipX = sourceSprite.flipX;
            overlaySprite.flipY = sourceSprite.flipY;
            overlayRenderer = overlaySprite;
            subMeshCount = 1;
        }
        else
        {
            Destroy(overlayObject);
            return null;
        }

        ConfigureOverlayRenderer(sourceRenderer, overlayRenderer, subMeshCount);
        return overlayRenderer;
    }

    private void ConfigureOverlayRenderer(Renderer sourceRenderer, Renderer overlayRenderer, int subMeshCount)
    {
        overlayRenderer.enabled = false;
        overlayRenderer.shadowCastingMode = ShadowCastingMode.Off;
        overlayRenderer.receiveShadows = false;
        overlayRenderer.lightProbeUsage = LightProbeUsage.Off;
        overlayRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        overlayRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        overlayRenderer.allowOcclusionWhenDynamic = false;
        overlayRenderer.renderingLayerMask = sourceRenderer.renderingLayerMask;
        overlayRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
        overlayRenderer.sortingOrder = sourceRenderer.sortingOrder;

        Material[] overlayMaterials = new Material[subMeshCount];
        for (int i = 0; i < overlayMaterials.Length; i++)
        {
            overlayMaterials[i] = _pinchFeedbackMaterial;
        }

        overlayRenderer.sharedMaterials = overlayMaterials;
    }

    private void ConfigurePinchFeedbackMaterial()
    {
        if (_pinchFeedbackMaterial == null)
        {
            return;
        }

        if (_pinchFeedbackMaterial.HasProperty(BaseColorPropertyId))
        {
            _pinchFeedbackMaterial.SetColor(BaseColorPropertyId, PinchFeedbackColor);
        }

        if (_pinchFeedbackMaterial.HasProperty(EdgePowerPropertyId))
        {
            _pinchFeedbackMaterial.SetFloat(EdgePowerPropertyId, PinchFeedbackEdgePower);
        }

        if (_pinchFeedbackMaterial.HasProperty(EdgeIntensityPropertyId))
        {
            _pinchFeedbackMaterial.SetFloat(EdgeIntensityPropertyId, PinchFeedbackEdgeIntensity);
        }
    }

    private void UpdatePinchOverlayScales()
    {
        Vector3 overlayScale = Vector3.one * PinchFeedbackScale;
        foreach (Renderer overlayRenderer in _pinchOverlayRenderers)
        {
            if (overlayRenderer != null)
            {
                overlayRenderer.transform.localScale = overlayScale;
            }
        }
    }

    private void SetPinchFeedbackVisible(bool visible)
    {
        if (!UsePinchFeedbackOverlay)
        {
            return;
        }

        EnsurePinchFeedbackOverlay();
        foreach (Renderer overlayRenderer in _pinchOverlayRenderers)
        {
            if (overlayRenderer != null)
            {
                overlayRenderer.enabled = visible;
            }
        }
    }

    private void DisposePinchFeedbackResources()
    {
        _pinchOverlayRenderers.Clear();

        if (_pinchFeedbackMaterial == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(_pinchFeedbackMaterial);
        }
        else
        {
            DestroyImmediate(_pinchFeedbackMaterial);
        }

        _pinchFeedbackMaterial = null;
    }
}
