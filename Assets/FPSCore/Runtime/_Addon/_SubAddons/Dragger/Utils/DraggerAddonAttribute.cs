using System;

/// <summary>
/// Marks a MonoBehaviour as a FirstPersonController addon.
/// - Place the script in Assets/FPSCore/Runtime/_Addon/.
/// - FirstPersonControllerEditor lists it as an "Add X" button until the component exists.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class DraggerAddonAttribute : Attribute
{
    /// <summary>Button label. Empty/null falls back to the nicified class name.</summary>
    public string DisplayName { get; }

    public DraggerAddonAttribute(string displayName = null)
    {
        DisplayName = displayName;
    }
}
