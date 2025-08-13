using System.Reflection;

namespace Relewise.BlazorApps.XmlSummaries;

public class XmlDocumentation
{
    public Dictionary<string, string> Summaries { get; set; } = new();
    public Dictionary<string, string> Params { get; set; } = new();

    public string? GetSummary(Type type) => GetSummary(type, "");
    public string? GetSummary(Type type, string memberName)
    {
        var xmlTypeName = type.Name;
        if (type.DeclaringType is not null)
        {
            xmlTypeName = type.DeclaringType.Name + "." + type.Name;
        }

        return GetSummary(type.Namespace!, string.IsNullOrEmpty(memberName) ? xmlTypeName : xmlTypeName + "." + memberName);
    }

    public string? GetSummary(string namespaceName, string typeName) => GetSummary(namespaceName + "." + typeName);

    public string? GetSummary(string endsWith)
    {
        if (Summaries.FirstOrDefault(kvp => kvp.Key.ToLower().EndsWith(endsWith.ToLower())) is
            { Key: { } matchingKey, Value: { Length: > 0 } matchingSummary })
        {
            return matchingSummary[0..1].ToUpper() + matchingSummary[1..];
        }

        return null;
    }

    public string? GetConstructorSummary(string typeName, ParameterInfo[] parameters)
    {
        if (Summaries.FirstOrDefault(kvp =>
                kvp.Key.Contains(typeName, StringComparison.OrdinalIgnoreCase)
                && kvp.Key.Contains("#ctor", StringComparison.OrdinalIgnoreCase)
                && kvp.Key.Split(',').Length - 1 == CommasInConstructor(parameters)
                && parameters.All(p => kvp.Key.Contains(MinimalMatchableCSharpTypeName(p.ParameterType), StringComparison.OrdinalIgnoreCase))) is
            { Key: { } matchingKey, Value: { } matchingSummary })
        {
            return matchingSummary;
        }

        return null;
    }

    public string? GetConstructorParam(string typeName, ParameterInfo[] parameters, ParameterInfo parameter)
    {
        if (Params.FirstOrDefault(kvp =>
                kvp.Key.Contains(typeName, StringComparison.OrdinalIgnoreCase)
                && kvp.Key.Contains("#ctor", StringComparison.OrdinalIgnoreCase)
                && kvp.Key.Split(',').Length - 1 == CommasInConstructor(parameters)
                && parameters.All(p => kvp.Key.Contains(MinimalMatchableCSharpTypeName(p.ParameterType), StringComparison.OrdinalIgnoreCase))
                && kvp.Key.Split("-")[0] == parameter.Name) is { Key: { } matchingKey, Value: { } matchingParam })
        {
            return matchingParam;
        }

        return null;
    }

    private static string MinimalMatchableCSharpTypeName(Type type)
    {
        return type.Name.Replace("`1", "").Replace("`2", "");
    }

    private int CommasInConstructor(ParameterInfo[] parameters)
    {
        int result = -1;
        foreach (var parameter in parameters)
        {
            if (parameter.ParameterType.IsGenericType && parameter.ParameterType.GetGenericArguments() is
                { Length: >= 2 } array)
            {
                result += array.Length;
            }
            else
            {
                result++;
            }
        }
        return result;
    }
}