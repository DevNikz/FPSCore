using UnityEngine;

/// <summary>
/// Replaces GrabberPromptProvider. Lives with the PromptUI addon and listens to
/// <see cref="DragEvents"/>, so it never references Dragger. While something is dragged, the prompt
/// lines come from the dragged object's <see cref="IPromptSource"/> in the Dragging context.
/// Installs itself at startup; nothing to add to the player.
/// </summary>
static class DraggerPromptBridge
{
    sealed class Provider : IPromptContextProvider
    {
        public IPromptSource Source;

        public int Priority => 10;

        public bool TryGetState(out PromptState state)
        {
            state = default;
            state.source = Source;
            state.context = PromptContext.Dragging;
            return true;
        }
    }

    static readonly Provider _provider = new Provider();
    static bool _isRegistered;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install()
    {
        DragEvents.Started -= OnDragStarted;
        DragEvents.Ended -= OnDragEnded;
        DragEvents.Started += OnDragStarted;
        DragEvents.Ended += OnDragEnded;
        _isRegistered = false; // the bus may have been reset with the play session
    }

    static void OnDragStarted(GameObject dragged)
    {
        // Resolved once per drag, not per frame.
        _provider.Source =
            dragged != null && dragged.TryGetComponent(out IPromptSource source) ? source : null;

        if (_isRegistered)
            return;
        PromptBus.Register(_provider);
        _isRegistered = true;
    }

    static void OnDragEnded(GameObject dragged)
    {
        _provider.Source = null;
        if (!_isRegistered)
            return;
        PromptBus.Unregister(_provider);
        _isRegistered = false;
    }
}
