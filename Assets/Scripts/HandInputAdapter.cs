using UnityEngine;

/// <summary>
/// Layer 2: Hand gesture input adapter.
/// Bridges raw pinch events from HandTrackingManagerV2 into high-level "intent" events.
/// Decouples business logic from the specific hand tracking hardware/SDK.
/// Replace this class to switch to Leap Motion, Touch, or any other input source
/// without changing any showroom-level code.
/// </summary>
public class HandInputAdapter : MonoBehaviour
{
    [Header("Source")]
    public HandTrackingManagerV2 HandTracking;

    // High-level intent events
    public event System.Action<int, Vector2> OnGrabStart;
    public event System.Action<int, Vector2> OnGrabDrag;
    public event System.Action<int, Vector2> OnGrabEnd;

    // Convenience accessors (pass-through from HandTrackingManagerV2)
    public Vector2[] CursorScreenPositions => HandTracking != null ? HandTracking.CursorScreenPositions : _defaultPositions;
    public bool[]    IsPinching            => HandTracking != null ? HandTracking.IsPinching            : _defaultBools;
    public string[]  Handedness            => HandTracking != null ? HandTracking.Handedness            : _defaultStrings;

    private static readonly Vector2[] _defaultPositions = new Vector2[2];
    private static readonly bool[]    _defaultBools     = new bool[2];
    private static readonly string[]  _defaultStrings   = new[] { "Unknown", "Unknown" };

    private void Start()
    {
        if (HandTracking == null)
            HandTracking = FindObjectOfType<HandTrackingManagerV2>();

        if (HandTracking != null)
        {
            HandTracking.OnPinchDown += (i, pos) => OnGrabStart?.Invoke(i, pos);
            HandTracking.OnPinchDrag += (i, pos) => OnGrabDrag?.Invoke(i, pos);
            HandTracking.OnPinchUp   += (i, pos) => OnGrabEnd?.Invoke(i, pos);
        }
        else
        {
            Debug.LogWarning("[HandInputAdapter] No HandTrackingManagerV2 found. Input will not work.");
        }
    }
}
