namespace Relewise.BlazorApps.Shared;

public sealed record VersionChangeRow(string TypeName, string Member = "", string Previous = "", string Current = "");
