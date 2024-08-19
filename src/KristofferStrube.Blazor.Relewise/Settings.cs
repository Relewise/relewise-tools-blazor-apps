using KristofferStrube.Blazor.Relewise.TypeEditors;
using Relewise.Client.DataTypes;
using System.Reflection;

namespace KristofferStrube.Blazor.Relewise;

public static class Settings
{
    public static readonly List<EditorHandler> Editors =
    [
        new(t => t.IsEnum,
            t => typeof(EnumEditor<>).MakeGenericType(new Type[] { t }),
            t => Enum.ToObject(t, 0)),
        new(t => t == typeof(double),
            _ => typeof(DoubleEditor),
            _ => 0.0),
        new(t => t == typeof(float),
            _ => typeof(FloatEditor),
            _ => 0.0f),
        new(t => t == typeof(decimal),
            _ => typeof(DecimalEditor),
            _ => 0M),
        new(t => t == typeof(ushort),
            _ => typeof(UshortEditor),
            _ => 0),
        new(t => t == typeof(int),
            _ => typeof(IntEditor),
            _ => 0),
        new(t => t == typeof(long),
            _ => typeof(LongEditor),
            _ => 0),
        new(t => t == typeof(string),
            _ => typeof(StringEditor),
            _ => ""),
        new(t => t == typeof(Guid),
            _ => typeof(GuidEditor),
            _ => null),
        new(t => t == typeof(DateTimeOffset),
            _ => typeof(DateTimeOffsetEditor),
            _ => null),
        new(t => t == typeof(DateTime),
            _ => typeof(DateTimeEditor),
            _ => null),
        new(t => t == typeof(bool),
            _ => typeof(BoolEditor),
            _ => false),
        new(t => t == typeof(byte),
            _ => typeof(ByteEditor),
            _ => false),
        new(t => t.IsArray,
            t => typeof(ArrayEditor<>).MakeGenericType(new Type[] { t.GetElementType()! }),
            t => Array.CreateInstance(t.GetElementType()!, 0)),
        new(t => t.IsGenericType && (t.GetGenericTypeDefinition() == typeof(List<>)),
            t => typeof(ListEditor<>).MakeGenericType(new Type[] { t.GenericTypeArguments[0] }),
            t => null),
        new(t => t.IsGenericType && (t.GetGenericTypeDefinition() == typeof(Dictionary<,>)),
            t => typeof(DictionaryEditor<,>).MakeGenericType(new Type[] { t.GenericTypeArguments[0], t.GenericTypeArguments[1] }),
            t => null),
        new(t => t == typeof(DataValue),
            t => typeof(DataValueEditor),
            t => new DataValue("")),
        new(t => t == typeof(int?),
            t => typeof(IntEditor),
            t => null),
        new(t => Nullable.GetUnderlyingType(t) is {},
            t => Nullable.GetUnderlyingType(t) is {} simpleType ? Editors.First(e => e.CanHandle(simpleType)).EditorType(simpleType) : typeof(ObjectEditor<>).MakeGenericType([t]),
            t => null),
        new(t => t.IsAssignableTo(typeof(object)),
            t => typeof(ObjectEditor<>).MakeGenericType(new Type[] { t }),
            t => {
                if (t.IsAbstract)
                {
                    return null;
                }
                if (t.GetConstructors().FirstOrDefault(c => c.GetParameters().Length is 0) is { } parameterLessConstructor)
                {
                    return parameterLessConstructor.Invoke(null);
                }
                try {
                    var someConstructor = t.GetConstructors().First();
                    var parameterTypes = someConstructor.GetParameters().Select(p => p.ParameterType);
                    var defaultValuesForParameters = parameterTypes.Select(t => t.IsValueType ? Activator.CreateInstance(t) : null).ToArray();
                    return someConstructor.Invoke(defaultValuesForParameters);
                }
                catch {
                    return null;
                }
            })
    ];

    public static bool CanCreateNoneNullInitValue(Type type) =>
        Editors.FirstOrDefault(editor => editor.CanHandle(type)) is { } editor
        && editor.InitValue(type) is not null;

    public static string Name(Type type)
    {
        if (Nullable.GetUnderlyingType(type) is { } simpleType)
        {
            return $"{Name(simpleType)}?";
        }

        var name = type.Name.Replace("`1", "").Replace("`2", "");

        if (type.DeclaringType is { } nestedType)
        {
            name = $"{Name(nestedType)}.{name}";
        }

        if (type.GenericTypeArguments is { Length: > 0 } args)
        {
            name += $"<{string.Join(", ", args.Select(t => Name(t)))}>";
        }

        return name;
    }

    public static string PropertyTypeName(PropertyInfo type)
    {
        if (Nullable.GetUnderlyingType(type.PropertyType) is { } simpleType)
        {
            return $"{Name(simpleType)}?";
        }

        if (new NullabilityInfoContext().Create(type).WriteState is NullabilityState.Nullable)
        {
            return $"{Name(type.PropertyType)}?";
        }

        var name = type.PropertyType.Name.Replace("`1", "").Replace("`2", "").Replace("`3", "");

        if (type.PropertyType.DeclaringType is { } nestedType)
        {
            name = $"{Name(nestedType)}.{name}";
        }

        if (type.PropertyType.GenericTypeArguments is { Length: > 0 } args)
        {
            name += $"<{string.Join(", ", args.Select(t => Name(t)))}>";
        }

        return name;
    }

    public static IEnumerable<PropertyInfo> GetProperties(Type type) => type.GetProperties().Where(p => p.SetMethod is not null && p.GetIndexParameters() is { Length: 0 } && p.Name is not "Custom" and not "DatasetId" and not "APIKeySecret");
}
