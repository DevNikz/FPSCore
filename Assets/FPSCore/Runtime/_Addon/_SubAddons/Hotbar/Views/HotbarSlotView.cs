using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One hotbar slot's visuals. Scene mode: put this on your slot prefab and assign the refs.
/// Code mode: HotbarUI builds the slot and calls Assign.
/// </summary>
public class HotbarSlotView : MonoBehaviour
{
    [Tooltip("Border graphic. Tinted by the selected state.")]
    [SerializeField]
    Image border;

    [Tooltip("Item icon.")]
    [SerializeField]
    Image icon;

    [Tooltip("Item name, shown only when the item has no icon.")]
    [SerializeField]
    TMP_Text label;

    [Tooltip("Stack count (\"x3\"), shown only above 1.")]
    [SerializeField]
    TMP_Text count;

    [SerializeField]
    Color normalColor = new Color(1f, 1f, 1f, 0.5f);

    [Tooltip("Border color of the equipped slot.")]
    [SerializeField]
    Color selectedColor = new Color(1f, 0.85f, 0.2f, 1f);

    /// <summary>Code mode: wire up the refs and colors.</summary>
    public void Assign(
        Image border,
        Image icon,
        TMP_Text label,
        TMP_Text count,
        Color normal,
        Color selected
    )
    {
        this.border = border;
        this.icon = icon;
        this.label = label;
        this.count = count;
        SetColors(normal, selected);
    }

    public void SetColors(Color normal, Color selected)
    {
        normalColor = normal;
        selectedColor = selected;
    }

    /// <summary>Show the slot. itemCount 0 = empty.</summary>
    public void Set(bool isSelected, Sprite sprite, string itemName, int itemCount)
    {
        bool hasItem = itemCount > 0;
        bool hasIcon = hasItem && sprite != null;

        if (border != null)
            border.color = isSelected ? selectedColor : normalColor;

        if (icon != null)
        {
            icon.sprite = hasIcon ? sprite : null;
            icon.enabled = hasIcon;
        }

        if (label != null)
        {
            label.text = hasItem && !hasIcon ? itemName : string.Empty;
            label.enabled = hasItem && !hasIcon;
        }

        if (count != null)
        {
            bool isStacked = itemCount > 1;
            count.text = isStacked ? "x" + itemCount : string.Empty;
            count.enabled = isStacked;
        }
    }
}
