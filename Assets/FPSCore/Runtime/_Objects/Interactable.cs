using System;
using System.Collections.Generic;
using UnityEngine;

public class Interactable : MonoBehaviour
{
    [SerializeField]
    private bool interactable = true;

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

    private void OnValidate() => _legacy = null;

    /// <summary> Add your Interact Event from your project's OnInteractEvent. </summary>
    //public event Action<Interactor> OnInteractEvent;

    public bool CanInteract => interactable && isActiveAndEnabled;

    public void SetInteractable(bool value) => interactable = value;

    // Optional component on this object (outlines, registries). None = nothing extra happens.
    private IInteractableHooks _hooks;

    private void Awake() => TryGetComponent(out _hooks);

    private void OnEnable() => _hooks?.OnInteractableEnabled(this);

    private void OnDisable() => _hooks?.OnInteractableDisabled(this);

    /// <summary>
    /// Prompt lines.
    /// - Falls back to PromptText as a single empty-handed Interact line.
    /// - Interactor is a custom player interactor script that interacts with the game objects. (Not incl. in FPS Core)
    /// </summary>
    // public void Interact(Interactor who)
    // {
    //     if (who == null || !CanInteract)
    //         return;
    //     OnInteractEvent?.Invoke(who);
    // }
}
