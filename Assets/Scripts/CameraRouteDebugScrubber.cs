using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CameraRouteDebugScrubber : MonoBehaviour, IBeginDragHandler, IEndDragHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("References")]
    [SerializeField] private Slider _slider;
    [SerializeField] private Text _valueLabel;
    [SerializeField] private Text _titleLabel;
    [SerializeField] private CameraRouteController _routeController;
    [SerializeField] private Button _cameraLookToggleButton;
    [SerializeField] private Text _cameraLookToggleLabel;

    [Header("Behavior")]
    [SerializeField] private bool _pauseAutoPlayWhileDragging = true;
    [SerializeField] private bool _pauseAutoPlayOnValueChange = true;

    private bool _wasAutoPlayEnabledBeforeDrag;
    private bool _isDragging;
    private bool _isPointerHeld;

    private void Awake()
    {
        if (_slider == null)
        {
            _slider = GetComponentInChildren<Slider>(true);
        }

        if (_routeController == null)
        {
            _routeController = FindObjectOfType<CameraRouteController>();
        }

        EnsureCameraLookToggleReferences();

        if (_slider != null)
        {
            _slider.minValue = 0f;
            _slider.maxValue = 1f;
            _slider.onValueChanged.AddListener(HandleSliderValueChanged);
        }

        if (_cameraLookToggleButton != null)
        {
            _cameraLookToggleButton.onClick.AddListener(HandleCameraLookToggleClicked);
        }
    }

    private void Start()
    {
        SyncFromRouteController();
    }

    private void OnDestroy()
    {
        if (_slider != null)
        {
            _slider.onValueChanged.RemoveListener(HandleSliderValueChanged);
        }

        if (_cameraLookToggleButton != null)
        {
            _cameraLookToggleButton.onClick.RemoveListener(HandleCameraLookToggleClicked);
        }
    }

    private void Update()
    {
        if (_routeController == null)
        {
            _routeController = FindObjectOfType<CameraRouteController>();
        }

        if (_routeController == null)
        {
            return;
        }

        if (!_isDragging)
        {
            SyncFromRouteController();
        }

        UpdateLabels();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        _isDragging = true;
        PauseAutoPlayForInteraction(_pauseAutoPlayWhileDragging);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        _isDragging = false;
        ResumeAutoPlayAfterInteraction();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        _isPointerHeld = true;
        PauseAutoPlayForInteraction(_pauseAutoPlayWhileDragging || _pauseAutoPlayOnValueChange);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        _isPointerHeld = false;
        ResumeAutoPlayAfterInteraction();
    }

    private void PauseAutoPlayForInteraction(bool shouldPause)
    {
        if (_routeController == null || !shouldPause)
        {
            return;
        }

        if (!_isPointerHeld && !_isDragging)
        {
            return;
        }

        _routeController.NotifyManualInteraction();
        _wasAutoPlayEnabledBeforeDrag = _routeController.IsAutoPlayEnabled;
        _routeController.SetAutoPlay(false, false);
    }

    private void ResumeAutoPlayAfterInteraction()
    {
        if (_routeController == null)
        {
            return;
        }

        if (_isDragging || _isPointerHeld)
        {
            return;
        }

        if (_wasAutoPlayEnabledBeforeDrag)
        {
            _routeController.SetAutoPlay(true, true);
        }
    }

    private void HandleSliderValueChanged(float value)
    {
        if (_routeController == null)
        {
            return;
        }

        if (_pauseAutoPlayOnValueChange && !_isPointerHeld && !_isDragging)
        {
            _wasAutoPlayEnabledBeforeDrag = _routeController.IsAutoPlayEnabled;
            _routeController.SetAutoPlay(false, false);
        }

        _routeController.NotifyManualInteraction();
        _routeController.TrySetNormalizedPosition(value, out _);
        UpdateLabels();
    }

    private void HandleCameraLookToggleClicked()
    {
        if (_routeController == null)
        {
            _routeController = FindObjectOfType<CameraRouteController>();
        }

        if (_routeController == null)
        {
            return;
        }

        _routeController.SetGestureLookEnabled(!_routeController.IsGestureLookAvailable);
        UpdateCameraLookToggleButton();
    }

    private void SyncFromRouteController()
    {
        if (_routeController == null || _slider == null)
        {
            return;
        }

        if (_routeController.Nodes.Count <= 1)
        {
            SetSliderWithoutNotify(0f);
            return;
        }

        int currentIndex = Mathf.Clamp(_routeController.CurrentNodeIndex, 0, _routeController.Nodes.Count - 1);
        float normalized = currentIndex / (float)(_routeController.Nodes.Count - 1);
        SetSliderWithoutNotify(normalized);
    }

    private void SetSliderWithoutNotify(float value)
    {
        if (_slider == null)
        {
            return;
        }

        _slider.SetValueWithoutNotify(value);
    }

    private void UpdateLabels()
    {
        if (_titleLabel != null)
        {
            _titleLabel.text = "Camera Debug Scrubber";
        }

        if (_valueLabel == null || _routeController == null)
        {
            return;
        }

        string nodeName = _routeController.CurrentNode != null ? _routeController.CurrentNode.ResolvedDisplayName : "--";
        int nodeIndex = _routeController.CurrentNodeIndex >= 0 ? _routeController.CurrentNodeIndex + 1 : 0;
        string lookMode = _routeController.IsGestureLookAvailable ? "drag view on" : "drag view off";
        _valueLabel.text = $"t={(_slider != null ? _slider.value : 0f):0.00}  cam {nodeIndex}/{_routeController.Nodes.Count}  {nodeName}  {lookMode}";
        UpdateCameraLookToggleButton();
    }

    private void EnsureCameraLookToggleReferences()
    {
        if (_cameraLookToggleButton == null)
        {
            Transform existingButton = transform.Find("Btn_CameraLookToggle");
            if (existingButton != null)
            {
                _cameraLookToggleButton = existingButton.GetComponent<Button>();
            }
        }

        if (_cameraLookToggleButton == null)
        {
            CreateRuntimeCameraLookToggleButton();
        }

        if (_cameraLookToggleLabel == null && _cameraLookToggleButton != null)
        {
            _cameraLookToggleLabel = _cameraLookToggleButton.GetComponentInChildren<Text>(true);
        }
    }

    private void CreateRuntimeCameraLookToggleButton()
    {
        var buttonObject = new GameObject(
            "Btn_CameraLookToggle",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button));
        buttonObject.transform.SetParent(transform, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-8f, -8f);
        rect.sizeDelta = new Vector2(160f, 30f);

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.13f, 0.18f, 0.26f, 0.95f);

        _cameraLookToggleButton = buttonObject.GetComponent<Button>();
        ColorBlock colors = _cameraLookToggleButton.colors;
        colors.normalColor = new Color(1f, 1f, 1f, 1f);
        colors.highlightedColor = new Color(0.92f, 0.97f, 1f, 1f);
        colors.pressedColor = new Color(0.72f, 0.84f, 0.97f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        colors.colorMultiplier = 1f;
        _cameraLookToggleButton.colors = colors;

        var labelObject = new GameObject(
            "Label",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Text));
        labelObject.transform.SetParent(buttonObject.transform, false);

        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(8f, 4f);
        labelRect.offsetMax = new Vector2(-8f, -4f);

        _cameraLookToggleLabel = labelObject.GetComponent<Text>();
        _cameraLookToggleLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _cameraLookToggleLabel.fontSize = 13;
        _cameraLookToggleLabel.alignment = TextAnchor.MiddleCenter;
        _cameraLookToggleLabel.color = Color.white;
    }

    private void UpdateCameraLookToggleButton()
    {
        if (_cameraLookToggleButton == null)
        {
            return;
        }

        bool isEnabled = _routeController != null && _routeController.IsGestureLookAvailable;
        Image image = _cameraLookToggleButton.targetGraphic as Image;
        if (image != null)
        {
            image.color = isEnabled
                ? new Color(0.11f, 0.42f, 0.24f, 0.98f)
                : new Color(0.13f, 0.18f, 0.26f, 0.95f);
        }

        if (_cameraLookToggleLabel != null)
        {
            _cameraLookToggleLabel.text = isEnabled
                ? "Drag View: ON"
                : "Drag View: OFF";
        }
    }
}
