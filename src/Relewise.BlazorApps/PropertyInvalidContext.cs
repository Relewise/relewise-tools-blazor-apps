using System.Reflection;

namespace Relewise.BlazorApps;

public record PropertyInvalidContext(Type[] AncestorInterfaces, PropertyInfo Property, string Reason);