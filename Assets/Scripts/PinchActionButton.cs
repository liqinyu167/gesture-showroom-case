using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public class PinchActionButton : MonoBehaviour, IPointerClickHandler
{
    public bool RequireDoubleClick = true;
    public float DoubleClickWindow = 0.8f;
    public UnityEvent OnTriggered = new UnityEvent();
    public UnityEvent OnArmed = new UnityEvent();
    public UnityEvent OnDisarmed = new UnityEvent();

    private Button _button;
    private bool _isArmed;
    private float _armedAtTime = -1f;

    private void Awake()
    {
        _button = GetComponent<Button>();
    }

    private void Update()
    {
        if (!_isArmed)
        {
            return;
        }

        if (_button != null && !_button.interactable)
        {
            ResetPendingState();
            return;
        }

        if (Time.unscaledTime - _armedAtTime > DoubleClickWindow)
        {
            ResetPendingState();
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        if (_button != null && !_button.interactable)
        {
            return;
        }

        if (!RequireDoubleClick)
        {
            Trigger();
            return;
        }

        float elapsed = Time.unscaledTime - _armedAtTime;
        if (_isArmed && elapsed <= DoubleClickWindow)
        {
            _isArmed = false;
            _armedAtTime = -1f;
            OnTriggered.Invoke();
            return;
        }

        _isArmed = true;
        _armedAtTime = Time.unscaledTime;
        OnArmed.Invoke();
    }

    public void ResetPendingState()
    {
        if (!_isArmed)
        {
            return;
        }

        _isArmed = false;
        _armedAtTime = -1f;
        OnDisarmed.Invoke();
    }

    private void Trigger()
    {
        _isArmed = false;
        _armedAtTime = -1f;
        OnTriggered.Invoke();
    }
}
