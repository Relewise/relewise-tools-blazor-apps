using Relewise.Client.DataTypes;
using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Relewise.BlazorApps.Shared;

public static class DataValueExtensions
{
    public static void EnsureDoubleDataValues(this object? value)
    {
        EnsureDoubleDataValuesInternal(value, new HashSet<object>(ReferenceEqualityComparer.Instance));
    }

    private static void EnsureDoubleDataValuesInternal(object? value, HashSet<object> visited)
    {
        if (value is null || value is string)
            return;

        if (value is DataValue dataValue)
        {
            if (dataValue.Type is DataValue.DataValueTypes.Double &&
                dataValue.Value is not null &&
                dataValue.Value.GetType() != typeof(double))
            {
                dataValue.Value = Convert.ToDouble(dataValue.Value);
            }
            else if (dataValue.Type is DataValue.DataValueTypes.DoubleList &&
                     dataValue.Value is IEnumerable enumerable)
            {
                List<double> doubles = new();
                foreach (object? item in enumerable)
                {
                    if (item is null) continue;
                    doubles.Add(Convert.ToDouble(item));
                }

                dataValue.Value = doubles;
            }

            return;
        }

        if (!visited.Add(value))
            return;

        if (value is IEnumerable enumerableValue)
        {
            foreach (object? item in enumerableValue)
                EnsureDoubleDataValuesInternal(item, visited);

            return;
        }

        Type type = value.GetType();

        if (type.IsPrimitive || type.IsEnum)
            return;

        foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanRead || property.GetIndexParameters().Length > 0)
                continue;

            object? propertyValue = property.GetValue(value);
            EnsureDoubleDataValuesInternal(propertyValue, visited);
        }
    }

    private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
    {
        public static ReferenceEqualityComparer Instance { get; } = new();

        // ReSharper disable once MemberHidesStaticFromOuterClass
        public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);

        public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
    }
}