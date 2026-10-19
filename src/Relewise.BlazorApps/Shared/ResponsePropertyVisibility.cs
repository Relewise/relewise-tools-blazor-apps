using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;

namespace Relewise.BlazorApps.Shared;

internal static class ResponsePropertyVisibility
{
    // API responses are read-only snapshots. Keep comparisons for their lifetime,
    // including when expansion actions recreate the corresponding editors.
    private static readonly ConditionalWeakTable<object, ConcurrentDictionary<PropertyInfo, bool>> comparisons = new();

    public static bool IsNonDefault(object value, object defaults, PropertyInfo property)
        => comparisons.GetValue(value, _ => new()).GetOrAdd(property, propertyInfo =>
            JsonConvert.SerializeObject(propertyInfo.GetValue(value)) != JsonConvert.SerializeObject(propertyInfo.GetValue(defaults)));
}
