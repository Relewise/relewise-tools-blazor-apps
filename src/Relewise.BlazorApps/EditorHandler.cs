namespace Relewise.BlazorApps;

public record EditorHandler(Func<Type, bool> CanHandle, Func<Type, Type> EditorType, Func<Type, object?> InitValue);