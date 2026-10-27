using UnityEngine;

/// <summary>
/// Puts a Carriable into the hotbar. Objects without this (or without a definition) skip it.
/// Carriable does not know about the hotbar; Hotbar reads this component.
/// </summary>
[CarriableAddon("Hotbar Item")]
[RequireComponent(typeof(Carriable))]
[DisallowMultipleComponent]
public class HotbarItem : MonoBehaviour
{
    [Tooltip("Same definition = same stack. Empty = skips the hotbar.")]
    [SerializeField]
    HotbarItemDefinition definition;

    public HotbarItemDefinition Definition => definition;

    void Awake()
    {
        if (definition == null)
            Debug.LogWarning($"{name}: HotbarItem has no definition (skips hotbar).", this);
    }
}
