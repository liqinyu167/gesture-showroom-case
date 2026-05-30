using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mediapipe;
using Mediapipe.Tasks.Vision.HandLandmarker;
using Mediapipe.Tasks.Vision.GestureRecognizer;
using Mediapipe.Unity;
using mpu = Mediapipe.Unity;
using mpexp = Mediapipe.Unity.Experimental;
using mpsample = Mediapipe.Unity.Sample;
using Graphic = UnityEngine.UI.Graphic;

public class HandTrackingManagerV2 : MonoBehaviour
{
    private static readonly (int start, int end)[] HandConnections =
    {
        (0, 1), (1, 2), (2, 3), (3, 4),
        (0, 5), (5, 6), (6, 7), (7, 8),
        (5, 9), (9, 10), (10, 11), (11, 12),
        (9, 13), (13, 14), (14, 15), (15, 16),
        (13, 17), (0, 17), (17, 18), (18, 19), (19, 20),
    };

    [Header("Configuration")]
    public string ModelPath = "hand_landmarker.bytes";
    public int MaxHands = 2;
    public float MinDetectionConfidence = 0.5f;
    public float MinPresenceConfidence = 0.5f;
    public float MinTrackingConfidence = 0.5f;
    public string GestureRecognizerModelPath = "gesture_recognizer.bytes";

    [Header("Visualization")]
    [Range(0f, 1f)] public float CameraOpacity = 1.0f;
    public float SmoothSpeed = 20f;
    public bool InvertMirroring = false; // Manual override for mirroring
    public bool UseLegacyScreenOverlay = false;
    public bool ShowHandSkeletonOverlay = true;
    public bool ShowThumbTipOverlay = true;
    public bool ShowIndexTipOverlay = true;
    public bool EnableCannedGestureDebug = true;
    public float OverlayPointSize = 10f;
    public float OverlayLineWidth = 3f;
    public float ImageSourceStartupTimeoutSeconds = 12f;
    public int ImageSourcePlayRetries = 3;
    [SerializeField] private HandLandmarkerResultAnnotationController _annotationController;
    [SerializeField] private mpu.Screen _screen;

    private HandLandmarker _taskApi;
    private GestureRecognizer _gestureRecognizer;
    private HandLandmarkerResult _latestResult;
    private bool _hasResult = false;
    private HandLandmarkerResult _guiResult;
    private bool _hasGuiResult = false;
    private GestureRecognizerResult _latestGestureResult;
    private bool _hasGestureResult = false;
    private GestureRecognizerResult _guiGestureResult;
    private bool _hasGuiGestureResult = false;
    private string[] _stableGestureLabels = new string[2];
    private float[] _stableGestureScores = new float[2];
    private string[] _lastGestureCandidates = new string[2];
    private int[] _gestureCandidateFrames = new int[2];
    private int[] _noneGestureFrames = new int[2];
    private static readonly object _resultLock = new object();
    private Coroutine _runCoroutine;
    private mpexp.TextureFramePool _textureFramePool;
    private mpexp.TextureFramePool _gestureTextureFramePool;

    [Header("Interaction (Pinch)")]
    [Tooltip("捏合判定阈值：大拇指尖和食指尖距离多近才算作捏合。调大判定更宽松。")]
    public float PinchDetectionThreshold = 0.05f;
    [Tooltip("松手防抖延迟：手指偶尔张开一下时，系统等待多久才判定松手。经常断触可调大。")]
    public float PinchReleaseToleranceTime = 0.15f;
    [Tooltip("手部丢失容错：手突然移出画面时保留状态的时间。遇到丢帧直接掉落可调大。")]
    public float HandLossToleranceTime = 0.3f;
    public float SlotReuseDistancePixels = 220f;
    public float DuplicatePalmDistancePixels = 140f;
    public float DuplicateCursorDistancePixels = 120f;
    public event System.Action<int, Vector2> OnPinchDown;
    public event System.Action<int, Vector2> OnPinchUp;
    public event System.Action<int, Vector2> OnPinchDrag;
    
    public Vector2[] CursorScreenPositions { get; private set; } = new Vector2[2];
    public bool[] IsPinching { get; private set; } = new bool[2];
    public string[] Handedness = new string[2] { "Unknown", "Unknown" };

    // Smoothed positions for fingertip overlays.
    public Vector2[] SmoothedIndexTipPositions => _smoothedIndexTipPositions;
    public Vector2[] SmoothedThumbTipPositions => _smoothedThumbTipPositions;
    private Vector2[] _smoothedIndexTipPositions = new Vector2[2];
    private Vector2[] _smoothedThumbTipPositions = new Vector2[2];
    private Vector2[] _palmCenterScreenPositions = new Vector2[2];
    private bool[] _openPalmCandidates = new bool[2];
    private bool[] _handActive = new bool[2];
    private Vector2[] _previousIndexTipPositions = new Vector2[2];
    private Vector2[] _previousThumbTipPositions = new Vector2[2];
    private Vector2[] _previousPalmCenterPositions = new Vector2[2];
    private Vector2[] _indexTipVelocity = new Vector2[2];
    private Vector2[] _thumbTipVelocity = new Vector2[2];
    private Vector2[] _palmCenterVelocity = new Vector2[2];
    private Vector2[] _indexTipAcceleration = new Vector2[2];
    private Vector2[] _thumbTipAcceleration = new Vector2[2];
    private Vector2[] _palmCenterAcceleration = new Vector2[2];
    private float[] _pinchDistancePixels = new float[2];
    private float[] _pinchDistanceNormalized = new float[2];
    private float[] _pinchReleaseTimers = new float[2];
    private float[] _handLossTimers = new float[2];
    private int[] _resolvedSlotByRawHandIndex = new int[2];
    private bool _singlePinchLockActive = false;
    private int _singlePinchLockedHand = -1;

    // GUI Styles
    private GUIStyle _leftHandStyle;
    private GUIStyle _rightHandStyle;
    private CanvasGroup _cameraCanvasGroup;
    private Graphic _cameraScreenGraphic;
    private Texture2D _guiTexture;

