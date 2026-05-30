using Cinemachine;
using UnityEngine;

public enum CameraRouteDirection
{
    None = 0,
    Left = 1,
    Right = 2,
    Up = 3,
    Down = 4,
}

public class CameraRouteNode : MonoBehaviour
{
    [Header("Identity")]
    public string Id = "camera-node";
    public string DisplayName = "Camera Node";
    public int Order = 0;
    public bool IsBranchNode = false;

    [Header("Camera")]
    public CinemachineVirtualCamera VirtualCamera;

    [Header("Neighbors")]
    public CameraRouteNode Left;
    public CameraRouteNode Right;
    public CameraRouteNode Up;
    public CameraRouteNode Down;

    public string ResolvedDisplayName
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(DisplayName))
            {
                return DisplayName;
            }

            if (!string.IsNullOrWhiteSpace(Id))
            {
                return Id;
            }

            return gameObject.name;
        }
    }

    public CameraRouteNode GetNeighbor(CameraRouteDirection direction)
    {
        switch (direction)
        {
            case CameraRouteDirection.Left:
                return Left;
            case CameraRouteDirection.Right:
                return Right;
            case CameraRouteDirection.Up:
                return Up;
            case CameraRouteDirection.Down:
                return Down;
            default:
                return null;
        }
    }

    private void Reset()
    {
        AutoAssignDefaults();
    }

    private void OnValidate()
    {
        AutoAssignDefaults();
    }

    private void AutoAssignDefaults()
    {
        if (VirtualCamera == null)
        {
            VirtualCamera = GetComponent<CinemachineVirtualCamera>();
        }

        if (string.IsNullOrWhiteSpace(Id))
        {
            Id = gameObject.name;
        }

        if (string.IsNullOrWhiteSpace(DisplayName))
        {
            DisplayName = gameObject.name;
        }
    }
}
