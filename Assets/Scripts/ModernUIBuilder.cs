using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ModernUIBuilder : MonoBehaviour
{
    [Header("Scene Switching")]
    public string PrimarySceneName = "MediaPipeDemo";
    public string SecondarySceneName = "MediaPipeCameraRouteTest";

    [Header("Button Trigger")]
    public bool ButtonsRequireDoubleClick = true;
    public float ButtonDoubleClickWindow = 0.8f;

    [Header("Runtime UI Updates")]
    [Tooltip("Only updates button label text when references are assigned. Disable to keep all button text fully scene-authored.")]
    public bool UpdateButtonLabelsAtRuntime = false;
    public bool HideLegacyRouteButtons = true;

    [Header("Scene References")]
    [SerializeField] private Canvas _canvas;
    [SerializeField] private GameObject _cameraRoutePanel;
    [SerializeField] private Text _cameraRouteTitle;
    [SerializeField] private Text _cameraNodeText;
    [SerializeField] private Text _cameraMessageText;
    [SerializeField] private Text _cameraHintText;
    [SerializeField] private Button _sceneSwitchButton;
    [SerializeField] private Text _sceneSwitchButtonLabel;
    [SerializeField] private Button _upRouteButton;
    [SerializeField] private Text _upRouteButtonLabel;
    [SerializeField] private Button _leftRouteButton;
    [SerializeField] private Text _leftRouteButtonLabel;
    [SerializeField] private Button _rightRouteButton;
    [SerializeField] private Text _rightRouteButtonLabel;
    [SerializeField] private Button _downRouteButton;
    [SerializeField] private Text _downRouteButtonLabel;

    private CameraRouteController _routeController;
    private readonly Dictionary<CameraRouteDirection, Button> _routeButtons = new Dictionary<CameraRouteDirection, Button>();
    private readonly Dictionary<CameraRouteDirection, Text> _routeButtonLabels = new Dictionary<CameraRouteDirection, Text>();
    private string _routeMessage = "Double pinch a button to trigger it.";

    private void Start()
    {
        ResolveRouteController();
        CacheSceneReferences();
        if (!TryBindExistingUi())
        {
            Debug.LogWarning("ModernUIBuilder: Failed to bind existing scene UI. Visual layout will be left untouched, but camera UI interactions may be unavailable.");
            return;
        }

        RefreshCameraRoutePanel(true);
    }

    private void Update()
    {
        ResolveRouteController();
        RefreshCameraRoutePanel();
    }

    [ContextMenu("Rebuild Scene UI")]
    public void RebuildSceneUi()
    {
        Debug.LogWarning("ModernUIBuilder: RebuildSceneUi is disabled. This component now only binds scene-authored UI and will not recreate or restyle it.");
    }

    private bool TryBindExistingUi()
    {
        _routeButtons.Clear();
        _routeButtonLabels.Clear();

        EnsureRoutePanelReferences();

        ConfigureExistingActionButton(_sceneSwitchButton, OnSceneSwitchTriggered, OnSceneSwitchArmed, OnButtonPendingReset);
        BindRouteButton(CameraRouteDirection.Up, _upRouteButton, _upRouteButtonLabel, "Btn_CameraUp");
        BindRouteButton(CameraRouteDirection.Left, _leftRouteButton, _leftRouteButtonLabel, "Btn_CameraLeft");
        BindRouteButton(CameraRouteDirection.Right, _rightRouteButton, _rightRouteButtonLabel, "Btn_CameraRight");
        BindRouteButton(CameraRouteDirection.Down, _downRouteButton, _downRouteButtonLabel, "Btn_CameraDown");

        return _cameraRoutePanel != null &&
            _sceneSwitchButton != null;
    }

    private void CacheSceneReferences()
    {
        if (_canvas == null)
        {
            _canvas = FindPreferredCanvas();
        }
    }

    private void EnsureRoutePanelReferences()
    {
        if (_cameraRoutePanel == null)
        {
            _cameraRoutePanel = GameObject.Find("CameraRoutePanel");
        }

        if (_cameraRoutePanel == null)
        {
            _cameraRoutePanel = FindRoutePanelFallback();
        }

        if (_cameraRoutePanel == null)
        {
            return;
        }

        if (_cameraRouteTitle == null)
        {
            _cameraRouteTitle = FindChildComponent<Text>(_cameraRoutePanel.transform, "CameraRouteTitle");
        }

        if (_cameraNodeText == null)
        {
            _cameraNodeText = FindChildComponent<Text>(_cameraRoutePanel.transform, "CameraRouteNode");
        }

        if (_cameraMessageText == null)
        {
            _cameraMessageText = FindChildComponent<Text>(_cameraRoutePanel.transform, "CameraRouteMessage");
        }

        if (_cameraHintText == null)
        {
            _cameraHintText = FindChildComponent<Text>(_cameraRoutePanel.transform, "CameraRouteHint");
        }

        if (_sceneSwitchButton == null)
        {
            _sceneSwitchButton = FindChildComponent<Button>(_cameraRoutePanel.transform, "Btn_SceneSwitch");
        }

        if (_sceneSwitchButtonLabel == null && _sceneSwitchButton != null)
        {
            _sceneSwitchButtonLabel = FindPreferredLabel(_sceneSwitchButton.transform);
        }

        if (_upRouteButton == null)
        {
            _upRouteButton = FindChildComponent<Button>(_cameraRoutePanel.transform, "Btn_CameraUp");
        }

        if (_leftRouteButton == null)
        {
            _leftRouteButton = FindChildComponent<Button>(_cameraRoutePanel.transform, "Btn_CameraLeft");
        }

        if (_rightRouteButton == null)
        {
            _rightRouteButton = FindChildComponent<Button>(_cameraRoutePanel.transform, "Btn_CameraRight");
        }

        if (_downRouteButton == null)
        {
            _downRouteButton = FindChildComponent<Button>(_cameraRoutePanel.transform, "Btn_CameraDown");
        }

        if (_upRouteButtonLabel == null && _upRouteButton != null)
        {
            _upRouteButtonLabel = FindPreferredLabel(_upRouteButton.transform);
        }

        if (_leftRouteButtonLabel == null && _leftRouteButton != null)
        {
            _leftRouteButtonLabel = FindPreferredLabel(_leftRouteButton.transform);
        }

        if (_rightRouteButtonLabel == null && _rightRouteButton != null)
        {
            _rightRouteButtonLabel = FindPreferredLabel(_rightRouteButton.transform);
        }

        if (_downRouteButtonLabel == null && _downRouteButton != null)
        {
            _downRouteButtonLabel = FindPreferredLabel(_downRouteButton.transform);
        }
    }

    private GameObject FindRoutePanelFallback()
    {
        if (_canvas == null)
        {
            return null;
        }

        Button[] buttons = _canvas.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button == null)
            {
                continue;
            }

            string buttonName = button.gameObject.name;
            if (buttonName == "Btn_SceneSwitch" ||
                buttonName == "Btn_CameraUp" ||
                buttonName == "Btn_CameraLeft" ||
                buttonName == "Btn_CameraRight" ||
                buttonName == "Btn_CameraDown")
            {
                return button.transform.parent != null ? button.transform.parent.gameObject : null;
            }
        }

        return null;
    }

    private void RefreshCameraRoutePanel(bool force = false)
    {
        if (_cameraRoutePanel == null)
        {
            return;
        }

        if (_routeController == null || _routeController.CurrentNode == null)
        {
            if (_cameraNodeText != null)
            {
                _cameraNodeText.text = "Current: --";
            }

            if (_cameraMessageText != null)
            {
                _cameraMessageText.text = "Message: Waiting for camera route controller.";
            }

            UpdateLegacyRouteButtons(false);
            return;
        }

        string currentNodeName = _routeController.CurrentNodeName;
        if (_routeController.IsTransitioning)
        {
            _routeMessage = $"Transitioning to {currentNodeName}";
        }
        else if (force)
        {
            _routeMessage = $"Auto touring zone {_routeController.CurrentNodeIndex + 1}/{_routeController.Nodes.Count}: {currentNodeName}";
        }

        if (_cameraNodeText != null)
        {
            _cameraNodeText.text = $"Current: Zone {_routeController.CurrentNodeIndex + 1}/{_routeController.Nodes.Count}  {SanitizeHudValue(currentNodeName, "--")}";
        }

        if (_cameraMessageText != null)
        {
            _cameraMessageText.text = $"Message: {SanitizeHudValue(_routeMessage, "Ready")}";
        }

        if (_cameraHintText != null)
        {
            _cameraHintText.text = $"Auto Tour  Stay {_routeController.ObservationDuration:0.0}s  Gap {_routeController.TransitionInterval:0.0}s  Move {_routeController.TransitionDuration:0.0}s";
        }

        UpdateLegacyRouteButtons(false);
        RefreshSceneSwitchButton();
    }

    private void UpdateLegacyRouteButtons(bool canInteract)
    {
        foreach (KeyValuePair<CameraRouteDirection, Button> pair in _routeButtons)
        {
            if (pair.Value != null)
            {
                pair.Value.interactable = canInteract;
                if (HideLegacyRouteButtons)
                {
                    pair.Value.gameObject.SetActive(false);
                }
            }
        }
    }

    private void OnRouteButtonClicked(CameraRouteDirection direction)
    {
        _routeMessage = $"Directional route '{DirectionToLabel(direction)}' has been disabled. Auto tour is active.";
        RefreshCameraRoutePanel(true);
    }

    private void RefreshSceneSwitchButton()
    {
        string targetSceneName = GetSceneSwitchTargetName();
        if (UpdateButtonLabelsAtRuntime && _sceneSwitchButtonLabel != null)
        {
            _sceneSwitchButtonLabel.text = string.IsNullOrWhiteSpace(targetSceneName)
                ? "Scene\nUnavailable"
                : $"Scene\n{targetSceneName}";
        }

        if (_sceneSwitchButton != null)
        {
            _sceneSwitchButton.interactable = !string.IsNullOrWhiteSpace(targetSceneName);
        }
    }

    private string GetSceneSwitchTargetName()
    {
        string activeSceneName = SceneManager.GetActiveScene().name;
        if (!string.IsNullOrWhiteSpace(PrimarySceneName) &&
            !string.Equals(activeSceneName, PrimarySceneName, System.StringComparison.OrdinalIgnoreCase))
        {
            return PrimarySceneName;
        }

        if (!string.IsNullOrWhiteSpace(SecondarySceneName) &&
            !string.Equals(activeSceneName, SecondarySceneName, System.StringComparison.OrdinalIgnoreCase))
        {
            return SecondarySceneName;
        }

        return string.Empty;
    }

    private void OnRouteButtonArmed(CameraRouteDirection direction)
    {
        _routeMessage = $"Directional route '{DirectionToLabel(direction)}' has been disabled. Auto tour is active.";
        RefreshCameraRoutePanel();
    }

    private void OnSceneSwitchArmed()
    {
        string targetSceneName = GetSceneSwitchTargetName();
        _routeMessage = string.IsNullOrWhiteSpace(targetSceneName)
            ? "No target scene configured."
            : $"Double pinch to open {targetSceneName}.";
        RefreshCameraRoutePanel();
    }

    private void OnSceneSwitchTriggered()
    {
        string targetSceneName = GetSceneSwitchTargetName();
        if (string.IsNullOrWhiteSpace(targetSceneName))
        {
            _routeMessage = "No target scene configured.";
            RefreshCameraRoutePanel();
            return;
        }

        _routeMessage = $"Loading {targetSceneName}";
        RefreshCameraRoutePanel();
        SceneManager.LoadScene(targetSceneName);
    }

    private void OnButtonPendingReset()
    {
        if (_routeController != null && _routeController.CurrentNode != null)
        {
            _routeMessage = $"Auto touring zone {_routeController.CurrentNodeIndex + 1}/{_routeController.Nodes.Count}: {_routeController.CurrentNodeName}";
        }
        else
        {
            _routeMessage = "Waiting for auto tour.";
        }

        RefreshCameraRoutePanel();
    }

    private void ResolveRouteController()
    {
        if (_routeController == null)
        {
            _routeController = FindObjectOfType<CameraRouteController>();
        }
    }

    private void BindRouteButton(CameraRouteDirection direction, Button button, Text label, string buttonName)
    {
        if (button == null && _cameraRoutePanel != null)
        {
            button = FindChildComponent<Button>(_cameraRoutePanel.transform, buttonName);
        }

        if (label == null && button != null)
        {
            label = FindPreferredLabel(button.transform);
        }

        if (button == null)
        {
            return;
        }

        ConfigureExistingActionButton(
            button,
            () => OnRouteButtonClicked(direction),
            () => OnRouteButtonArmed(direction),
            OnButtonPendingReset);
        _routeButtons[direction] = button;
        _routeButtonLabels[direction] = label;
    }

    private void ConfigureExistingActionButton(
        Button button,
        UnityEngine.Events.UnityAction onTriggered,
        UnityEngine.Events.UnityAction onArmed,
        UnityEngine.Events.UnityAction onReset)
    {
        if (button == null)
        {
            return;
        }

        PinchActionButton trigger = button.GetComponent<PinchActionButton>();
        if (trigger == null)
        {
            trigger = button.gameObject.AddComponent<PinchActionButton>();
        }

        trigger.RequireDoubleClick = ButtonsRequireDoubleClick;
        trigger.DoubleClickWindow = ButtonDoubleClickWindow;
        trigger.OnTriggered.RemoveAllListeners();
        trigger.OnArmed.RemoveAllListeners();
        trigger.OnDisarmed.RemoveAllListeners();
        if (onTriggered != null) trigger.OnTriggered.AddListener(onTriggered);
        if (onArmed != null) trigger.OnArmed.AddListener(onArmed);
        if (onReset != null) trigger.OnDisarmed.AddListener(onReset);
    }

    private T FindChildComponent<T>(Transform root, string childName, string requiredParentName = null) where T : Component
    {
        if (root == null)
        {
            return null;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if ((string.IsNullOrWhiteSpace(requiredParentName) || child.name == requiredParentName) && child.name == childName)
            {
                T component = child.GetComponent<T>();
                if (component != null)
                {
                    return component;
                }
            }

            if (!string.IsNullOrWhiteSpace(requiredParentName) && child.name == requiredParentName)
            {
                T directChildComponent = FindChildComponent<T>(child, childName, null);
                if (directChildComponent != null)
                {
                    return directChildComponent;
                }
            }

            T nestedComponent = FindChildComponent<T>(child, childName, requiredParentName);
            if (nestedComponent != null)
            {
                return nestedComponent;
            }
        }

        return null;
    }

    private Canvas FindPreferredCanvas()
    {
        Canvas[] canvases = FindObjectsOfType<Canvas>();
        for (int i = 0; i < canvases.Length; i++)
        {
            if (canvases[i] != null && canvases[i].gameObject.name == "Main Canvas")
            {
                return canvases[i];
            }
        }

        for (int i = 0; i < canvases.Length; i++)
        {
            if (canvases[i] != null && canvases[i].gameObject.name != "CursorCanvas")
            {
                return canvases[i];
            }
        }

        return FindObjectOfType<Canvas>();
    }

    private string DirectionToLabel(CameraRouteDirection direction)
    {
        switch (direction)
        {
            case CameraRouteDirection.Left:
                return "Left";
            case CameraRouteDirection.Right:
                return "Right";
            case CameraRouteDirection.Up:
                return "Up";
            case CameraRouteDirection.Down:
                return "Down";
            default:
                return "Route";
        }
    }

    private string SanitizeHudValue(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private Text FindPreferredLabel(Transform root)
    {
        if (root == null)
        {
            return null;
        }

        Text namedLabel = FindChildComponent<Text>(root, "Label");
        if (namedLabel != null)
        {
            return namedLabel;
        }

        return root.GetComponentInChildren<Text>(true);
    }
}
