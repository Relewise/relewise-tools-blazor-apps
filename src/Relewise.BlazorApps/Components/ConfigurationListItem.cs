namespace Relewise.BlazorApps.Components;

public sealed record ConfigurationListItem<T>(T Value, string Name, string TypeName, string Group,
    string Description, bool Enabled, double? Priority = null);
