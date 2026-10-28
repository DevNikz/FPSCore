using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lives on the Interactor's GameObject. Picks what the player should be prompted with:
/// the highest-priority registered <see cref="IPromptContextProvider"/> (placing, carrying,
/// dragging), else the Interactable under the crosshair when empty-handed.
/// Publishes the merged lines to <see cref="PromptBus"/> only when the source or state changes.
/// </summary>
[FPSAddon("Prompt UI (Sub-Addon)")]
[RequireComponent(typeof(Interactor))]
[DisallowMultipleComponent]
public class PromptPublisher : MonoBehaviour
{
    Interactor _interactor;

    readonly List<PromptEntry> _merged = new();
    IPromptSource _source,
        _extra;
    IReadOnlyList<PromptEntry> _sourceLines,
        _extraLines;
    PromptContext _context;
    bool _hasPublished;

    void Awake() => _interactor = GetComponent<Interactor>();

    void OnDisable()
    {
        // Leave the UI hidden and force a fresh publish on re-enable.
        _hasPublished = false;
        _source = _extra = null;
        _sourceLines = _extraLines = null;
        PromptBus.Publish(null, PromptContext.EmptyHands);
    }

    void Update()
    {
        var state = ResolveState();
        var sourceLines = state.source?.Prompts;
        var extraLines = state.extra?.Prompts;

        if (
            _hasPublished
            && state.context == _context
            && ReferenceEquals(state.source, _source)
            && ReferenceEquals(state.extra, _extra)
            && ReferenceEquals(sourceLines, _sourceLines)
            && ReferenceEquals(extraLines, _extraLines)
        )
            return; // nothing changed: no list work

        _hasPublished = true;
        _context = state.context;
        _source = state.source;
        _extra = state.extra;
        _sourceLines = sourceLines;
        _extraLines = extraLines;

        _merged.Clear();
        AddLines(sourceLines);
        AddLines(extraLines);
        PromptBus.Publish(_merged, _context);
    }

    PromptState ResolveState()
    {
        IPromptContextProvider best = null;
        PromptState bestState = default;
        var providers = PromptBus.Providers;
        for (int i = 0; i < providers.Count; i++)
        {
            var p = providers[i];
            if (best != null && p.Priority <= best.Priority)
                continue;
            if (!p.TryGetState(out var s))
                continue;
            best = p;
            bestState = s;
        }
        if (best != null)
            return bestState;

        return new PromptState { source = _interactor.Target, context = PromptContext.EmptyHands };
    }

    void AddLines(IReadOnlyList<PromptEntry> lines)
    {
        if (lines == null)
            return;
        for (int i = 0; i < lines.Count; i++)
            _merged.Add(lines[i]);
    }
}
