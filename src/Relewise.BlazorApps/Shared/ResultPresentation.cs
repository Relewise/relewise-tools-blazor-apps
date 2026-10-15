using System.Collections.Concurrent;
using System.Reflection;
using Humanizer;
using Relewise.Client.DataTypes;

namespace Relewise.BlazorApps.Shared;

public static class ResultPresentation
{
    private static readonly string[] identifierNames = ["ProductId", "VariantId", "ContentId", "CategoryId", "BrandId", "DisplayAdId", "Id", "AuthenticatedId", "TemporaryId"];
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> identifierProperties = new();

    public static bool IsResultType(Type type) => type.Assembly == typeof(ProductResult).Assembly
        && (type.Name.EndsWith("Result", StringComparison.Ordinal) || type.Name.EndsWith("ResultDetails", StringComparison.Ordinal));

    public static string ItemName(Type type)
    {
        string name = type.Name;
        if (name.EndsWith("ResultDetails", StringComparison.Ordinal)) name = name[..^13];
        else if (name.EndsWith("Result", StringComparison.Ordinal)) name = name[..^6];
        return name == "Content" ? "content item" : name.Humanize(LetterCasing.LowerCase);
    }

    public static string? Identifier(object? value)
    {
        if (value is null || !IsResultType(value.GetType())) return null;
        var properties = identifierProperties.GetOrAdd(value.GetType(), type => identifierNames
            .Select(name => type.GetProperty(name))
            .Where(property => property is not null && property.GetIndexParameters().Length == 0)
            .Cast<PropertyInfo>().ToArray());
        foreach (var property in properties)
        {
            string? id = property.GetValue(value)?.ToString();
            if (!string.IsNullOrWhiteSpace(id)) return id;
        }
        return null;
    }

    public static string Header(object? value, Type declaredType)
    {
        string title = Settings.Name(value?.GetType() ?? declaredType);
        return Identifier(value) is { } id ? $"{title} ({id})" : title;
    }
}
