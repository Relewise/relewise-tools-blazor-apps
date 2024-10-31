namespace KristofferStrube.Blazor.Relewise;

public record TypeInheritanceLimiter(Type[] AncestorInterfaces, Type BaseType, Type[] TypeInhertianceLimit);