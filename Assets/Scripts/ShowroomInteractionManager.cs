using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Manages all hand-tracked interactions for the showroom.
/// Translates raw pinch events from HandTrackingManagerV2 into IHandInteractable calls.
/// Uses ShowroomRegistry to detect observation mode and apply global-override grabs.
/// </summary>
public class ShowroomInteractionManager : MonoBehaviour
{
    [Header("Dependencies")]
    public HandTrackingManagerV2 HandTracking;
    public ModernUIBuilder UIBuilder;
    public GameObject CursorPrefab;
    
    [Header("Cursor Colors")]
    public Color LeftCursorColor  = new Color(0.2f, 0.8f, 1f, 0.8f);
    public Color RightCursorColor = new Color(1f, 0.4f, 0.2f, 0.8f);
    [Range(0.1f, 1f)]
    public float PinchDarkenMultiplier = 0.5f;

    [Header("Interaction Settings")]
    public float ClickDragThreshold  = 150.0f;
    public float DoubleClickTimeWindow = 0.5f;
    
    [Header("Fallback")]
    [Tooltip("Optional fallback item to interact with when raycast hits nothing.")]
    public InteractableItem GlobalFallbackItem;

    // --- Private State ---
    private ShowroomCursor[] _cursors         = new ShowroomCursor[2];
    private InteractableItem[] _grabbedItems  = new InteractableItem[2];
    private Vector2[] _lastDragPositions      = new Vector2[2];
    private Vector2[] _pinchStartPositions    = new Vector2[2];
    private float[]   _lastClickTimes         = new float[2];
    private InteractableItem[] _lastClickTargets = new InteractableItem[2]; // per-hand: which item was last clicked
    private float     _lastPinchDistance      = -1f;
    private GameObject[] _pendingUiTargets    = new GameObject[2];
    private PointerEventData[] _activeUiPointerData = new PointerEventData[2];
    private bool[] _activeUiSupportsDrag = new bool[2];
    private bool[] _activeUiDragging = new bool[2];
    private bool _cameraGestureDragActive;
    private CameraRouteController _cameraRouteController;

    private void Start()
    {
        _lastClickTimes[0] = 0f;
        _lastClickTimes[1] = 0f;

        if (HandTracking == null) HandTracking = FindObjectOfType<HandTrackingManagerV2>();
        if (UIBuilder    == null) UIBuilder    = FindObjectOfType<ModernUIBuilder>();
        _cameraRouteController = FindObjectOfType<CameraRouteController>();

        if (HandTracking != null)
        {
            HandTracking.OnPinchDown += HandlePinchDown;
            HandTracking.OnPinchDrag += HandlePinchDrag;
            HandTracking.OnPinchUp   += HandlePinchUp;
        }

        // Initialize cursors — parent to dedicated CursorCanvas (ScreenSpaceOverlay) so they render above everything
        Canvas cursorCanvas = null;
        Canvas mainCanvas = null;
        var allCanvases = FindObjectsOfType<Canvas>();
        foreach (var c in allCanvases)
        {
            if (c.gameObject.name == "CursorCanvas") { cursorCanvas = c; break; }
        }
        foreach (var c in allCanvases)
        {
            if (c.gameObject.name == "Main Canvas") { mainCanvas = c; break; }
        }
        if (cursorCanvas == null) cursorCanvas = FindObjectOfType<Canvas>(); // fallback
        SyncCanvasCoordinateSpace(mainCanvas, cursorCanvas);

        for (int i = 0; i < 2; i++)
        {
            _cursors[i] = GetOrCreateCursor(i, cursorCanvas);
            _cursors[i].UpdateState(new Vector2(-1000, -1000), false);
        }
    }

