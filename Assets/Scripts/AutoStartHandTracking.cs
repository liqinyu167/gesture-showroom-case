using UnityEngine;
using UnityEngine.UI;
using Mediapipe.Unity.Sample.MediaPipeVideo;
using Mediapipe;
using System.Collections.Generic;
using System.Collections;

[System.Obsolete("Legacy prototype entry. Use HandTrackingManagerV2 in Assets/Scenes/MediaPipeDemo.unity instead.")]
public class AutoStartHandTracking : MonoBehaviour
{
    [Header("UI Settings")]
    [Range(0f, 1f)]
    public float CameraOpacity = 1f;

    [Header("Smooth Settings")]
    [Tooltip("How fast the dots follow the hand. Lower = Smoother but more latency.")]
    [Range(1f, 50f)]
    public float SmoothSpeed = 10f;

    private MediaPipeVideoSolution _solution;
    private List<NormalizedLandmarkList> _latestLandmarks;
    private List<ClassificationList> _latestHandedness;
    private CanvasGroup _screenCanvasGroup;

    // State for smoothed positions
    private Vector2[] _smoothedPositions = new Vector2[2]; // Pre-allocate for 2 hands usually
    private bool[] _isHandActive = new bool[2];

    IEnumerator Start()
    {
        var screenObj = GameObject.Find("Main Canvas/Container Panel/Body/Screen");
        if (screenObj != null)
        {
            _screenCanvasGroup = screenObj.GetComponent<CanvasGroup>();
            if (_screenCanvasGroup == null)
            {
                _screenCanvasGroup = screenObj.AddComponent<CanvasGroup>();
            }
        }

        _solution = GetComponent<MediaPipeVideoSolution>();
        if (_solution != null)
        {
            _solution.OnHandLandmarksOutput += (landmarks, handedness) =>
            {
                _latestLandmarks = landmarks;
                _latestHandedness = handedness;
            };

            yield return new WaitUntil(() => Mediapipe.Unity.Sample.ImageSourceProvider.ImageSource != null);
            _solution.Play();
        }
        else
        {
            Debug.LogError("MediaPipeVideoSolution component not found on the same GameObject.");
        }
    }

    void Update()
    {
        if (_screenCanvasGroup != null)
        {
            _screenCanvasGroup.alpha = CameraOpacity;
        }

        // Interpolate positions for smoothness
        if (_latestLandmarks != null)
        {
            // Ensure our internal state matches hand count (expand if needed)
            if (_latestLandmarks.Count > _smoothedPositions.Length)
            {
                System.Array.Resize(ref _smoothedPositions, _latestLandmarks.Count);
                System.Array.Resize(ref _isHandActive, _latestLandmarks.Count);
            }

            for (int i = 0; i < _latestLandmarks.Count; i++)
            {
                if (_latestLandmarks[i] != null && _latestLandmarks[i].Landmark.Count > 8)
                {
                    var tip = _latestLandmarks[i].Landmark[8];
                    Vector2 targetPos = new Vector2(tip.X * Screen.width, tip.Y * Screen.height);

                    if (!_isHandActive[i])
                    {
                        // First frame hand appears, snap to it
                        _smoothedPositions[i] = targetPos;
                        _isHandActive[i] = true;
                    }
                    else
                    {
                        // Interpolate towards target
                        _smoothedPositions[i] = Vector2.Lerp(_smoothedPositions[i], targetPos, Time.deltaTime * SmoothSpeed);
                    }
                }
                else
                {
                    _isHandActive[i] = false;
                }
            }

            // Mark remaining indices inactive
            for (int i = _latestLandmarks.Count; i < _isHandActive.Length; i++)
            {
                _isHandActive[i] = false;
            }
        }
        else if (_isHandActive != null)
        {
            for (int i = 0; i < _isHandActive.Length; i++) _isHandActive[i] = false;
        }
    }

    void OnGUI()
    {
        GUIStyle leftStyle = new GUIStyle(GUI.skin.label) { fontSize = 40, fontStyle = FontStyle.Bold };
        leftStyle.normal.textColor = UnityEngine.Color.red;

        GUIStyle rightStyle = new GUIStyle(GUI.skin.label) { fontSize = 40, fontStyle = FontStyle.Bold };
        rightStyle.normal.textColor = UnityEngine.Color.green;

        GUIStyle defaultStyle = new GUIStyle(GUI.skin.label) { fontSize = 40 };
        defaultStyle.normal.textColor = UnityEngine.Color.white;

        GUILayout.BeginArea(new UnityEngine.Rect(20, 20, 800, 800));

        if (_latestLandmarks != null && _latestLandmarks.Count > 0)
        {
            GUILayout.Label("Hands Tracking: " + _latestLandmarks.Count, defaultStyle);

            for (int i = 0; i < _latestLandmarks.Count; i++)
            {
                if (_isHandActive[i])
                {
                    bool isLeft = true;
                    if (_latestHandedness != null && i < _latestHandedness.Count)
                    {
                        var classification = _latestHandedness[i].Classification[0];
                        isLeft = classification.Label == "Left";
                    }

                    GUIStyle currentStyle = isLeft ? leftStyle : rightStyle;
                    string handName = isLeft ? "Left" : "Right";

                    // Show raw data in text for debugging, but we use smoothed for visual dots
                    var tip = _latestLandmarks[i].Landmark[8];
                    GUILayout.Label($"{handName} Hand Index Tip: X={tip.X:F3}, Y={tip.Y:F3}", currentStyle);
                }
            }
        }
        else
        {
            GUILayout.Label("Detecting hands...", defaultStyle);
        }

        GUILayout.EndArea();

        // Draw smoothed dots
        if (_isHandActive != null)
        {
            for (int i = 0; i < _isHandActive.Length; i++)
            {
                if (_isHandActive[i])
                {
                    bool isLeft = true;
                    if (_latestHandedness != null && i < _latestHandedness.Count)
                    {
                        var classification = _latestHandedness[i].Classification[0];
                        isLeft = classification.Label == "Left";
                    }

                    UnityEngine.Color dotColor = isLeft ? UnityEngine.Color.red : UnityEngine.Color.green;
                    Vector2 pos = _smoothedPositions[i];
                    DrawRect(new UnityEngine.Rect(pos.x - 15, pos.y - 15, 30, 30), dotColor);
                }
            }
        }
    }

    private void DrawRect(UnityEngine.Rect rect, UnityEngine.Color color)
    {
        Texture2D texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, color);
        texture.Apply();
        GUI.DrawTexture(rect, texture);
        Destroy(texture);
    }
}
