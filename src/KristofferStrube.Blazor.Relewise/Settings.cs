using KristofferStrube.Blazor.Relewise.TypeEditors;
using Relewise.Client.DataTypes;

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
        new(t => t == typeof(int),
            _ => typeof(IntEditor),
            _ => 0),
        new(t => t == typeof(long),
            _ => typeof(LongEditor),
            _ => 0),
        new(t => t == typeof(string),
            _ => typeof(StringEditor),
            _ => ""),
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
                return t.GetConstructors().FirstOrDefault(c => c.GetParameters().Length is 0) is { } parameterLessConstructor ? parameterLessConstructor.Invoke(null) : null;
            })
    ];

    public static string Name(Type type) => (type.DeclaringType is { } nestedType ? $"{Name(nestedType)}." : "") + type.Name.Replace("`1", "").Replace("`2", "") + (type.GenericTypeArguments is { Length: > 0 } args ? $"<{string.Join(", ", args.Select(t => t.Name))}>" : "");
}