    private ShowroomCursor GetOrCreateCursor(int handIndex, Canvas cursorCanvas)
    {
        string cursorName = $"ShowroomCursor_{handIndex}";

        if (cursorCanvas != null)
        {
            var existingCursors = cursorCanvas.GetComponentsInChildren<ShowroomCursor>(true);
            ShowroomCursor firstMatch = null;

            foreach (var existingCursor in existingCursors)
            {
                if (existingCursor.gameObject.name != cursorName)
                {
                    continue;
                }

                if (firstMatch == null)
                {
                    firstMatch = existingCursor;
                    continue;
                }

                Destroy(existingCursor.gameObject);
            }

            if (firstMatch != null)
            {
                firstMatch.transform.SetParent(cursorCanvas.transform, false);
                firstMatch.gameObject.SetActive(true);
                return firstMatch;
            }
        }

        GameObject cursorObj;
        if (CursorPrefab != null)
        {
            cursorObj = Instantiate(CursorPrefab);
            cursorObj.name = cursorName;
        }
        else
        {
            cursorObj = new GameObject(cursorName);
        }

        if (cursorCanvas != null)
        {
            cursorObj.transform.SetParent(cursorCanvas.transform, false);
        }

        var cursor = cursorObj.GetComponent<ShowroomCursor>();
        if (cursor == null)
        {
            cursor = cursorObj.AddComponent<ShowroomCursor>();
        }

        return cursor;
    }

    private void SyncCanvasCoordinateSpace(Canvas sourceCanvas, Canvas targetCanvas)
    {
        if (sourceCanvas == null || targetCanvas == null || sourceCanvas == targetCanvas)
        {
            return;
        }

        targetCanvas.renderMode = sourceCanvas.renderMode;
        targetCanvas.pixelPerfect = sourceCanvas.pixelPerfect;
        targetCanvas.worldCamera = sourceCanvas.worldCamera;
        targetCanvas.planeDistance = sourceCanvas.planeDistance;

        CanvasScaler sourceScaler = sourceCanvas.GetComponent<CanvasScaler>();
        CanvasScaler targetScaler = targetCanvas.GetComponent<CanvasScaler>();
        if (sourceScaler == null || targetScaler == null)
        {
            return;
        }

        targetScaler.uiScaleMode = sourceScaler.uiScaleMode;
        targetScaler.referenceResolution = sourceScaler.referenceResolution;
        targetScaler.screenMatchMode = sourceScaler.screenMatchMode;
        targetScaler.matchWidthOrHeight = sourceScaler.matchWidthOrHeight;
        targetScaler.referencePixelsPerUnit = sourceScaler.referencePixelsPerUnit;
        targetScaler.scaleFactor = sourceScaler.scaleFactor;
        targetScaler.physicalUnit = sourceScaler.physicalUnit;
        targetScaler.fallbackScreenDPI = sourceScaler.fallbackScreenDPI;
        targetScaler.defaultSpriteDPI = sourceScaler.defaultSpriteDPI;
        targetScaler.dynamicPixelsPerUnit = sourceScaler.dynamicPixelsPerUnit;
    }

    private void Update()
    {
        if (HandTracking == null) return;

        // Update cursors from latest hand tracking data
        for (int i = 0; i < 2; i++)
        {
            Vector2 cursorPos  = HandTracking.CursorScreenPositions[i];
            bool    isPinching = HandTracking.IsPinching[i];
            if (cursorPos.sqrMagnitude < 0.1f) cursorPos = new Vector2(-1000, -1000);

            string handedness = HandTracking.Handedness[i];
            Color  baseColor  = (handedness == "Left") ? LeftCursorColor : RightCursorColor;
            _cursors[i].SetColors(baseColor, PinchDarkenMultiplier);
            _cursors[i].UpdateState(cursorPos, isPinching);
        }

        ReportUserActivityToCameraRoute();

        HandleTwoHandPinchZoom();
    }

    private void ReportUserActivityToCameraRoute()
    {
        if (_cameraRouteController == null)
        {
            _cameraRouteController = FindObjectOfType<CameraRouteController>();
            if (_cameraRouteController == null)
            {
                return;
            }
        }

        for (int i = 0; i < 2; i++)
        {
            Vector2 cursorPos = HandTracking.CursorScreenPositions[i];
            bool hasTrackedHand =
                HandTracking.IsPinching[i] ||
                cursorPos.x > -999f ||
                cursorPos.y > -999f;

            if (hasTrackedHand)
            {
                _cameraRouteController.NotifyUserActivity();
                return;
            }
        }
    }

