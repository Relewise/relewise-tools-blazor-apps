namespace Relewise.BlazorApps.Shared;

public sealed class EditorValidationState
{
    private readonly Dictionary<object, string> errors = new();

    public bool IsValid => errors.Count == 0;
    public IEnumerable<string> Errors => errors.Values;

    public void SetError(object owner, string? message)
    {
        if (string.IsNullOrEmpty(message))
            errors.Remove(owner);
        else
            errors[owner] = message;
    }
}
