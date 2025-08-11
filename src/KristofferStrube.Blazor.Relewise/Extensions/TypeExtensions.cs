namespace KristofferStrube.Blazor.Relewise.Extensions;

public static class TypeExtensions
{
    private const string BigLetters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    public static string SpaceSeparatedName(this Type type)
    {
        string cleaned = type.Name;
        foreach (char bigLetter in BigLetters)
        {
            cleaned = cleaned.Replace($"{bigLetter}", $" {bigLetter}");
        }
        return cleaned.Trim();
    }
}
