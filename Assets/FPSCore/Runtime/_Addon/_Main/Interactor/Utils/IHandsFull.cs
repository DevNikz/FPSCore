using UnityEngine;

public interface IHandsFull
{
    bool AreHandsFull { get; }

    /// <summary>The object in the hands, null when empty.</summary>
    GameObject HeldObject { get; }
}
