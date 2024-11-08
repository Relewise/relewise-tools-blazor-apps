using System.Reflection;

namespace KristofferStrube.Blazor.Relewise;

public record PropertyInvalidContext(Type[] AncestorInterfaces, PropertyInfo Property, string Reason);