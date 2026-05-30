using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central registry for all interactable items in the showroom.
/// Replaces the static ItemObserver.ActiveObserver pattern.
/// Add this component to the ShowroomManager GameObject.
/// </summary>
public class ShowroomRegistry : MonoBehaviour
{
    public static ShowroomRegistry Instance { get; private set; }

    private readonly List<InteractableItem> _allItems = new List<InteractableItem>();
    private IHandInteractable _focusedItem;

    /// <summary>The item currently in "observation mode".</summary>
    public IHandInteractable FocusedItem => _focusedItem;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Register(InteractableItem item)
    {
        if (item != null && !_allItems.Contains(item))
            _allItems.Add(item);
    }

    public void Unregister(InteractableItem item)
    {
        _allItems.Remove(item);
        if (_focusedItem == item) ClearFocus();
    }

    /// <summary>Set a specific item as the global observation focus.</summary>
    public void SetFocus(IHandInteractable item)
    {
        // Exit previous focused item's observation if different
        if (_focusedItem != null && _focusedItem != item)
        {
            var prevMono = _focusedItem as MonoBehaviour;
            prevMono?.GetComponent<ItemObserver>()?.ExitObservation();
        }
        _focusedItem = item;
    }

    /// <summary>Remove observation focus.</summary>
    public void ClearFocus()
    {
        _focusedItem = null;
    }

    /// <summary>Get first registered item as an interaction fallback.</summary>
    public InteractableItem GetFirstItem()
    {
        return _allItems.Count > 0 ? _allItems[0] : null;
    }

    public IReadOnlyList<InteractableItem> GetAllItems() => _allItems.AsReadOnly();
}
