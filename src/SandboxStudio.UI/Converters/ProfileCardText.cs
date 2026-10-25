namespace SandboxStudio.UI.Converters;

/// <summary>
/// Display text for profile cards, used through x:Bind function bindings (a
/// converter cannot be used from x:Bind inside a Window's DataTemplate).
/// </summary>
public static class ProfileCardText
{
    /// <summary>
    /// Returns the localized label for a Networking tri-state ("Enable",
    /// "Disable", "Default" or unset).
    /// </summary>
    public static string Networking(string? value) => value switch
    {
        "Enable" => Localizer.Get("CardNetworkingEnabled"),
        "Disable" => Localizer.Get("CardNetworkingDisabled"),
        _ => Localizer.Get("CardNetworkingDefault")
    };
}
