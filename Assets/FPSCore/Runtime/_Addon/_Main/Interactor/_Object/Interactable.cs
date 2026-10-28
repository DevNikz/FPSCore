using System;
using System.Collections.Generic;
using UnityEngine;

public class Interactable : MonoBehaviour, IPromptSource
{
    [SerializeField]
    private bool interactable = true;

    [Tooltip(
        "Player states this can be used in. Holding = while carrying something "
            + "(e.g. a computer terminal). Nothing selected = Any."
    )]
    [SerializeField]
    private PromptContext interactWhen = PromptContext.Any;

    [Tooltip(
        "Simple prompt: one Interact line shown while empty-handed. Ignored when Prompts below is filled."
    )]
    [SerializeField]
    private string promptText;

    [Tooltip(
        "HUD lines for this object, each with its own action key and the player state it shows in. "
            + "While carrying, the HUD reads the held object's lines (state = Holding)."
    )]
    [SerializeField]
    private PromptEntry[] prompts;
    public string PromptText => promptText;
    private PromptEntry[] _legacy;
    private IHeldInteractFilter[] _heldFilters;

    [SerializeField]
    private bool IsDebugEnabled;

    /// <summary>Prompt lines. Falls back to PromptText as a single empty-handed Interact line.</summary>
    public IReadOnlyList<PromptEntry> Prompts
    {
        get
        {
            if (prompts != null && prompts.Length > 0)
                return prompts;
            if (string.IsNullOrEmpty(promptText))
                return Array.Empty<PromptEntry>();
            return _legacy ??= new[]
            {
                new PromptEntry
                {
                    action = PromptAction.Interact,
                    text = promptText,
                    when = PromptContext.EmptyHands,
                },
            };
        }
    }

    void OnValidate() => _legacy = null;

    public event Action<Interactor> OnInteractEvent;

    /// <summary>Raised when any Interactable is enabled. Registries (e.g. outlines) hook in here.</summary>
    public static event Action<Interactable> Enabled;

    /// <summary>Raised when any Interactable is disabled.</summary>
    public static event Action<Interactable> Disabled;

    public bool CanInteract => interactable && isActiveAndEnabled;

    public void SetInteractable(bool value) => interactable = value;

    /// <summary>Usable in this player state. While holding, every IHeldInteractFilter must accept 'held'.</summary>
    public bool CanInteractIn(PromptContext state, GameObject held)
    {
        if (!CanInteract)
            return false;
        if (interactWhen != 0 && (interactWhen & state) == 0)
            return false;
        if (state != PromptContext.Holding)
            return true;

        for (int i = 0; i < _heldFilters.Length; i++)
            if (!_heldFilters[i].CanInteractWhileHeld(held))
                return false;
        return true;
    }

    private void Awake() => _heldFilters = GetComponents<IHeldInteractFilter>();

    private void OnEnable() => Enabled?.Invoke(this);

    private void OnDisable() => Disabled?.Invoke(this);

    public void Interact(Interactor who)
    {
        if (who == null || !CanInteract)
            return;
        if (IsDebugEnabled)
            Debug.Log($"Object {name} Has Been Interacted", this);
        OnInteractEvent?.Invoke(who);
    }
}
