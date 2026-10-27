using UnityEngine;

/// <summary>What an object is in the hotbar. Same definition = same stack.</summary>
[CreateAssetMenu(menuName = "FPS Core/Hotbar Item", fileName = "HotbarItem")]
public class HotbarItemDefinition : ScriptableObject
{
    [Tooltip("Shown when there is no icon. Empty = asset name.")]
    [SerializeField]
    string displayName;

    [SerializeField]
    Sprite icon;

    [Tooltip("Max items of this kind in one slot.")]
    [SerializeField, Min(1)]
    int maxStack = 64;

    public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
    public Sprite Icon => icon;
    public int MaxStack => Mathf.Max(1, maxStack);
}
