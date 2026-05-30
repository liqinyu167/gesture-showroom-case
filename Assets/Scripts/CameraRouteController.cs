using System;
using System.Collections;
using System.Collections.Generic;
using Cinemachine;
using UnityEngine;

public class CameraRouteController : MonoBehaviour
{
    private const float GestureLookEpsilon = 0.01f;

    [Header("Route")]
    public CameraRouteNode InitialNode;
    public bool AutoCollectChildNodes = true;
    public bool LoopRoute = true;
    public int ActivePriority = 100;
    public int InactivePriority = 10;

    [Header("Auto Tour")]
    public bool AutoPlay = true;
    [Min(0f)] public float ObservationDuration = 2f;
    [Min(0f)] public float TransitionInterval = 0.4f;
    [Min(0.01f)] public float TransitionDuration = 1.2f;
    public CinemachineBlendDefinition.Style BlendStyle = CinemachineBlendDefinition.Style.EaseInOut;

    [Header("Manual Timeout")]
    public bool AutoResumeAfterManualIdle = true;
    [Min(0f)] public float ManualIdleResumeDelay = 10f;
    public bool SuspendAutoTourWhileUserActive = true;

    [Header("Camera")]
    public bool AutoBindMainCamera = true;
    public Camera ControlledCamera;

    [Header("Gesture Look")]
    public bool GestureLookEnabled = false;
    [Min(0.01f)] public float GestureLookYawSensitivity = 0.09f;
    [Min(0.01f)] public float GestureLookPitchSensitivity = 0.03f;
    [Min(0f)] public float GestureLookMaxYaw = 42f;
    [Min(0f)] public float GestureLookMaxPitch = 10f;
    [Min(0f)] public float GestureLookIdleResetDelay = 2.5f;
    [Min(0.01f)] public float GestureLookResetSpeed = 8f;

    private readonly List<CameraRouteNode> _nodes = new List<CameraRouteNode>();
    private readonly Dictionary<CameraRouteNode, Vector3> _nodeBaseLocalPositions = new Dictionary<CameraRouteNode, Vector3>();
    private readonly Dictionary<CameraRouteNode, Quaternion> _nodeBaseLocalRotations = new Dictionary<CameraRouteNode, Quaternion>();
    private CameraRouteNode _currentNode;
    private float _transitionEndTime = -1f;
    private Coroutine _tourRoutine;
    private Coroutine _transitionRoutine;
    private bool _lastAutoPlayState;
    private float _lastManualInteractionTime = float.NegativeInfinity;
    private bool _autoTourSuspendedByUserActivity;
    private bool _currentTransitionIsAuto;
    private int _gestureLookActiveHand = -1;
    private bool _isGestureLookActive;
    private Vector2 _lastGestureLookScreenPosition;
    private float _lastGestureLookInputTime = float.NegativeInfinity;
    private float _gestureLookYawOffset;
    private float _gestureLookPitchOffset;

    public event Action<CameraRouteNode> OnCurrentNodeChanged;

    public CameraRouteNode CurrentNode => _currentNode;
    public string CurrentNodeName => _currentNode != null ? _currentNode.ResolvedDisplayName : "--";
    public bool IsTransitioning => _transitionEndTime > 0f && Time.time < _transitionEndTime;
    public IReadOnlyList<CameraRouteNode> Nodes => _nodes;
    public int CurrentNodeIndex => _currentNode == null ? -1 : _nodes.IndexOf(_currentNode);
    public bool IsAutoPlayEnabled => AutoPlay;
    public bool IsGestureLookAvailable => GestureLookEnabled;

    private void Awake()
    {
        RefreshRoute();
        ActivateInitialNode();
        _lastAutoPlayState = AutoPlay;
        if (AutoPlay)
        {
            RestartAutoTour();
        }
    }