    private void Start()
    {
        // Configure Camera Opacity
        GameObject screenObj = _screen != null ? _screen.gameObject : GameObject.Find("Screen");
        if (screenObj != null)
        {
            _cameraCanvasGroup = screenObj.GetComponent<CanvasGroup>();
            if (_cameraCanvasGroup == null)
            {
                _cameraCanvasGroup = screenObj.AddComponent<CanvasGroup>();
            }
            _cameraScreenGraphic = screenObj.GetComponent<Graphic>();
            ApplyCameraOpacity();
        }

        // Setup GUI Styles
        _leftHandStyle = new GUIStyle();
        _leftHandStyle.normal.textColor = UnityEngine.Color.red;
        _leftHandStyle.fontSize = 20;
        _leftHandStyle.fontStyle = FontStyle.Bold;

        _rightHandStyle = new GUIStyle();
        _rightHandStyle.normal.textColor = UnityEngine.Color.green;
        _rightHandStyle.fontSize = 20;
        _rightHandStyle.fontStyle = FontStyle.Bold;

        _guiTexture = new Texture2D(1, 1);
        _guiTexture.SetPixel(0, 0, UnityEngine.Color.white);
        _guiTexture.Apply();

        SyncAnnotationVisibility();

        // Start Tracking
        _runCoroutine = StartCoroutine(Run());
    }

    private void Update()
    {
        if (_cameraCanvasGroup != null)
        {
            ApplyCameraOpacity();
        }

        // Smooth landmarks
        lock (_resultLock)
        {
            if (_hasResult)
            {
                // Results are deep-cloned in callbacks, so a struct copy is enough here.
                _guiResult = _latestResult;
                _hasGuiResult = true;
                if (_hasGestureResult)
                {
                    _guiGestureResult = _latestGestureResult;
                    _hasGuiGestureResult = true;
                }

                if (_guiResult.handLandmarks != null)
                {
                    bool[] occupiedSlots = new bool[MaxHands];
                    List<Vector2> acceptedPalmCenters = new List<Vector2>(MaxHands);
                    List<Vector2> acceptedCursorCenters = new List<Vector2>(MaxHands);
                    List<string> acceptedHandedness = new List<string>(MaxHands);
                    bool leftHandAccepted = false;
                    bool rightHandAccepted = false;
                    EnsureRawHandMappingCapacity(_guiResult.handLandmarks.Count);
                    for (int i = 0; i < _resolvedSlotByRawHandIndex.Length; i++)
                    {
                        _resolvedSlotByRawHandIndex[i] = -1;
                    }

                    for (int rawHandIndex = 0; rawHandIndex < _guiResult.handLandmarks.Count; rawHandIndex++)
                    {
                        var landmarks = _guiResult.handLandmarks[rawHandIndex];
                        string handedness = GetHandednessLabel(_guiResult, rawHandIndex);
                        Vector2 palmCenterPos = Vector2.zero;
                        Vector2 cursorCenterPos = Vector2.zero;

                        if (landmarks.landmarks != null && landmarks.landmarks.Count > 8)
                        {
                            palmCenterPos = GetPalmCenterScreenPoint(landmarks.landmarks);
                            cursorCenterPos = GetCursorCenterScreenPoint(landmarks.landmarks);
                        }

                        if (IsDuplicateHandDetection(handedness, palmCenterPos, cursorCenterPos, acceptedPalmCenters, acceptedCursorCenters, acceptedHandedness))
                        {
                            continue;
                        }

                        if ((handedness == "Left" && leftHandAccepted) || (handedness == "Right" && rightHandAccepted))
                        {
                            continue;
                        }

                        int handSlot = ResolveHandSlot(handedness, rawHandIndex, palmCenterPos, occupiedSlots);

                        if (handSlot < 0 || handSlot >= MaxHands)
                        {
                            continue;
                        }

                        if (_singlePinchLockActive && handSlot != _singlePinchLockedHand)
                        {
                            continue;
                        }

                        occupiedSlots[handSlot] = true;
                        _resolvedSlotByRawHandIndex[rawHandIndex] = handSlot;
                        Handedness[handSlot] = handedness;
                        acceptedPalmCenters.Add(palmCenterPos);
                        acceptedCursorCenters.Add(cursorCenterPos);
                        acceptedHandedness.Add(handedness);
                        if (handedness == "Left")
                        {
                            leftHandAccepted = true;
                        }
                        else if (handedness == "Right")
                        {
                            rightHandAccepted = true;
                        }

                        if (landmarks.landmarks != null && landmarks.landmarks.Count > 8)
                        {
                            EnsureHandTrackingCapacity(handSlot + 1);
                            UpdateStableGestureStateForSlot(rawHandIndex, handSlot);
                            _openPalmCandidates[handSlot] = IsOpenPalmLike(landmarks.landmarks);

                            var indexTip = landmarks.landmarks[8];
                            var thumbTip = landmarks.landmarks[4];
                            Vector2 indexTargetPos = ToScreenPoint(indexTip);
                            Vector2 thumbTargetPos = ToScreenPoint(thumbTip);

                            if (!_handActive[handSlot])
                            {
                                _smoothedIndexTipPositions[handSlot] = indexTargetPos;
                                _smoothedThumbTipPositions[handSlot] = thumbTargetPos;
                                _previousIndexTipPositions[handSlot] = indexTargetPos;
                                _previousThumbTipPositions[handSlot] = thumbTargetPos;
                                _previousPalmCenterPositions[handSlot] = palmCenterPos;
                                _palmCenterScreenPositions[handSlot] = palmCenterPos;
                                _indexTipVelocity[handSlot] = Vector2.zero;
                                _thumbTipVelocity[handSlot] = Vector2.zero;
                                _palmCenterVelocity[handSlot] = Vector2.zero;
                                _indexTipAcceleration[handSlot] = Vector2.zero;
                                _thumbTipAcceleration[handSlot] = Vector2.zero;
                                _palmCenterAcceleration[handSlot] = Vector2.zero;
                                _handActive[handSlot] = true;
                            }
                            else
                            {
                                float deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
                                UpdateKinematics(handSlot, indexTargetPos, thumbTargetPos, palmCenterPos, deltaTime);
                                _smoothedIndexTipPositions[handSlot] = Vector2.Lerp(_smoothedIndexTipPositions[handSlot], indexTargetPos, Time.deltaTime * SmoothSpeed);
                                _smoothedThumbTipPositions[handSlot] = Vector2.Lerp(_smoothedThumbTipPositions[handSlot], thumbTargetPos, Time.deltaTime * SmoothSpeed);
                                _palmCenterScreenPositions[handSlot] = palmCenterPos;
                            }

                            _pinchDistancePixels[handSlot] = Vector2.Distance(thumbTargetPos, indexTargetPos);
                            _pinchDistanceNormalized[handSlot] = Vector2.Distance(
                                new Vector2(thumbTip.x, thumbTip.y),
                                new Vector2(indexTip.x, indexTip.y)
                            );

                            // Interaction Logic (Average of smoothed thumb and index, converting from GUI space to Screen space)
                            Vector2 guiCursor = (_smoothedIndexTipPositions[handSlot] + _smoothedThumbTipPositions[handSlot]) / 2f;
                            Vector2 screenCursor = new Vector2(guiCursor.x, UnityEngine.Screen.height - guiCursor.y);
                            CursorScreenPositions[handSlot] = screenCursor;

                            bool isPinchingNow = _pinchDistanceNormalized[handSlot] < PinchDetectionThreshold;
                            
                            if (isPinchingNow)
                            {
                                _pinchReleaseTimers[handSlot] = 0f; // reset tolerance
                                if (!IsPinching[handSlot])
                                {
                                    if (!_singlePinchLockActive && !HasOtherTrackedHand(handSlot, occupiedSlots))
                                    {
                                        _singlePinchLockActive = true;
                                        _singlePinchLockedHand = handSlot;
                                    }

                                    IsPinching[handSlot] = true;
                                    OnPinchDown?.Invoke(handSlot, screenCursor);
                                }
                                else
                                {
                                    OnPinchDrag?.Invoke(handSlot, screenCursor);
                                }
                            }
                            else if (IsPinching[handSlot])
                            {
                                _pinchReleaseTimers[handSlot] += Time.deltaTime;
                                if (_pinchReleaseTimers[handSlot] >= PinchReleaseToleranceTime)
                                {
                                    IsPinching[handSlot] = false;
                                    ReleaseSinglePinchLockIfNeeded(handSlot);
                                    OnPinchUp?.Invoke(handSlot, screenCursor);
                                }
                                else
                                {
                                    // Tolerance active: simulate drag
                                    OnPinchDrag?.Invoke(handSlot, screenCursor);
                                }
                            }
                        }
                    }

                    for (int i = 0; i < MaxHands; i++)
                    {
                        if (!occupiedSlots[i])
                        {
                            _handActive[i] = false;
                            Handedness[i] = "Unknown";
                            _palmCenterScreenPositions[i] = Vector2.zero;
                            _openPalmCandidates[i] = false;
                            ResetStableGestureState(i);
                            if (!IsPinching[i])
                            {
                                CursorScreenPositions[i] = new Vector2(-1000, -1000);
                            }
                        }
                    }

                    if (_singlePinchLockActive && !IsPinching[_singlePinchLockedHand] && !occupiedSlots[_singlePinchLockedHand])
                    {
                        ClearSinglePinchLock();
                    }
                }
                else
                {
                    for (int i = 0; i < MaxHands; i++)
                    {
                        _handActive[i] = false;
                        _palmCenterScreenPositions[i] = Vector2.zero;
                        _openPalmCandidates[i] = false;
                        ResetStableGestureState(i);
                    }
                    ClearSinglePinchLock();
                }
            }
            else
            {
                for (int i = 0; i < MaxHands; i++)
                {
                    _handActive[i] = false;
                    _palmCenterScreenPositions[i] = Vector2.zero;
                    _openPalmCandidates[i] = false;
                    ResetStableGestureState(i);
                }
                ClearSinglePinchLock();
            }
        }

        // Cleanup off-screen hand states with tolerance
        for (int i = 0; i < MaxHands; i++)
        {
            if (!_handActive[i])
            {
                _handLossTimers[i] += Time.deltaTime;
                if (_handLossTimers[i] >= HandLossToleranceTime)
                {
                    if (IsPinching[i])
                    {
                        IsPinching[i] = false;
                        ReleaseSinglePinchLockIfNeeded(i);
                        OnPinchUp?.Invoke(i, CursorScreenPositions[i]);
                    }
                    CursorScreenPositions[i] = new Vector2(-1000, -1000); // Hide cursor far off-screen
                }
            }
            else
            {
                _handLossTimers[i] = 0f;
            }
        }

        // Safety check for annotation controller
        if (_annotationController == null)
        {
            _annotationController = FindObjectOfType<HandLandmarkerResultAnnotationController>();
            SyncAnnotationVisibility();
        }
    }

