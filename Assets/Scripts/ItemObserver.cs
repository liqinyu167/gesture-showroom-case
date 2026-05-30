using Fungus;
using UnityEngine;

[RequireComponent(typeof(InteractableItem))]
public class ItemObserver : MonoBehaviour
{
    [Header("Observation Distance")]
    public float MinObservationDistance = 2.0f;
    public float MaxObservationDistance = 15.0f;
    [Range(2.0f, 15.0f)]
    public float ObservationDistance = 8.0f;
    
    [Header("Scale & Motion")]
    public float TargetScaleMultiplier = 1.0f;
    public float ZoomSensitivity = 0.005f;
    public float TransitionSpeed = 5.0f;

    public bool IsObserving => _isObserving;
    public bool UsesImageObservationMode => _interactable != null && _interactable.UsesImageObservationMode;

    private bool _isObserving = false;
    private Vector3 _originalPosition;
    private Quaternion _originalRotation;
    private Vector3 _originalScale;
    private Vector2 _imageObservationAngles;

    private Camera _mainCamera;
    private InteractableItem _interactable;
    private CameraRouteController _cameraRouteController;

    private void Awake()
    {
        _interactable = GetComponent<InteractableItem>();
        _mainCamera = Camera.main;
        _cameraRouteController = FindObjectOfType<CameraRouteController>();
    }
    
    private void Start()
    {
        _originalPosition = transform.position;
        _originalRotation = transform.rotation;
        _originalScale    = transform.localScale;

        if (_interactable != null)
            _interactable.OnDoubleClick += ToggleObservation;
    }

    private void Update()
    {
        if (_mainCamera == null)
        {
            _mainCamera = Camera.main;
            if (_mainCamera == null)
            {
                return;
            }
        }

        if (_isObserving)
        {
            Vector3 targetPos = _mainCamera.transform.position + _mainCamera.transform.forward * ObservationDistance;
            transform.position   = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * TransitionSpeed);
            transform.localScale = Vector3.Lerp(transform.localScale, _originalScale * TargetScaleMultiplier, Time.deltaTime * TransitionSpeed);

            if (UsesImageObservationMode)
            {
                Quaternion targetRotation = GetImageObservationRotation();
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * TransitionSpeed);
            }
        }
        else
        {
            transform.position   = Vector3.Lerp(transform.position, _originalPosition, Time.deltaTime * TransitionSpeed);
            transform.localScale = Vector3.Lerp(transform.localScale, _originalScale, Time.deltaTime * TransitionSpeed);
            transform.rotation   = Quaternion.Lerp(transform.rotation, _originalRotation, Time.deltaTime * TransitionSpeed);
        }
    }

    /// <summary>Toggle observation mode on/off.</summary>
    public void ToggleObservation()
    {
        if (_isObserving) ExitObservation();
        else              EnterObservation();
    }

    /// <summary>Enter observation mode and register as focused item.</summary>
    public void EnterObservation()
    {
        if (_isObserving) return;
        _isObserving = true;
        _imageObservationAngles = Vector2.zero;

        if (_cameraRouteController == null)
        {
            _cameraRouteController = FindObjectOfType<CameraRouteController>();
        }

        if (ShowroomRegistry.Instance != null)
            ShowroomRegistry.Instance.SetFocus(_interactable);

        RaiseObservationEnteredEvent();
        Debug.Log($"[ItemObserver] Entered Observation: {_interactable.ItemName}");
    }

    /// <summary>Exit observation mode and clear the registry focus.</summary>
    public void ExitObservation()
    {
        if (!_isObserving) return;
        _isObserving = false;
        _imageObservationAngles = Vector2.zero;

        if (ShowroomRegistry.Instance != null)
            ShowroomRegistry.Instance.ClearFocus();

        if (_cameraRouteController == null)
        {
            _cameraRouteController = FindObjectOfType<CameraRouteController>();
        }

        _cameraRouteController?.SetGestureLookEnabled(true);

        Debug.Log($"[ItemObserver] Exited Observation: {_interactable.ItemName}");
    }

    /// <summary>Adjust observation distance (called from OnScale via InteractableItem).</summary>
    public void AdjustZoom(float deltaDistance)
    {
        ObservationDistance -= deltaDistance * ZoomSensitivity;
        ObservationDistance  = Mathf.Clamp(ObservationDistance, MinObservationDistance, MaxObservationDistance);
    }

    /// <summary>Apply a small, clamped tilt while keeping image-like items front-facing.</summary>
    public void ApplyImageObservationRotation(Vector2 rotationDelta)
    {
        if (!_isObserving || !UsesImageObservationMode)
        {
            return;
        }

        float maxAngle = Mathf.Max(0.0f, _interactable.ImageObservationMaxAngle);
        _imageObservationAngles += new Vector2(rotationDelta.x, rotationDelta.y);
        _imageObservationAngles = Vector2.ClampMagnitude(_imageObservationAngles, maxAngle);
    }

    private void OnDestroy()
    {
        if (_interactable != null)
            _interactable.OnDoubleClick -= ToggleObservation;

        // Clean up registry reference if we were focused
        if (_isObserving && ShowroomRegistry.Instance != null)
            ShowroomRegistry.Instance.ClearFocus();
    }

    private void RaiseObservationEnteredEvent()
    {
        var fungusManager = FungusManager.Instance;
        if (fungusManager == null || fungusManager.EventDispatcher == null)
        {
            return;
        }

        fungusManager.EventDispatcher.Raise(new ShowroomObservationEntered.ShowroomObservationEnteredEvent(this));
    }

    private Quaternion GetImageObservationRotation()
    {
        Vector3 faceCameraForward = -_mainCamera.transform.forward;
        if (faceCameraForward.sqrMagnitude <= Mathf.Epsilon)
        {
            return transform.rotation;
        }

        Quaternion faceCameraRotation = Quaternion.LookRotation(faceCameraForward, _mainCamera.transform.up);
        Quaternion tiltRotation = Quaternion.Euler(_imageObservationAngles.x, _imageObservationAngles.y, 0.0f);
        Quaternion frontAxisCorrection = GetFrontAxisCorrection(_interactable.ImageFrontAxis);
        return faceCameraRotation * tiltRotation * frontAxisCorrection;
    }

    private static Quaternion GetFrontAxisCorrection(ObservationFacingAxis axis)
    {
        switch (axis)
        {
            case ObservationFacingAxis.NegativeZ:
                return Quaternion.Euler(0.0f, 180.0f, 0.0f);
            case ObservationFacingAxis.PositiveX:
                return Quaternion.Euler(0.0f, -90.0f, 0.0f);
            case ObservationFacingAxis.NegativeX:
                return Quaternion.Euler(0.0f, 90.0f, 0.0f);
            case ObservationFacingAxis.PositiveY:
                return Quaternion.Euler(90.0f, 0.0f, 0.0f);
            case ObservationFacingAxis.NegativeY:
                return Quaternion.Euler(-90.0f, 0.0f, 0.0f);
            default:
                return Quaternion.identity;
        }
    }
}
