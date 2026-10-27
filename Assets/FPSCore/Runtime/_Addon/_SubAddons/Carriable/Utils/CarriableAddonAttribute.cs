using System;

/// <summary>
/// Marks a MonoBehaviour as a Carriable addon.
/// - Place the script in Assets/FPSCore/Runtime/_Addon/_SubAddons/Carriable/_Objects.
/// - Carriable lists it as an "Add X" button until the component exists.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class CarriableAddonAttribute : Attribute
{
    /// <summary>Button label. Empty/null falls back to the nicified class name.</summary>
    public string DisplayName { get; }

    public CarriableAddonAttribute(string displayName = null)
    {
        DisplayName = displayName;
    }
}