    private string GetHandednessLabel(HandLandmarkerResult result, int rawHandIndex)
    {
        if (result.handedness != null && rawHandIndex < result.handedness.Count)
        {
            var hand = result.handedness[rawHandIndex];
            if (hand.categories != null && hand.categories.Count > 0 && !string.IsNullOrEmpty(hand.categories[0].categoryName))
            {
                return hand.categories[0].categoryName;
            }
        }

        return "Unknown";
    }

    private int ResolveHandSlot(string handedness, int rawHandIndex, Vector2 palmCenterPos, bool[] occupiedSlots)
    {
        int reusableSlot = FindReusableHandSlot(palmCenterPos, occupiedSlots);
        if (reusableSlot >= 0)
        {
            return reusableSlot;
        }

        int preferredSlot = -1;
        if (handedness == "Left")
        {
            preferredSlot = 0;
        }
        else if (handedness == "Right")
        {
            preferredSlot = 1;
        }

        if (preferredSlot >= 0 && preferredSlot < occupiedSlots.Length && !occupiedSlots[preferredSlot])
        {
            return preferredSlot;
        }

        for (int i = 0; i < occupiedSlots.Length; i++)
        {
            if (!occupiedSlots[i])
            {
                return i;
            }
        }

        return Mathf.Clamp(rawHandIndex, 0, occupiedSlots.Length - 1);
    }

    private int FindReusableHandSlot(Vector2 palmCenterPos, bool[] occupiedSlots)
    {
        if (palmCenterPos == Vector2.zero)
        {
            return -1;
        }

        int bestSlot = -1;
        float bestDistanceSqr = SlotReuseDistancePixels * SlotReuseDistancePixels;
        int slotCount = Mathf.Min(MaxHands, _previousPalmCenterPositions.Length);

        for (int i = 0; i < slotCount; i++)
        {
            if (occupiedSlots[i] || !_handActive[i])
            {
                continue;
            }

            float distanceSqr = (_previousPalmCenterPositions[i] - palmCenterPos).sqrMagnitude;
            if (distanceSqr <= bestDistanceSqr)
            {
                bestDistanceSqr = distanceSqr;
                bestSlot = i;
            }
        }

        return bestSlot;
    }