    private void Update()
    {
        if (_lastAutoPlayState != AutoPlay)
        {
            _lastAutoPlayState = AutoPlay;
            SetAutoPlay(AutoPlay, true);
        }

        if (!AutoPlay &&
            AutoResumeAfterManualIdle &&
            ManualIdleResumeDelay > 0f &&
            _transitionRoutine == null &&
            Time.time - _lastManualInteractionTime >= ManualIdleResumeDelay)
        {
            SetAutoPlay(true, true);
        }

        if (AutoPlay &&
            SuspendAutoTourWhileUserActive &&
            _autoTourSuspendedByUserActivity &&
            _transitionRoutine == null &&
            Time.time - _lastManualInteractionTime >= ManualIdleResumeDelay)
        {
            _autoTourSuspendedByUserActivity = false;
            RestartAutoTour();
        }

        UpdateGestureLookReset();
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            CacheNodes();
        }
    }

    private void OnDisable()
    {
        StopAllRunningCoroutines();
    }

    public void RefreshRoute()
    {
        CacheNodes();
        CacheNodeBasePoses();
        EnsureControlledCamera();
        ConfigureCinemachine();
    }

    public bool TryGetAdjacentNode(CameraRouteDirection direction, out CameraRouteNode targetNode)
    {
        targetNode = null;

        if (_currentNode == null || direction == CameraRouteDirection.None)
        {
            return false;
        }

        targetNode = _currentNode.GetNeighbor(direction);
        return targetNode != null;
    }

    public bool TryTransitionTo(CameraRouteNode targetNode, out string failureReason)
    {
        failureReason = null;

        EnsureControlledCamera();

        if (targetNode == null)
        {
            failureReason = "Missing target camera node.";
            return false;
        }

        if (targetNode.VirtualCamera == null)
        {
            failureReason = $"{targetNode.ResolvedDisplayName} has no virtual camera.";
            return false;
        }

        if (ControlledCamera == null)
        {
            failureReason = "Main camera is missing.";
            return false;
        }

        NotifyManualInteraction();
        StartTransition(targetNode, true, true, false);
        return true;
    }

    public void RestartAutoTour()
    {
        if (!AutoPlay || _nodes.Count <= 1)
        {
            return;
        }

        if (_tourRoutine != null)
        {
            StopCoroutine(_tourRoutine);
        }

        _tourRoutine = StartCoroutine(AutoTourLoop());
    }

    public void SetAutoPlay(bool enabled, bool restartIfNeeded = true)
    {
        AutoPlay = enabled;
        _lastAutoPlayState = enabled;

        if (!enabled)
        {
            if (_tourRoutine != null)
            {
                StopCoroutine(_tourRoutine);
                _tourRoutine = null;
            }

            return;
        }

        if (restartIfNeeded)
        {
            RestartAutoTour();
        }
    }

    public void NotifyManualInteraction()
    {
        _lastManualInteractionTime = Time.time;
    }

    public void SetGestureLookEnabled(bool enabled)
    {
        GestureLookEnabled = enabled;

        if (enabled)
        {
            return;
        }

        RestoreCurrentNodePoseImmediately();
        ResetGestureLookState();
    }

    public void NotifyUserActivity()
    {
        NotifyManualInteraction();

        if (!AutoPlay || !SuspendAutoTourWhileUserActive)
        {
            return;
        }

        if (!_autoTourSuspendedByUserActivity)
        {
            _autoTourSuspendedByUserActivity = true;

            if (_tourRoutine != null)
            {
                StopCoroutine(_tourRoutine);
                _tourRoutine = null;
            }

            if (_currentTransitionIsAuto && _transitionRoutine != null)
            {
                StopCoroutine(_transitionRoutine);
                _transitionRoutine = null;
                _transitionEndTime = -1f;
                _currentTransitionIsAuto = false;
            }
        }
    }

    public bool TryBeginGestureLook(int handIndex, Vector2 screenPosition)
    {
        if (!GestureLookEnabled || handIndex < 0 || _currentNode == null)
        {
            return false;
        }

        CaptureNodeBasePose(_currentNode);
        _gestureLookActiveHand = handIndex;
        _isGestureLookActive = true;
        _lastGestureLookScreenPosition = screenPosition;
        _lastGestureLookInputTime = Time.time;
        NotifyUserActivity();
        return true;
    }

    public bool TryUpdateGestureLook(int handIndex, Vector2 screenPosition)
    {
        if (!_isGestureLookActive || _currentNode == null)
        {
            return false;
        }

        Vector2 delta = screenPosition - _lastGestureLookScreenPosition;
        _lastGestureLookScreenPosition = screenPosition;

        _gestureLookYawOffset = Mathf.Clamp(
            _gestureLookYawOffset + delta.x * GestureLookYawSensitivity,
            -GestureLookMaxYaw,
            GestureLookMaxYaw);
        _gestureLookPitchOffset = Mathf.Clamp(
            _gestureLookPitchOffset - delta.y * GestureLookPitchSensitivity,
            -GestureLookMaxPitch,
            GestureLookMaxPitch);

        ApplyGestureLookOffset();
        _lastGestureLookInputTime = Time.time;
        NotifyUserActivity();
        return true;
    }

    public bool TryEndGestureLook(int handIndex)
    {
        if (!_isGestureLookActive)
        {
            return false;
        }

        _isGestureLookActive = false;
        _gestureLookActiveHand = -1;
        _lastGestureLookInputTime = Time.time;
        return true;
    }

    public bool TrySetNormalizedPosition(float normalizedValue, out CameraRouteNode targetNode)
    {
        targetNode = null;

        if (_nodes.Count == 0)
        {
            return false;
        }

        float clamped = Mathf.Clamp01(normalizedValue);
        int targetIndex = Mathf.RoundToInt(clamped * (_nodes.Count - 1));
        targetIndex = Mathf.Clamp(targetIndex, 0, _nodes.Count - 1);
        targetNode = _nodes[targetIndex];

        if (targetNode == null)
        {
            return false;
        }

        if (_transitionRoutine != null)
        {
            StopCoroutine(_transitionRoutine);
            _transitionRoutine = null;
        }

        NotifyManualInteraction();
        _transitionEndTime = -1f;
        ActivateNode(targetNode, false);
        return true;
    }

    private void ActivateInitialNode()
    {
        if (InitialNode == null && _nodes.Count > 0)
        {
            InitialNode = _nodes[0];
        }

        if (InitialNode == null)
        {
            Debug.LogWarning("[CameraRoute] No initial node configured.");
            return;
        }

        EnsureControlledCamera();
        ConfigureCinemachine();
        ActivateNode(InitialNode, false);
    }

    private void ActivateNode(CameraRouteNode targetNode, bool isTransition)
    {
        RestoreCurrentNodePoseImmediately();
        RestoreNodeTransform(targetNode, true);
        ResetGestureLookState();

        for (int i = 0; i < _nodes.Count; i++)
        {
            CameraRouteNode node = _nodes[i];
            if (node == null || node.VirtualCamera == null)
            {
                continue;
            }

            node.VirtualCamera.enabled = true;
            node.VirtualCamera.Priority = node == targetNode ? ActivePriority : InactivePriority;
        }

        _currentNode = targetNode;
        _transitionEndTime = isTransition ? Time.time + Mathf.Max(TransitionDuration, 0.01f) : -1f;
        OnCurrentNodeChanged?.Invoke(_currentNode);
        Debug.Log($"[CameraRoute] Active node: {CurrentNodeName}");
    }

    private void CacheNodes()
    {
        _nodes.Clear();

        CameraRouteNode[] foundNodes = AutoCollectChildNodes
            ? GetComponentsInChildren<CameraRouteNode>(true)
            : FindObjectsOfType<CameraRouteNode>(true);

        for (int i = 0; i < foundNodes.Length; i++)
        {
            CameraRouteNode node = foundNodes[i];
            if (node != null && !_nodes.Contains(node))
            {
                _nodes.Add(node);
            }
        }

        _nodes.Sort(CompareNodes);
    }

    private void CacheNodeBasePoses()
    {
        List<CameraRouteNode> staleNodes = null;
        foreach (CameraRouteNode cachedNode in _nodeBaseLocalRotations.Keys)
        {
            if (_nodes.Contains(cachedNode))
            {
                continue;
            }

            staleNodes ??= new List<CameraRouteNode>();
            staleNodes.Add(cachedNode);
        }

        if (staleNodes != null)
        {
            for (int i = 0; i < staleNodes.Count; i++)
            {
                CameraRouteNode staleNode = staleNodes[i];
                _nodeBaseLocalRotations.Remove(staleNode);
                _nodeBaseLocalPositions.Remove(staleNode);
            }
        }

        for (int i = 0; i < _nodes.Count; i++)
        {
            CaptureNodeBasePose(_nodes[i]);
        }
    }

    private void CaptureNodeBasePose(CameraRouteNode node)
    {
        if (node == null)
        {
            return;
        }

        _nodeBaseLocalPositions[node] = node.transform.localPosition;
        _nodeBaseLocalRotations[node] = node.transform.localRotation;
    }

    private int CompareNodes(CameraRouteNode a, CameraRouteNode b)
    {
        if (ReferenceEquals(a, b))
        {
            return 0;
        }

        if (a == null)
        {
            return 1;
        }

        if (b == null)
        {
            return -1;
        }

        int orderComparison = a.Order.CompareTo(b.Order);
        if (orderComparison != 0)
        {
            return orderComparison;
        }

        return string.Compare(a.ResolvedDisplayName, b.ResolvedDisplayName, StringComparison.OrdinalIgnoreCase);
    }

    private void EnsureControlledCamera()
    {
        if (ControlledCamera == null && AutoBindMainCamera)
        {
            ControlledCamera = Camera.main;
        }
    }

    private void ConfigureCinemachine()
    {
        EnsureControlledCamera();

        if (ControlledCamera != null)
        {
            CinemachineBrain brain = ControlledCamera.GetComponent<CinemachineBrain>();
            if (brain == null)
            {
                brain = ControlledCamera.gameObject.AddComponent<CinemachineBrain>();
            }

            brain.enabled = true;
            brain.m_DefaultBlend = new CinemachineBlendDefinition(BlendStyle, TransitionDuration);
        }

        for (int i = 0; i < _nodes.Count; i++)
        {
            CameraRouteNode node = _nodes[i];
            if (node != null && node.VirtualCamera != null)
            {
                node.VirtualCamera.enabled = true;
                node.VirtualCamera.Priority = InactivePriority;
            }
        }
    }

    private void UpdateGestureLookReset()
    {
        if (_currentNode == null || _isGestureLookActive || !HasGestureLookOffset())
        {
            return;
        }

        if (GestureLookEnabled &&
            GestureLookIdleResetDelay > 0f &&
            Time.time - _lastGestureLookInputTime < GestureLookIdleResetDelay)
        {
            return;
        }

        float lerpFactor = 1f - Mathf.Exp(-GestureLookResetSpeed * Time.deltaTime);
        _gestureLookYawOffset = Mathf.Lerp(_gestureLookYawOffset, 0f, lerpFactor);
        _gestureLookPitchOffset = Mathf.Lerp(_gestureLookPitchOffset, 0f, lerpFactor);

        if (!HasGestureLookOffset())
        {
            RestoreCurrentNodePoseImmediately();
            return;
        }

        ApplyGestureLookOffset();
    }

    private bool HasGestureLookOffset()
    {
        return Mathf.Abs(_gestureLookYawOffset) > GestureLookEpsilon ||
            Mathf.Abs(_gestureLookPitchOffset) > GestureLookEpsilon;
    }

    private void ApplyGestureLookOffset()
    {
        if (_currentNode == null)
        {
            return;
        }

        if (!_nodeBaseLocalRotations.TryGetValue(_currentNode, out Quaternion baseRotation))
        {
            CaptureNodeBasePose(_currentNode);
            baseRotation = _currentNode.transform.localRotation;
        }

        if (!_nodeBaseLocalPositions.TryGetValue(_currentNode, out Vector3 basePosition))
        {
            CaptureNodeBasePose(_currentNode);
            basePosition = _currentNode.transform.localPosition;
        }

        _currentNode.transform.localPosition = basePosition;

        Quaternion yawRotation = Quaternion.AngleAxis(_gestureLookYawOffset, Vector3.up);
        Vector3 yawedForward = yawRotation * (baseRotation * Vector3.forward);
        Vector3 yawedRight = yawRotation * (baseRotation * Vector3.right);
        Quaternion pitchRotation = Quaternion.AngleAxis(_gestureLookPitchOffset, yawedRight);
        Vector3 adjustedForward = pitchRotation * yawedForward;

        if (adjustedForward.sqrMagnitude <= 0.0001f)
        {
            adjustedForward = yawedForward;
        }

        _currentNode.transform.rotation = Quaternion.LookRotation(adjustedForward.normalized, Vector3.up);
    }

    private void RestoreCurrentNodePoseImmediately()
    {
        RestoreNodeTransform(_currentNode, true);
    }

    private void RestoreNodeTransform(CameraRouteNode node, bool immediate)
    {
        if (node == null)
        {
            return;
        }

        if (!_nodeBaseLocalRotations.TryGetValue(node, out Quaternion baseRotation) ||
            !_nodeBaseLocalPositions.TryGetValue(node, out Vector3 basePosition))
        {
            CaptureNodeBasePose(node);
            baseRotation = node.transform.localRotation;
            basePosition = node.transform.localPosition;
        }

        node.transform.localPosition = basePosition;
        node.transform.localRotation = immediate
            ? baseRotation
            : Quaternion.Slerp(node.transform.localRotation, baseRotation, 1f);
    }

    private void ResetGestureLookState()
    {
        _gestureLookActiveHand = -1;
        _isGestureLookActive = false;
        _lastGestureLookScreenPosition = Vector2.zero;
        _gestureLookYawOffset = 0f;
        _gestureLookPitchOffset = 0f;
        _lastGestureLookInputTime = float.NegativeInfinity;
    }

    private void StartTransition(CameraRouteNode targetNode, bool restartTourAfterTransition, bool stopAutoTour, bool isAutoTransition)
    {
        if (_transitionRoutine != null)
        {
            StopCoroutine(_transitionRoutine);
        }

        if (stopAutoTour && _tourRoutine != null)
        {
            StopCoroutine(_tourRoutine);
            _tourRoutine = null;
        }

        _currentTransitionIsAuto = isAutoTransition;
        _transitionRoutine = StartCoroutine(TransitionToNodeCoroutine(targetNode, restartTourAfterTransition));
    }

    private IEnumerator TransitionToNodeCoroutine(CameraRouteNode targetNode, bool restartTourAfterTransition)
    {
        if (TransitionInterval > 0f)
        {
            yield return new WaitForSeconds(TransitionInterval);
        }

        ActivateNode(targetNode, true);

        while (IsTransitioning)
        {
            yield return null;
        }

        _transitionRoutine = null;
        _currentTransitionIsAuto = false;

        if (restartTourAfterTransition && AutoPlay && !_autoTourSuspendedByUserActivity)
        {
            RestartAutoTour();
        }
    }

    private IEnumerator AutoTourLoop()
    {
        while (AutoPlay && _nodes.Count > 1)
        {
            if (_autoTourSuspendedByUserActivity)
            {
                _tourRoutine = null;
                yield break;
            }

            if (ObservationDuration > 0f)
            {
                yield return new WaitForSeconds(ObservationDuration);
            }

            if (_autoTourSuspendedByUserActivity)
            {
                _tourRoutine = null;
                yield break;
            }

            CameraRouteNode nextNode = GetNextNode();
            if (nextNode == null || nextNode.VirtualCamera == null)
            {
                _tourRoutine = null;
                yield break;
            }

            StartTransition(nextNode, false, false, true);

            while (_transitionRoutine != null)
            {
                yield return null;
            }
        }

        _tourRoutine = null;
    }

    private CameraRouteNode GetNextNode()
    {
        if (_nodes.Count == 0)
        {
            return null;
        }

        int currentIndex = CurrentNodeIndex;
        if (currentIndex < 0)
        {
            return _nodes[0];
        }

        int nextIndex = currentIndex + 1;
        if (nextIndex >= _nodes.Count)
        {
            if (!LoopRoute)
            {
                return null;
            }

            nextIndex = 0;
        }

        return _nodes[nextIndex];
    }

    private void StopAllRunningCoroutines()
    {
        if (_tourRoutine != null)
        {
            StopCoroutine(_tourRoutine);
            _tourRoutine = null;
        }

        if (_transitionRoutine != null)
        {
            StopCoroutine(_transitionRoutine);
            _transitionRoutine = null;
        }

        _transitionEndTime = -1f;
        _currentTransitionIsAuto = false;
        ResetGestureLookState();
    }
}
