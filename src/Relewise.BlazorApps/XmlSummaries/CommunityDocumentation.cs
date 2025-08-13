namespace Relewise.BlazorApps.XmlSummaries;

public class CommunityDocumentation(List<CommunityDocumentationEntry> entries)
{
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
        if (entries.FirstOrDefault(kvp => kvp.Name.ToLower().EndsWith(endsWith.ToLower())) is
            { Name: { } matchingKey, Summary: { Length: > 0 } matchingSummary })
        {
            return matchingSummary[0..1].ToUpper() + matchingSummary[1..];
        }

        return null;
    }
}