    private IEnumerator Run()
    {
        Debug.Log("HandTrackingManagerV2: Run() iteration started");
        // Wait for Bootstrap if it exists in the scene
        var bootstrap = FindObjectOfType<mpsample.Bootstrap>();
        if (bootstrap != null)
        {
            Debug.Log("HandTrackingManagerV2: Waiting for Bootstrap initialization...");
            yield return new WaitUntil(() => bootstrap.isFinished);
            Debug.Log("HandTrackingManagerV2: Bootstrap finished!");
        }

        Debug.Log($"HandTrackingManagerV2: Preparing model asset: {ModelPath}");
        yield return mpsample.AssetLoader.PrepareAssetAsync(ModelPath);
        if (EnableCannedGestureDebug)
        {
            Debug.Log($"HandTrackingManagerV2: Preparing gesture recognizer model asset: {GestureRecognizerModelPath}");
            yield return mpsample.AssetLoader.PrepareAssetAsync(GestureRecognizerModelPath);
        }
        Debug.Log("HandTrackingManagerV2: Model asset prepared!");

        var options = new HandLandmarkerOptions(
            new Mediapipe.Tasks.Core.BaseOptions(Mediapipe.Tasks.Core.BaseOptions.Delegate.CPU, modelAssetPath: ModelPath),
            runningMode: Mediapipe.Tasks.Vision.Core.RunningMode.LIVE_STREAM,
            numHands: MaxHands,
            minHandDetectionConfidence: MinDetectionConfidence,
            minHandPresenceConfidence: MinPresenceConfidence,
            minTrackingConfidence: MinTrackingConfidence,
            resultCallback: OnOutput
        );

        _taskApi = HandLandmarker.CreateFromOptions(options, GpuManager.GpuResources);
        Debug.Log("HandTrackingManagerV2: HandLandmarker created!");

        if (EnableCannedGestureDebug)
        {
            var gestureOptions = new GestureRecognizerOptions(
                new Mediapipe.Tasks.Core.BaseOptions(Mediapipe.Tasks.Core.BaseOptions.Delegate.CPU, modelAssetPath: GestureRecognizerModelPath),
                runningMode: Mediapipe.Tasks.Vision.Core.RunningMode.LIVE_STREAM,
                numHands: MaxHands,
                minHandDetectionConfidence: MinDetectionConfidence,
                minHandPresenceConfidence: MinPresenceConfidence,
                minTrackingConfidence: MinTrackingConfidence,
                resultCallback: OnGestureOutput
            );
            _gestureRecognizer = GestureRecognizer.CreateFromOptions(gestureOptions, GpuManager.GpuResources);
            Debug.Log("HandTrackingManagerV2: GestureRecognizer created!");
        }

        yield return WaitForImageSourceProvider();

        var imageSource = mpsample.ImageSourceProvider.ImageSource;
        if (imageSource == null)
        {
            Debug.LogError("HandTrackingManagerV2: ImageSource is still null after waiting. Check Bootstrap and AppSettings in the scene.");
            yield break;
        }

        Debug.Log($"HandTrackingManagerV2: Starting ImageSource ({mpsample.ImageSourceProvider.CurrentSourceType})...");
        yield return PlayImageSourceWithRetry(imageSource);
        Debug.Log($"HandTrackingManagerV2: ImageSource playing: {imageSource.isPlaying}, Resolution: {imageSource.textureWidth}x{imageSource.textureHeight}");

        if (!imageSource.isPrepared)
        {
            Debug.LogError("Failed to start ImageSource");
            yield break;
        }

        _textureFramePool = new mpexp.TextureFramePool(imageSource.textureWidth, imageSource.textureHeight, TextureFormat.RGBA32, 10);
        if (_gestureRecognizer != null)
        {
            _gestureTextureFramePool = new mpexp.TextureFramePool(imageSource.textureWidth, imageSource.textureHeight, TextureFormat.RGBA32, 10);
        }

        if (_screen != null)
        {
            Debug.Log("Initializing Screen component");
            _screen.Initialize(imageSource);
        }
        else
        {
            Debug.LogWarning("Screen component NOT assigned!");
        }
        if (_annotationController != null && !UseLegacyScreenOverlay)
        {
            // Match the official sample setup: Screen handles UV flipping, annotation stays unmirrored.
            _annotationController.isMirrored = InvertMirroring;
            _annotationController.imageSize = new Vector2Int(imageSource.textureWidth, imageSource.textureHeight);
            Debug.Log($"HandTrackingManagerV2: Annotation mirroring set to: {_annotationController.isMirrored}");
        }

        var transformationOptions = imageSource.GetTransformationOptions();
        var flipHorizontally = transformationOptions.flipHorizontally;
        var flipVertically = transformationOptions.flipVertically;
        var imageProcessingOptions = new Mediapipe.Tasks.Vision.Core.ImageProcessingOptions(rotationDegrees: (int)transformationOptions.rotationAngle);

        while (true)
        {
            if (!_textureFramePool.TryGetTextureFrame(out var textureFrame))
            {
                yield return new WaitForEndOfFrame();
                continue;
            }

            // Sync with render thread for CPU read
            yield return new WaitForEndOfFrame();
            textureFrame.ReadTextureOnCPU(imageSource.GetCurrentTexture(), flipHorizontally, flipVertically);
            var image = textureFrame.BuildCPUImage();
            textureFrame.Release();

            if (_taskApi != null)
            {
                var timestamp = GetCurrentTimestampMillisec();
                _taskApi.DetectAsync(image, timestamp, imageProcessingOptions);
                if (_gestureRecognizer != null)
                {
                    if (_gestureTextureFramePool != null && _gestureTextureFramePool.TryGetTextureFrame(out var gestureTextureFrame))
                    {
                        yield return new WaitForEndOfFrame();
                        gestureTextureFrame.ReadTextureOnCPU(imageSource.GetCurrentTexture(), flipHorizontally, flipVertically);
                        var gestureImage = gestureTextureFrame.BuildCPUImage();
                        gestureTextureFrame.Release();
                        _gestureRecognizer.RecognizeAsync(gestureImage, timestamp, imageProcessingOptions);
                    }
                }
            }
        }
    }

