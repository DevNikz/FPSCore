using TMPro;
using UnityEngine;

/// <summary>
/// One prompt line. Scene mode: put it on your line prefab and assign the labels.
/// - Key Label + Text Label: key and text in separate labels ("[E]" / "Open").
/// - Text Label only: one combined label ("[E]   Open").
/// Code mode: PromptUI adds this itself and assigns a single label.
/// </summary>
public class PromptLineView : MonoBehaviour
{
    [Tooltip("Optional. Shows the key, e.g. [E].")]
    [SerializeField]
    TMP_Text keyLabel;

    [Tooltip("Shows the prompt text (or key + text when Key Label is empty).")]
    [SerializeField]
    TMP_Text textLabel;

    /// <summary>Code mode: hand over the single label.</summary>
    public void Assign(TMP_Text text)
    {
        keyLabel = null;
        textLabel = text;
    }

    public void Set(string key, string text)
    {
        if (textLabel == null)
            return;

        if (keyLabel != null)
        {
            keyLabel.text = "[" + key + "]";
            textLabel.text = text;
        }
        else
            textLabel.text = "[" + key + "]   " + text;
    }
}
