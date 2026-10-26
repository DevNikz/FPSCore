using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One row of the keybind menu: action label, bind button (shows the key) and a reset button.
/// For Scene mode, put this on your row prefab and assign the references.
/// KeybindMenu builds the same row in code for Code mode.
/// </summary>
public class KeybindRow : MonoBehaviour
{
    [SerializeField]
    private TMP_Text actionLabel;

    [Tooltip("Click to start rebinding. Its child text shows the key.")]
    [SerializeField]
    private Button bindButton;

    [SerializeField]
    private TMP_Text bindText;

    [Tooltip("Optional. Resets this one binding.")]
    [SerializeField]
    private Button resetButton;

    private const string LISTENING_TEXT = "...";
    private bool _isLocked;

    /// <summary>Code mode only: hands the references of a row built in code.</summary>
    public void Assign(TMP_Text label, Button bind, TMP_Text bindLabel, Button reset)
    {
        actionLabel = label;
        bindButton = bind;
        bindText = bindLabel;
        resetButton = reset;
    }

    public void Setup(
        string label,
        string bindDisplay,
        bool isLocked,
        Action onBind,
        Action onReset
    )
    {
        if (actionLabel == null || bindButton == null || bindText == null)
        {
            Debug.LogWarning(
                "KeybindRow: assign Action Label, Bind Button and Bind Text on the row prefab.",
                this
            );
            return;
        }

        _isLocked = isLocked;
        actionLabel.text = label;
        bindText.text = bindDisplay;

        bindButton.onClick.RemoveAllListeners();
        bindButton.onClick.AddListener(() => onBind?.Invoke());
        if (resetButton != null)
        {
            resetButton.onClick.RemoveAllListeners();
            resetButton.onClick.AddListener(() => onReset?.Invoke());
        }
        SetInteractable(true);
    }

    /// <summary>Locked rows stay non-interactable.</summary>
    public void SetInteractable(bool value)
    {
        if (bindButton != null)
            bindButton.interactable = value && !_isLocked;
        if (resetButton != null)
            resetButton.interactable = value && !_isLocked;
    }

    public void ShowListening()
    {
        if (bindText != null)
            bindText.text = LISTENING_TEXT;
    }
}