    private IEnumerator WaitForImageSourceProvider()
    {
        var bootstrap = FindObjectOfType<mpsample.Bootstrap>();
        float startTime = Time.realtimeSinceStartup;

        while (mpsample.ImageSourceProvider.ImageSource == null)
        {
            if (bootstrap != null && bootstrap.isFinished)
            {
                break;
            }

            if (Time.realtimeSinceStartup - startTime >= ImageSourceStartupTimeoutSeconds)
            {
                break;
            }

            yield return null;
        }
    }

    private IEnumerator PlayImageSourceWithRetry(mpu.ImageSource imageSource)
    {
        int maxAttempts = Mathf.Max(1, ImageSourcePlayRetries);

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            if (attempt > 1)
            {
                Debug.LogWarning($"HandTrackingManagerV2: Retrying ImageSource startup ({attempt}/{maxAttempts})...");
                imageSource.Stop();
                yield return new WaitForSeconds(0.5f);
            }

            bool failed = false;
            var playRoutine = imageSource.Play();
            while (true)
            {
                object current = null;
                bool hasNext = false;
                try
                {
                    hasNext = playRoutine.MoveNext();
                    if (hasNext)
                    {
                        current = playRoutine.Current;
                    }
                }
                catch (System.Exception ex)
                {
                    failed = true;
                    Debug.LogWarning($"HandTrackingManagerV2: ImageSource.Play failed on attempt {attempt}/{maxAttempts}: {ex.Message}");
                    break;
                }

                if (!hasNext)
                {
                    break;
                }

                yield return current;
            }

            if (!failed && imageSource.isPrepared && imageSource.isPlaying)
            {
                yield break;
            }
        }
    }

    private int _logCounter = 0;
    private void OnOutput(HandLandmarkerResult result, Image image, long timestamp)
    {
        if (image == null)
        {
            Debug.LogWarning("[HandTracking] OnOutput received null image");
        }

        lock (_resultLock)
        {
            var clonedResult = default(HandLandmarkerResult);
            result.CloneTo(ref clonedResult);
            _latestResult = clonedResult;
            _hasResult = true;
        }

        if (_annotationController != null && !UseLegacyScreenOverlay)
        {
            // Safety check for result fields
            if (result.handLandmarks != null && result.handLandmarks.Count > 0)
            {
                try
                {
                    _annotationController.DrawLater(result);
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[HandTracking] Exception in _annotationController.DrawLater: {e.Message}");
                }
            }
            // We removed the immediate clear (DrawLater(default)) to improve stability (prevent flickering)

            if (_logCounter % 100 == 0)
            {
                int count = result.handLandmarks != null ? result.handLandmarks.Count : 0;
                Debug.Log($"[HandTracking] Hand detected: {count}, Timestamp: {timestamp}");
                _logCounter = 0;
            }
            _logCounter++;
        }
    }

    private void OnGestureOutput(GestureRecognizerResult result, Image image, long timestamp)
    {
        lock (_resultLock)
        {
            var clonedResult = default(GestureRecognizerResult);
            result.CloneTo(ref clonedResult);
            _latestGestureResult = clonedResult;
            _hasGestureResult = true;
        }
    }

    private void OnGUI()
    {
        if (_hasGuiResult && _guiResult.handLandmarks != null)
        {
            for (int i = 0; i < _guiResult.handLandmarks.Count; i++)
            {
                string handLabel = "Unknown";
                GUIStyle style = GUI.skin.label;

                if (_guiResult.handedness != null && i < _guiResult.handedness.Count)
                {
                    var hand = _guiResult.handedness[i];
                    if (hand.categories != null && hand.categories.Count > 0)
                    {
                        handLabel = hand.categories[0].categoryName;
                        style = (handLabel == "Left") ? _leftHandStyle : _rightHandStyle;
                    }
                }

                // Print Hand Info
                GUILayout.BeginArea(new UnityEngine.Rect(10, 10 + (i * 150), 520, 150));
                GUILayout.Label($"Hand #{i}: {handLabel}", style);

                var landmarks = _guiResult.handLandmarks[i].landmarks;
                if (landmarks != null && landmarks.Count > 8)
                {
                    if (_hasGuiGestureResult)
                    {
                        GUILayout.Label($"Canned Gesture: {GetStableGestureDisplay(i)}", style);
                    }
                    GUILayout.Label($"Index Tip (X: {landmarks[8].x:F3}, Y: {landmarks[8].y:F3}, Z: {landmarks[8].z:F3})", style);
                    GUILayout.Label($"Thumb Tip (X: {landmarks[4].x:F3}, Y: {landmarks[4].y:F3}, Z: {landmarks[4].z:F3})", style);
                    GUILayout.Label($"Pinch Distance (px: {_pinchDistancePixels[i]:F1}, norm: {_pinchDistanceNormalized[i]:F3})", style);
                }
                GUILayout.EndArea();

                if (ShowHandSkeletonOverlay && landmarks != null && landmarks.Count > 0)
                {
                    UnityEngine.Color overlayColor = (handLabel == "Left") ? UnityEngine.Color.red : UnityEngine.Color.green;
                    DrawHandOverlay(landmarks, overlayColor);
                }

                if (i < _handActive.Length && _handActive[i])
                {
                    UnityEngine.Color dotColor = (handLabel == "Left") ? UnityEngine.Color.red : UnityEngine.Color.green;

                    if (ShowIndexTipOverlay && i < _smoothedIndexTipPositions.Length)
                    {
                        DrawRect(_smoothedIndexTipPositions[i], 15, dotColor);
                    }

                    if (ShowThumbTipOverlay && i < _smoothedThumbTipPositions.Length)
                    {
                        DrawDiamond(_smoothedThumbTipPositions[i], 15, dotColor);
                    }
                }
            }
        }
        else
        {
            GUI.Label(new UnityEngine.Rect(10, 10, 200, 30), "No Hands Detected", _leftHandStyle);
        }
    }

    private void DrawRect(Vector2 position, int size, UnityEngine.Color color)
    {
        var previousColor = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(new UnityEngine.Rect(position.x - size / 2, position.y - size / 2, size, size), _guiTexture);
        GUI.color = previousColor;
    }

    private void DrawDiamond(Vector2 position, int size, UnityEngine.Color color)
    {
        var previousColor = GUI.color;
        var previousMatrix = GUI.matrix;
        GUI.color = color;
        GUIUtility.RotateAroundPivot(45f, position);
        GUI.DrawTexture(new UnityEngine.Rect(position.x - size / 2, position.y - size / 2, size, size), _guiTexture);
        GUI.matrix = previousMatrix;
        GUI.color = previousColor;
    }

    private void DrawHandOverlay(IReadOnlyList<Mediapipe.Tasks.Components.Containers.NormalizedLandmark> landmarks, UnityEngine.Color color)
    {
        for (int i = 0; i < HandConnections.Length; i++)
        {
            var (start, end) = HandConnections[i];
            if (start < landmarks.Count && end < landmarks.Count)
            {
                DrawLine(ToScreenPoint(landmarks[start]), ToScreenPoint(landmarks[end]), color, OverlayLineWidth);
            }
        }

        for (int i = 0; i < landmarks.Count; i++)
        {
            DrawRect(ToScreenPoint(landmarks[i]), Mathf.RoundToInt(OverlayPointSize), color);
        }
    }

    private Vector2 ToScreenPoint(Mediapipe.Tasks.Components.Containers.NormalizedLandmark landmark)
    {
        var x = InvertMirroring ? 1f - landmark.x : landmark.x;
        return new Vector2(x * UnityEngine.Screen.width, landmark.y * UnityEngine.Screen.height);
    }

    private Vector2 GetPalmCenterScreenPoint(IReadOnlyList<Mediapipe.Tasks.Components.Containers.NormalizedLandmark> landmarks)
    {
        int[] palmIndices = { 0, 5, 9, 13, 17 };
        Vector2 sum = Vector2.zero;
        int count = 0;

        for (int i = 0; i < palmIndices.Length; i++)
        {
            int index = palmIndices[i];
            if (index < landmarks.Count)
            {
                sum += ToScreenPoint(landmarks[index]);
                count++;
            }
        }

        return count > 0 ? sum / count : Vector2.zero;
    }

    private Vector2 GetCursorCenterScreenPoint(IReadOnlyList<Mediapipe.Tasks.Components.Containers.NormalizedLandmark> landmarks)
    {
        if (landmarks == null || landmarks.Count <= 8)
        {
            return Vector2.zero;
        }

        return (ToScreenPoint(landmarks[4]) + ToScreenPoint(landmarks[8])) * 0.5f;
    }

    private bool IsDuplicateHandDetection(
        string handedness,
        Vector2 palmCenterPos,
        Vector2 cursorCenterPos,
        List<Vector2> acceptedPalmCenters,
        List<Vector2> acceptedCursorCenters,
        List<string> acceptedHandedness)
    {
        float palmDistanceThresholdSqr = DuplicatePalmDistancePixels * DuplicatePalmDistancePixels;
        float cursorDistanceThresholdSqr = DuplicateCursorDistancePixels * DuplicateCursorDistancePixels;

        for (int i = 0; i < acceptedPalmCenters.Count; i++)
        {
            bool sameHandedness =
                string.Equals(acceptedHandedness[i], handedness, System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(acceptedHandedness[i], "Unknown", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(handedness, "Unknown", System.StringComparison.OrdinalIgnoreCase);

            if (!sameHandedness)
            {
                continue;
            }

            bool palmOverlap =
                palmCenterPos != Vector2.zero &&
                acceptedPalmCenters[i] != Vector2.zero &&
                (acceptedPalmCenters[i] - palmCenterPos).sqrMagnitude <= palmDistanceThresholdSqr;

            bool cursorOverlap =
                cursorCenterPos != Vector2.zero &&
                acceptedCursorCenters[i] != Vector2.zero &&
                (acceptedCursorCenters[i] - cursorCenterPos).sqrMagnitude <= cursorDistanceThresholdSqr;

            if (palmOverlap || cursorOverlap)
            {
                return true;
            }
        }

        return false;
    }

    private bool HasOtherTrackedHand(int excludedHandSlot, bool[] occupiedSlots)
    {
        for (int i = 0; i < MaxHands; i++)
        {
            if (i == excludedHandSlot)
            {
                continue;
            }

            bool handAlreadyVisibleThisFrame = occupiedSlots != null && i < occupiedSlots.Length && occupiedSlots[i];
            bool handAlreadyTracked = i < _handActive.Length && _handActive[i];
            bool handAlreadyPinching = i < IsPinching.Length && IsPinching[i];

            if (handAlreadyVisibleThisFrame || handAlreadyTracked || handAlreadyPinching)
            {
                return true;
            }
        }

        return false;
    }

    private void ReleaseSinglePinchLockIfNeeded(int handSlot)
    {
        if (_singlePinchLockActive && _singlePinchLockedHand == handSlot)
        {
            ClearSinglePinchLock();
        }
    }

    private void ClearSinglePinchLock()
    {
        _singlePinchLockActive = false;
        _singlePinchLockedHand = -1;
    }

    private void UpdateKinematics(int handIndex, Vector2 indexPosition, Vector2 thumbPosition, Vector2 palmCenterPosition, float deltaTime)
    {
        var nextIndexVelocity = (indexPosition - _previousIndexTipPositions[handIndex]) / deltaTime;
        var nextThumbVelocity = (thumbPosition - _previousThumbTipPositions[handIndex]) / deltaTime;
        var nextPalmVelocity = (palmCenterPosition - _previousPalmCenterPositions[handIndex]) / deltaTime;

        _indexTipAcceleration[handIndex] = (nextIndexVelocity - _indexTipVelocity[handIndex]) / deltaTime;
        _thumbTipAcceleration[handIndex] = (nextThumbVelocity - _thumbTipVelocity[handIndex]) / deltaTime;
        _palmCenterAcceleration[handIndex] = (nextPalmVelocity - _palmCenterVelocity[handIndex]) / deltaTime;

        _indexTipVelocity[handIndex] = nextIndexVelocity;
        _thumbTipVelocity[handIndex] = nextThumbVelocity;
        _palmCenterVelocity[handIndex] = nextPalmVelocity;

        _previousIndexTipPositions[handIndex] = indexPosition;
        _previousThumbTipPositions[handIndex] = thumbPosition;
        _previousPalmCenterPositions[handIndex] = palmCenterPosition;
    }

    private bool IsOpenPalmLike(IReadOnlyList<Mediapipe.Tasks.Components.Containers.NormalizedLandmark> landmarks)
    {
        if (landmarks == null || landmarks.Count <= 20)
        {
            return false;
        }

        Vector2 palmCenter = GetPalmCenterNormalized(landmarks);
        if (palmCenter == Vector2.zero)
        {
            return false;
        }

        int extendedCount = 0;
        if (IsFingerExtended(landmarks, palmCenter, 5, 6, 7, 8)) extendedCount++;
        if (IsFingerExtended(landmarks, palmCenter, 9, 10, 11, 12)) extendedCount++;
        if (IsFingerExtended(landmarks, palmCenter, 13, 14, 15, 16)) extendedCount++;
        if (IsFingerExtended(landmarks, palmCenter, 17, 18, 19, 20)) extendedCount++;

        bool thumbExtended = IsThumbExtended(landmarks, palmCenter);
        float pinchDistance = Vector2.Distance(ToNormalizedPoint(landmarks[4]), ToNormalizedPoint(landmarks[8]));
        float fingertipSpread =
            Vector2.Distance(ToNormalizedPoint(landmarks[8]), ToNormalizedPoint(landmarks[12])) +
            Vector2.Distance(ToNormalizedPoint(landmarks[12]), ToNormalizedPoint(landmarks[16])) +
            Vector2.Distance(ToNormalizedPoint(landmarks[16]), ToNormalizedPoint(landmarks[20]));

        return extendedCount >= 3 && thumbExtended && pinchDistance > PinchDetectionThreshold * 1.6f && fingertipSpread > 0.18f;
    }

    private bool IsFingerExtended(IReadOnlyList<Mediapipe.Tasks.Components.Containers.NormalizedLandmark> landmarks, Vector2 palmCenter, int mcpIndex, int pipIndex, int dipIndex, int tipIndex)
    {
        float mcpDistance = Vector2.Distance(palmCenter, ToNormalizedPoint(landmarks[mcpIndex]));
        float pipDistance = Vector2.Distance(palmCenter, ToNormalizedPoint(landmarks[pipIndex]));
        float dipDistance = Vector2.Distance(palmCenter, ToNormalizedPoint(landmarks[dipIndex]));
        float tipDistance = Vector2.Distance(palmCenter, ToNormalizedPoint(landmarks[tipIndex]));

        return tipDistance > dipDistance * 1.02f &&
               dipDistance > pipDistance * 0.98f &&
               pipDistance > mcpDistance * 0.98f &&
               tipDistance - mcpDistance > 0.08f;
    }

    private bool IsThumbExtended(IReadOnlyList<Mediapipe.Tasks.Components.Containers.NormalizedLandmark> landmarks, Vector2 palmCenter)
    {
        Vector2 thumbTip = ToNormalizedPoint(landmarks[4]);
        Vector2 thumbIp = ToNormalizedPoint(landmarks[3]);
        Vector2 thumbMcp = ToNormalizedPoint(landmarks[2]);
        Vector2 indexMcp = ToNormalizedPoint(landmarks[5]);

        float tipDistance = Vector2.Distance(palmCenter, thumbTip);
        float ipDistance = Vector2.Distance(palmCenter, thumbIp);
        float lateralSpread = Vector2.Distance(thumbTip, indexMcp);
        float baseSpread = Vector2.Distance(thumbMcp, indexMcp);

        return tipDistance > ipDistance * 1.02f && lateralSpread > baseSpread * 1.1f;
    }

    private Vector2 ToNormalizedPoint(Mediapipe.Tasks.Components.Containers.NormalizedLandmark landmark)
    {
        float x = InvertMirroring ? 1f - landmark.x : landmark.x;
        return new Vector2(x, landmark.y);
    }

    private Vector2 GetPalmCenterNormalized(IReadOnlyList<Mediapipe.Tasks.Components.Containers.NormalizedLandmark> landmarks)
    {
        int[] palmIndices = { 0, 5, 9, 13, 17 };
        Vector2 sum = Vector2.zero;
        int count = 0;

        for (int i = 0; i < palmIndices.Length; i++)
        {
            int index = palmIndices[i];
            if (index < landmarks.Count)
            {
                sum += ToNormalizedPoint(landmarks[index]);
                count++;
            }
        }

        return count > 0 ? sum / count : Vector2.zero;
    }

    private void EnsureHandTrackingCapacity(int requiredCapacity)
    {
        if (_smoothedIndexTipPositions.Length >= requiredCapacity)
        {
            return;
        }

        System.Array.Resize(ref _smoothedIndexTipPositions, requiredCapacity);
        System.Array.Resize(ref _smoothedThumbTipPositions, requiredCapacity);
        System.Array.Resize(ref _palmCenterScreenPositions, requiredCapacity);
        System.Array.Resize(ref _openPalmCandidates, requiredCapacity);
        System.Array.Resize(ref _handActive, requiredCapacity);
        System.Array.Resize(ref _previousIndexTipPositions, requiredCapacity);
        System.Array.Resize(ref _previousThumbTipPositions, requiredCapacity);
        System.Array.Resize(ref _previousPalmCenterPositions, requiredCapacity);
        System.Array.Resize(ref _indexTipVelocity, requiredCapacity);
        System.Array.Resize(ref _thumbTipVelocity, requiredCapacity);
        System.Array.Resize(ref _palmCenterVelocity, requiredCapacity);
        System.Array.Resize(ref _indexTipAcceleration, requiredCapacity);
        System.Array.Resize(ref _thumbTipAcceleration, requiredCapacity);
        System.Array.Resize(ref _palmCenterAcceleration, requiredCapacity);
        System.Array.Resize(ref _pinchDistancePixels, requiredCapacity);
        System.Array.Resize(ref _pinchDistanceNormalized, requiredCapacity);
    }

    private void DrawLine(Vector2 start, Vector2 end, UnityEngine.Color color, float width)
    {
        var delta = end - start;
        var angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        var length = delta.magnitude;
        if (length <= 0.01f)
        {
            return;
        }

        var previousColor = GUI.color;
        var previousMatrix = GUI.matrix;
        GUI.color = color;
        GUIUtility.RotateAroundPivot(angle, start);
        GUI.DrawTexture(new UnityEngine.Rect(start.x, start.y - width * 0.5f, length, width), _guiTexture);
        GUI.matrix = previousMatrix;
        GUI.color = previousColor;
    }

    private void SyncAnnotationVisibility()
    {
        if (_annotationController != null)
        {
            _annotationController.gameObject.SetActive(!UseLegacyScreenOverlay);
        }
    }

    private void ApplyCameraOpacity()
    {
        if (_cameraCanvasGroup != null)
        {
            _cameraCanvasGroup.alpha = CameraOpacity;
        }

        if (_cameraScreenGraphic != null)
        {
            UnityEngine.Color color = _cameraScreenGraphic.color;
            color.r = 1f;
            color.g = 1f;
            color.b = 1f;
            color.a = CameraOpacity;
            _cameraScreenGraphic.color = color;
            _cameraScreenGraphic.canvasRenderer.SetAlpha(CameraOpacity);
        }
    }

    private long GetCurrentTimestampMillisec()
    {
        return (long)(Time.realtimeSinceStartup * 1000);
    }

    private string FormatGestureClassification(GestureRecognizerResult result, int handIndex)
    {
        if (result.gestures == null || handIndex >= result.gestures.Count)
        {
            return "None";
        }

        var gestures = result.gestures[handIndex];
        if (gestures.categories == null || gestures.categories.Count == 0)
        {
            return "None";
        }

        var topGesture = gestures.categories[0];
        return $"{topGesture.categoryName} ({topGesture.score:F2})";
    }

    private void UpdateStableGestureStateForSlot(int rawHandIndex, int handSlot)
    {
        EnsureGestureDebugCapacity(handSlot + 1);

        string rawLabel = "None";
        float rawScore = 0f;

        if (_hasGuiGestureResult && TryGetTopGesture(_guiGestureResult, rawHandIndex, out string detectedLabel, out float detectedScore))
        {
            rawLabel = detectedLabel;
            rawScore = detectedScore;
        }

        UpdateStableGestureState(handSlot, rawLabel, rawScore);
    }

    private void UpdateStableGestureState(int handSlot, string rawLabel, float rawScore)
    {
        EnsureGestureDebugCapacity(handSlot + 1);

        if (rawLabel == _lastGestureCandidates[handSlot])
        {
            _gestureCandidateFrames[handSlot]++;
        }
        else
        {
            _lastGestureCandidates[handSlot] = rawLabel;
            _gestureCandidateFrames[handSlot] = 1;
        }

        if (rawLabel == "None")
        {
            _noneGestureFrames[handSlot]++;
            if (_noneGestureFrames[handSlot] >= 8)
            {
                _stableGestureLabels[handSlot] = "None";
                _stableGestureScores[handSlot] = 0f;
            }
        }
        else
        {
            _noneGestureFrames[handSlot] = 0;
            if (_gestureCandidateFrames[handSlot] >= 3)
            {
                _stableGestureLabels[handSlot] = rawLabel;
                _stableGestureScores[handSlot] = rawScore;
            }
        }
    }

    private bool TryGetTopGesture(GestureRecognizerResult result, int rawHandIndex, out string label, out float score)
    {
        label = "None";
        score = 0f;

        if (result.gestures == null || rawHandIndex >= result.gestures.Count)
        {
            return false;
        }

        var gestures = result.gestures[rawHandIndex];
        if (gestures.categories == null || gestures.categories.Count == 0)
        {
            return false;
        }

        label = gestures.categories[0].categoryName;
        score = gestures.categories[0].score;
        return true;
    }

    private string GetStableGestureDisplay(int rawHandIndex)
    {
        if (rawHandIndex < 0 || rawHandIndex >= _resolvedSlotByRawHandIndex.Length)
        {
            return "None";
        }

        int handSlot = _resolvedSlotByRawHandIndex[rawHandIndex];
        if (handSlot < 0 || handSlot >= _stableGestureLabels.Length)
        {
            return "None";
        }

        if (string.IsNullOrEmpty(_stableGestureLabels[handSlot]) || _stableGestureLabels[handSlot] == "None")
        {
            return "None";
        }

        return $"{_stableGestureLabels[handSlot]} ({_stableGestureScores[handSlot]:F2})";
    }

    private void ResetStableGestureState(int handSlot)
    {
        if (handSlot < 0 || handSlot >= _stableGestureLabels.Length)
        {
            return;
        }

        _stableGestureLabels[handSlot] = "None";
        _stableGestureScores[handSlot] = 0f;
        _lastGestureCandidates[handSlot] = "None";
        _gestureCandidateFrames[handSlot] = 0;
        _noneGestureFrames[handSlot] = 0;
    }

    private void EnsureGestureDebugCapacity(int requiredCapacity)
    {
        if (_stableGestureLabels.Length >= requiredCapacity)
        {
            return;
        }

        System.Array.Resize(ref _stableGestureLabels, requiredCapacity);
        System.Array.Resize(ref _stableGestureScores, requiredCapacity);
        System.Array.Resize(ref _lastGestureCandidates, requiredCapacity);
        System.Array.Resize(ref _gestureCandidateFrames, requiredCapacity);
        System.Array.Resize(ref _noneGestureFrames, requiredCapacity);
    }

    private void EnsureRawHandMappingCapacity(int requiredCapacity)
    {
        if (_resolvedSlotByRawHandIndex.Length >= requiredCapacity)
        {
            return;
        }

        int oldLength = _resolvedSlotByRawHandIndex.Length;
        System.Array.Resize(ref _resolvedSlotByRawHandIndex, requiredCapacity);
        for (int i = oldLength; i < _resolvedSlotByRawHandIndex.Length; i++)
        {
            _resolvedSlotByRawHandIndex[i] = -1;
        }
    }

    private void OnDestroy()
    {
        if (_runCoroutine != null) StopCoroutine(_runCoroutine);
        if (_taskApi != null) _taskApi.Close();
        if (_gestureRecognizer != null) _gestureRecognizer.Close();
        if (_textureFramePool != null) _textureFramePool.Dispose();
        if (_gestureTextureFramePool != null) _gestureTextureFramePool.Dispose();
        if (_guiTexture != null) Destroy(_guiTexture);
    }
}
