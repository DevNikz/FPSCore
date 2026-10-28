using System;
using System.Collections.Generic;
using UnityEngine;

// PromptEntry, PromptContext and PromptAction stay where they are today; move them into this file
// when convenient. Everything below is the decoupling layer between prompt sources and PromptUI.

/// <summary>Anything that can offer HUD prompt lines (Interactable, the furniture placer, ...).</summary>
public interface IPromptSource
{
    IReadOnlyList<PromptEntry> Prompts { get; }
}

/// <summary>What a provider wants shown right now.</summary>
public struct PromptState
{
    public IPromptSource source;

    /// <summary>Optional second source whose lines follow the first (aimed-at while carrying).</summary>
    public IPromptSource extra;
    public PromptContext context;
}

/// <summary>
/// A system that overrides the empty-handed prompt (carrying, dragging, placing).
/// The highest Priority provider that returns true wins. Register on enable, unregister on disable.
/// </summary>
public interface IPromptContextProvider
{
    int Priority { get; }
    bool TryGetState(out PromptState state);
}

/// <summary>
/// Put on a held object to add game-specific bits to the Holding context
/// (e.g. HoldingBox, HoldingScanner).
/// </summary>
public interface IPromptContextFlag
{
    PromptContext Flag { get; }
}

/// <summary>Static channel between prompt publishers and PromptUI.</summary>
public static class PromptBus
{
    public static event Action<IReadOnlyList<PromptEntry>, PromptContext> Changed;

    public static IReadOnlyList<PromptEntry> CurrentLines { get; private set; } =
        Array.Empty<PromptEntry>();
    public static PromptContext CurrentContext { get; private set; } = PromptContext.EmptyHands;

    static readonly List<IPromptContextProvider> _providers = new();
    public static IReadOnlyList<IPromptContextProvider> Providers => _providers;

    public static void Register(IPromptContextProvider provider)
    {
        if (provider != null && !_providers.Contains(provider))
            _providers.Add(provider);
    }

    public static void Unregister(IPromptContextProvider provider) => _providers.Remove(provider);

    /// <summary>Stores the lines as the current state and tells every listener.</summary>
    public static void Publish(IReadOnlyList<PromptEntry> lines, PromptContext context)
    {
        CurrentLines = lines ?? Array.Empty<PromptEntry>();
        CurrentContext = context;
        Changed?.Invoke(CurrentLines, context);
    }

    // Keeps statics clean when Enter Play Mode skips the domain reload.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Changed = null;
        _providers.Clear();
        CurrentLines = Array.Empty<PromptEntry>();
        CurrentContext = PromptContext.EmptyHands;
    }
}