    // ------------------------------------------------------------------
    // Two-hand scale
    // ------------------------------------------------------------------
    private void HandleTwoHandPinchZoom()
    {
        if (_grabbedItems[0] != null && _grabbedItems[0] == _grabbedItems[1])
        {
            // Scale only allowed inside observation mode
            var observer = _grabbedItems[0].GetComponent<ItemObserver>();
            bool isObserving = observer != null && observer.IsObserving;

            Vector2 pos0 = HandTracking.CursorScreenPositions[0];
            Vector2 pos1 = HandTracking.CursorScreenPositions[1];
            float currentDistance = Vector2.Distance(pos0, pos1);

            if (isObserving && _lastPinchDistance > 0)
            {
                float deltaDistance = currentDistance - _lastPinchDistance;
                if (Mathf.Abs(deltaDistance) > 1.0f)
                {
                    _grabbedItems[0].OnScale(deltaDistance);
                }
            }
            _lastPinchDistance = currentDistance;
        }
        else
        {
            _lastPinchDistance = -1f;
        }
    }

    // ------------------------------------------------------------------
    // Pinch Down
    // ------------------------------------------------------------------
    private void HandlePinchDown(int handIndex, Vector2 screenPos)
    {
        _lastDragPositions[handIndex]  = screenPos;
        _pinchStartPositions[handIndex] = screenPos;
        Debug.Log($"[Interaction] PinchDown by Hand {handIndex} at {screenPos}");

        // 1. Check interactive UI first
        if (TryBeginUiInteraction(handIndex, screenPos)) return;

        // 2. Global override: if an item is in observation mode, always grab it
        var registry = ShowroomRegistry.Instance;
        if (registry != null && registry.FocusedItem != null)
        {
            var focusedItem = registry.FocusedItem as InteractableItem;
            if (focusedItem != null)
            {
                GrabItem(handIndex, focusedItem);
                return;
            }
        }

        // 3. Camera look mode now requires both hands pinching together.
        if (TryBeginCameraGestureLook(handIndex, screenPos)) return;

        // 3. Standard 3D physics raycast — only grab if we actually hit an item
        Ray ray = Camera.main.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            Debug.Log($"[Interaction] Raycast 3D hit: {hit.collider.gameObject.name}");
            InteractableItem item = hit.collider.GetComponentInParent<InteractableItem>();
            if (item != null)
            {
                GrabItem(handIndex, item);
                return;
            }
            Debug.Log("[Interaction] Hit 3D object, but not an InteractableItem.");
        }
        else
        {
            Debug.Log("[Interaction] Raycast hit nothing — no grab in normal mode.");
        }
    }

    // ------------------------------------------------------------------
    // Pinch Drag
    // ------------------------------------------------------------------
    private void HandlePinchDrag(int handIndex, Vector2 screenPos)
    {
        Vector2 delta = screenPos - _lastDragPositions[handIndex];
        _lastDragPositions[handIndex] = screenPos;

        if (TryUpdateUiInteractionDrag(handIndex, screenPos, delta))
        {
            return;
        }

        if (TryUpdateCameraGestureLook(handIndex, screenPos))
        {
            return;
        }

        if (_grabbedItems[handIndex] != null)
        {
            var observer = _grabbedItems[handIndex].GetComponent<ItemObserver>();
            bool isObserving = observer != null && observer.IsObserving;

            // Rotation only allowed inside observation mode, and not during two-hand zoom
            bool isTwoHandedGrab = _grabbedItems[0] != null && _grabbedItems[0] == _grabbedItems[1];
            if (isObserving && !isTwoHandedGrab)
            {
                _grabbedItems[handIndex].OnRotate(delta);
            }
        }
    }

    // ------------------------------------------------------------------
    // Pinch Up
    // ------------------------------------------------------------------
    private void HandlePinchUp(int handIndex, Vector2 screenPos)
    {
        Debug.Log($"[Interaction] PinchUp by Hand {handIndex}");
        if (TryCompleteUiInteraction(handIndex, screenPos))
        {
            return;
        }

        if (TryEndCameraGestureLook(handIndex))
        {
            return;
        }

        if (_grabbedItems[handIndex] != null)
        {
            float dragDistance = (screenPos - _pinchStartPositions[handIndex]).magnitude;
            if (dragDistance < ClickDragThreshold)
            {
                bool sameItemClick = _lastClickTargets[handIndex] == _grabbedItems[handIndex];
                bool withinWindow  = Time.time - _lastClickTimes[handIndex] < DoubleClickTimeWindow;

                if (sameItemClick && withinWindow)
                {
                    Debug.Log($"[Interaction] DOUBLE Click on: {_grabbedItems[handIndex].ItemName}");
                    _grabbedItems[handIndex].OnHandDoubleClick();
                    _lastClickTimes[handIndex]   = 0f;
                    _lastClickTargets[handIndex] = null; // reset so 3rd click isn't another double
                }
                else
                {
                    Debug.Log($"[Interaction] Single Click on: {_grabbedItems[handIndex].ItemName} (drag: {dragDistance:F1})");
                    _grabbedItems[handIndex].OnSingleClick();
                    _lastClickTimes[handIndex]   = Time.time;
                    _lastClickTargets[handIndex] = _grabbedItems[handIndex];
                }
            }
            else
            {
                Debug.Log($"[Interaction] Drag released (drag: {dragDistance:F1} > threshold)");
                _lastClickTimes[handIndex]   = 0f;
                _lastClickTargets[handIndex] = null;
            }

            _grabbedItems[handIndex].OnDeselected();
            _grabbedItems[handIndex] = null;
        }
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------
    private void GrabItem(int handIndex, InteractableItem item)
    {
        if (_grabbedItems[handIndex] != null && _grabbedItems[handIndex] != item)
            _grabbedItems[handIndex].OnDeselected();
        _grabbedItems[handIndex] = item;
        _grabbedItems[handIndex].OnSelected();
    }

    private bool TryBeginCameraGestureLook(int handIndex, Vector2 screenPos)
    {
        if (_cameraRouteController == null)
        {
            _cameraRouteController = FindObjectOfType<CameraRouteController>();
        }

        if (_cameraRouteController == null ||
            !_cameraRouteController.IsGestureLookAvailable ||
            HandTracking == null)
        {
            return false;
        }

        int otherHandIndex = handIndex == 0 ? 1 : 0;
        if (!HandTracking.IsPinching[otherHandIndex] ||
            _pendingUiTargets[handIndex] != null ||
            _pendingUiTargets[otherHandIndex] != null)
        {
            return false;
        }

        CancelGrabbedItemForCameraGesture(0);
        CancelGrabbedItemForCameraGesture(1);

        Vector2 midpoint = GetTwoHandGestureMidpoint();
        if (!_cameraGestureDragActive)
        {
            if (!_cameraRouteController.TryBeginGestureLook(handIndex, midpoint))
            {
                return false;
            }

            _cameraGestureDragActive = true;
            Debug.Log("[Interaction] Camera gesture look started by two-hand pinch.");
            return true;
        }

        _cameraRouteController.TryUpdateGestureLook(handIndex, midpoint);
        return true;
    }

    private bool TryUpdateCameraGestureLook(int handIndex, Vector2 screenPos)
    {
        if (!_cameraGestureDragActive || _cameraRouteController == null || HandTracking == null)
        {
            return false;
        }

        if (!HandTracking.IsPinching[0] || !HandTracking.IsPinching[1])
        {
            return false;
        }

        if (!_cameraRouteController.TryUpdateGestureLook(handIndex, GetTwoHandGestureMidpoint()))
        {
            _cameraGestureDragActive = false;
            return false;
        }

        return true;
    }

    private bool TryEndCameraGestureLook(int handIndex)
    {
        if (!_cameraGestureDragActive)
        {
            return false;
        }

        _cameraGestureDragActive = false;
        _cameraRouteController?.TryEndGestureLook(handIndex);
        Debug.Log("[Interaction] Camera gesture look ended.");
        return true;
    }

    private Vector2 GetTwoHandGestureMidpoint()
    {
        if (HandTracking == null)
        {
            return Vector2.zero;
        }

        return (HandTracking.CursorScreenPositions[0] + HandTracking.CursorScreenPositions[1]) * 0.5f;
    }

    private void CancelGrabbedItemForCameraGesture(int handIndex)
    {
        if (_grabbedItems[handIndex] == null)
        {
            return;
        }

        _grabbedItems[handIndex].OnDeselected();
        _grabbedItems[handIndex] = null;
        _lastClickTimes[handIndex] = 0f;
        _lastClickTargets[handIndex] = null;
    }

    private bool TryBeginUiInteraction(int handIndex, Vector2 screenPos)
    {
        ResetUiInteractionState(handIndex);
        _pendingUiTargets[handIndex] = null;
        if (!TryFindUIHitTarget(
            screenPos,
            out GameObject clickTarget,
            out Vector2 resolvedPointerPosition,
            out RaycastResult initialRaycast,
            out bool supportsDrag))
        {
            return false;
        }

        _pendingUiTargets[handIndex] = clickTarget;
        _activeUiSupportsDrag[handIndex] = supportsDrag;
        _activeUiPointerData[handIndex] = CreatePointerEventData(handIndex, resolvedPointerPosition, clickTarget, initialRaycast);

        ExecuteUiPointerEnter(clickTarget, _activeUiPointerData[handIndex]);
        ExecuteUiPointerDown(clickTarget, _activeUiPointerData[handIndex]);
        if (supportsDrag)
        {
            ExecuteUiInitializePotentialDrag(clickTarget, _activeUiPointerData[handIndex]);
        }

        Debug.Log($"[Interaction] UI target armed by hand {handIndex}: {clickTarget.name}");
        return true;
    }

    private bool TryCompleteUiInteraction(int handIndex, Vector2 screenPos)
    {
        GameObject pendingTarget = _pendingUiTargets[handIndex];
        if (pendingTarget == null)
        {
            return false;
        }

        PointerEventData pointerData = _activeUiPointerData[handIndex];
        UpdatePointerEventData(handIndex, screenPos, pointerData);

        _pendingUiTargets[handIndex] = null;
        bool didDragUi = _activeUiDragging[handIndex];
        float dragDistance = (screenPos - _pinchStartPositions[handIndex]).magnitude;

        if (didDragUi)
        {
            ExecuteUiEndDrag(pendingTarget, pointerData);
            ExecuteUiPointerUp(pendingTarget, pointerData);
            ExecuteUiPointerExit(pendingTarget, pointerData);
            ResetUiInteractionState(handIndex);
            return true;
        }

        if (dragDistance > ClickDragThreshold)
        {
            ExecuteUiPointerUp(pendingTarget, pointerData);
            ExecuteUiPointerExit(pendingTarget, pointerData);
            Debug.Log($"[Interaction] UI interaction cancelled by drag ({dragDistance:F1}).");
            ResetUiInteractionState(handIndex);
            return true;
        }

        if (TryFindUIHitTarget(
            screenPos,
            out GameObject releaseTarget,
            out Vector2 releasePointerPosition,
            out RaycastResult releaseRaycast,
            out bool _)
            && releaseTarget == pendingTarget)
        {
            pointerData = CreatePointerEventData(handIndex, releasePointerPosition, releaseTarget, releaseRaycast);
            ExecuteUiPointerUp(pendingTarget, pointerData);
            ExecuteUiClick(pendingTarget, pointerData);
            ExecuteUiPointerExit(pendingTarget, pointerData);
        }
        else
        {
            ExecuteUiPointerUp(pendingTarget, pointerData);
            ExecuteUiPointerExit(pendingTarget, pointerData);
            Debug.Log($"[Interaction] UI interaction released off target: {pendingTarget.name}");
        }

        ResetUiInteractionState(handIndex);
        return true;
    }

    private bool TryUpdateUiInteractionDrag(int handIndex, Vector2 screenPos, Vector2 delta)
    {
        GameObject pendingTarget = _pendingUiTargets[handIndex];
        PointerEventData pointerData = _activeUiPointerData[handIndex];
        if (pendingTarget == null || pointerData == null)
        {
            return false;
        }

        UpdatePointerEventData(handIndex, screenPos, pointerData, delta);

        if (_activeUiSupportsDrag[handIndex])
        {
            if (!_activeUiDragging[handIndex] && delta.sqrMagnitude > 0.01f)
            {
                _activeUiDragging[handIndex] = true;
                pointerData.pointerDrag = pendingTarget;
                ExecuteUiBeginDrag(pendingTarget, pointerData);
            }

            if (_activeUiDragging[handIndex])
            {
                ExecuteUiDrag(pendingTarget, pointerData);
            }
        }

        return true;
    }

    private bool TryFindUIHitTarget(
        Vector2 screenPos,
        out GameObject clickTarget,
        out Vector2 resolvedPointerPosition,
        out RaycastResult hitRaycast,
        out bool supportsDrag)
    {
        clickTarget = null;
        resolvedPointerPosition = screenPos;
        hitRaycast = default;
        supportsDrag = false;
        if (EventSystem.current == null)
        {
            return false;
        }

        if (TryFindUIHitTargetAtPosition(screenPos, out clickTarget, out hitRaycast, out supportsDrag))
        {
            resolvedPointerPosition = screenPos;
            return true;
        }

        return false;
    }

    private bool TryFindUIHitTargetAtPosition(
        Vector2 pointerPosition,
        out GameObject clickTarget,
        out RaycastResult hitRaycast,
        out bool supportsDrag)
    {
        clickTarget = null;
        hitRaycast = default;
        supportsDrag = false;
        var pointerData = new PointerEventData(EventSystem.current) { position = pointerPosition };
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, results);

        foreach (var result in results)
        {
            GameObject hitUI = result.gameObject;
            if (hitUI.name == "Screen" || hitUI.name.Contains("Screen")) continue;

            var slider       = hitUI.GetComponentInParent<Slider>();
            var button       = hitUI.GetComponentInParent<Button>();
            var dragHandler  = hitUI.GetComponentInParent<IDragHandler>();
            var clickHandler = hitUI.GetComponentInParent<IPointerClickHandler>();

            if (slider != null)
            {
                Debug.Log($"[Interaction] UI hit candidate: {hitUI.name} at {pointerPosition}");
                clickTarget = slider.gameObject;
                hitRaycast = result;
                supportsDrag = true;
                return true;
            }

            if (button != null)
            {
                Debug.Log($"[Interaction] UI hit candidate: {hitUI.name} at {pointerPosition}");
                clickTarget = button.gameObject;
                hitRaycast = result;
                supportsDrag = false;
                return true;
            }

            if (dragHandler != null)
            {
                Debug.Log($"[Interaction] UI drag candidate: {hitUI.name} at {pointerPosition}");
                Component dragComponent = dragHandler as Component;
                clickTarget = dragComponent != null ? dragComponent.gameObject : hitUI;
                hitRaycast = result;
                supportsDrag = true;
                return true;
            }

            if (clickHandler != null)
            {
                Debug.Log($"[Interaction] UI click candidate: {hitUI.name} at {pointerPosition}");
                Component clickComponent = clickHandler as Component;
                clickTarget = clickComponent != null ? clickComponent.gameObject : hitUI;
                hitRaycast = result;
                return true;
            }
        }
        return false;
    }

    private void ExecuteUiClick(GameObject clickTarget, PointerEventData pointerData)
    {
        if (clickTarget == null || EventSystem.current == null)
        {
            return;
        }

        Debug.Log($"[Interaction] UI click executed on: {clickTarget.name}");
        ExecuteUiEventChain(clickTarget, pointerData, ExecuteEvents.pointerClickHandler);
    }

    private void ExecuteUiPointerEnter(GameObject target, PointerEventData pointerData)
    {
        if (target == null || EventSystem.current == null)
        {
            return;
        }

        ExecuteUiEventChain(target, pointerData, ExecuteEvents.pointerEnterHandler);
    }

    private void ExecuteUiPointerDown(GameObject target, PointerEventData pointerData)
    {
        if (target == null || EventSystem.current == null)
        {
            return;
        }

        pointerData.pressPosition = pointerData.position;
        pointerData.pointerPress = target;
        pointerData.rawPointerPress = target;
        pointerData.pointerDrag = target;
        pointerData.eligibleForClick = true;
        pointerData.pointerPressRaycast = pointerData.pointerCurrentRaycast;
        ExecuteUiEventChain(target, pointerData, ExecuteEvents.pointerDownHandler);
    }

    private void ExecuteUiInitializePotentialDrag(GameObject target, PointerEventData pointerData)
    {
        if (target == null || EventSystem.current == null)
        {
            return;
        }

        pointerData.useDragThreshold = false;
        ExecuteUiEventChain(target, pointerData, ExecuteEvents.initializePotentialDrag);
    }

    private void ExecuteUiBeginDrag(GameObject target, PointerEventData pointerData)
    {
        if (target == null || EventSystem.current == null)
        {
            return;
        }

        pointerData.dragging = true;
        ExecuteUiEventChain(target, pointerData, ExecuteEvents.beginDragHandler);
    }

    private void ExecuteUiDrag(GameObject target, PointerEventData pointerData)
    {
        if (target == null || EventSystem.current == null)
        {
            return;
        }

        ExecuteUiEventChain(target, pointerData, ExecuteEvents.dragHandler);
    }

    private void ExecuteUiEndDrag(GameObject target, PointerEventData pointerData)
    {
        if (target == null || EventSystem.current == null)
        {
            return;
        }

        ExecuteUiEventChain(target, pointerData, ExecuteEvents.endDragHandler);
        pointerData.dragging = false;
    }

    private void ExecuteUiPointerUp(GameObject target, PointerEventData pointerData)
    {
        if (target == null || EventSystem.current == null)
        {
            return;
        }

        ExecuteUiEventChain(target, pointerData, ExecuteEvents.pointerUpHandler);
    }

    private void ExecuteUiPointerExit(GameObject target, PointerEventData pointerData)
    {
        if (target == null || EventSystem.current == null)
        {
            return;
        }

        ExecuteUiEventChain(target, pointerData, ExecuteEvents.pointerExitHandler);
    }

    private void ExecuteUiEventChain<T>(GameObject target, PointerEventData pointerData, ExecuteEvents.EventFunction<T> handler)
        where T : IEventSystemHandler
    {
        Transform current = target != null ? target.transform : null;
        while (current != null)
        {
            ExecuteEvents.Execute(current.gameObject, pointerData, handler);
            current = current.parent;
        }
    }

    private PointerEventData CreatePointerEventData(int handIndex, Vector2 screenPos, GameObject hoveredTarget, RaycastResult raycast)
    {
        var pointerData = new PointerEventData(EventSystem.current)
        {
            pointerId = handIndex,
            position = screenPos,
            pressPosition = screenPos,
            pointerCurrentRaycast = raycast,
            pointerPressRaycast = raycast,
            pointerEnter = hoveredTarget,
            pointerPress = hoveredTarget,
            rawPointerPress = hoveredTarget,
            button = PointerEventData.InputButton.Left
        };

        return pointerData;
    }

    private void UpdatePointerEventData(int handIndex, Vector2 screenPos, PointerEventData pointerData, Vector2? overrideDelta = null)
    {
        if (pointerData == null)
        {
            return;
        }

        Vector2 delta = overrideDelta ?? (screenPos - pointerData.position);
        pointerData.delta = delta;
        pointerData.position = screenPos;

        if (TryFindUIHitTargetAtPosition(screenPos, out GameObject currentTarget, out RaycastResult currentRaycast, out bool _))
        {
            pointerData.pointerCurrentRaycast = currentRaycast;
            pointerData.pointerEnter = currentTarget;
        }
        else
        {
            pointerData.pointerCurrentRaycast = default;
        }
    }

    private void ResetUiInteractionState(int handIndex)
    {
        _pendingUiTargets[handIndex] = null;
        _activeUiPointerData[handIndex] = null;
        _activeUiSupportsDrag[handIndex] = false;
        _activeUiDragging[handIndex] = false;
    }

    private void OnDestroy()
    {
        if (HandTracking != null)
        {
            HandTracking.OnPinchDown -= HandlePinchDown;
            HandTracking.OnPinchDrag -= HandlePinchDrag;
            HandTracking.OnPinchUp   -= HandlePinchUp;
        }
    }
}
