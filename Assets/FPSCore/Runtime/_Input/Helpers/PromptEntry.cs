using System;
using UnityEngine;

/// <summary>Which input action a prompt line shows the key for.</summary>
/// This displays what Key / Control Input for that specific action
public enum PromptAction
{
    Interact,

    // Add your Action Prompts Here.
    // HoldObject
    // TakeObject
    // Etc.

    // Shop Keeper Action Prompts
    // Grab,
    // Scan,
    // BoxStore,
    // Rotate, // furniture preview
    // Cancel, // furniture preview
    // Sell, // furniture preview, while moving a placed piece
}

/// <summary>Player state a prompt line is valid in. Add flags here when new states are needed.</summary>
[Flags]
public enum PromptContext
{
    EmptyHands = 1,
    Holding = 2, // carrying anything (E pickup)
    Dragging = 4, // Grabber is dragging an object

    //Add Additional Prompt Context here

    Any = EmptyHands | Holding | Dragging,

    //Shop Keeper Prompt Context
    // HoldingBox = 8, // carrying a CardboardBox (also counts as Holding)
    // HoldingScanner = 16, // carrying a ScannerTool (also counts as Holding)
    // Placing = 32, // a furniture preview is up (not part of Any: only the placer's own lines use it)
    // Any = EmptyHands | Holding | Dragging | HoldingBox | HoldingScanner,
}

/// <summary>One HUD line: "[key of action] text", shown only when the player state matches.</summary>
[Serializable]
public struct PromptEntry
{
    public PromptAction action;
    public string text;

    [Tooltip("States this line is shown in. Nothing selected = Any.")]
    public PromptContext when;
}
