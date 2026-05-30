/// <summary>
/// Common contract for all objects that can be interacted with via hand tracking.
/// Implement this interface on any exhibitable item (3D objects, video panels, documents, etc.)
/// </summary>
public interface IHandInteractable
{
    string DisplayName { get; }

    void OnHoverEnter();
    void OnHoverExit();
    void OnGrabbed();
    void OnReleased();
    void OnRotate(UnityEngine.Vector2 delta);
    void OnScale(float deltaDistance);
    void OnSingleClick();
    void OnHandDoubleClick();  // Renamed to avoid naming conflict with C# events
}
