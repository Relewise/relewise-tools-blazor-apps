using AngleSharp;
using System.Threading;
using System.Web;

namespace KristofferStrube.Blazor.Relewise.XmlSummaries;

public class XMLDocumentationCache
{
    private XmlDocumentation? xmlDocumentation;

    static SemaphoreSlim semaphoreSlim = new SemaphoreSlim(1, 1);
    public async Task<XmlDocumentation> GetAsync()
    {
        if (xmlDocumentation is null)
        {
            await semaphoreSlim.WaitAsync();
            if (xmlDocumentation is null)
            {
                xmlDocumentation = await getAsync();
            }
        }

        return xmlDocumentation;
    }

    private static async Task<XmlDocumentation> getAsync()
    {
        var httpClient = new HttpClient();

        var content = await httpClient.GetStringAsync($"https://kristoffer-strube.dk/API/NugetXMLDocs/Relewise.Client/1.132.0");

        XmlDocumentation result = new();

        IBrowsingContext context = BrowsingContext.New();
        var document = await context.OpenAsync(req => req.Content(content.Replace("/>", "></SEE>")));
        foreach (var seeReference in document.QuerySelectorAll("see"))
        {
            seeReference.OuterHtml = seeReference.GetAttribute("cref")?.Split(".").Last() ?? string.Empty;
        }

        foreach (var member in document.GetElementsByTagName("doc")[0].Children[1].Children)
        {
            foreach (var child in member.Children)
            {
                if (child.TagName is "SUMMARY" && child.InnerHtml.Trim().Length > 0)
                {
                    foreach (var summaryWrapper in child.Children.Where(c => c.TagName == "SUMMARY"))
                    {
                        summaryWrapper.OuterHtml = summaryWrapper.InnerHtml.Trim();
                    }
                    foreach (var seeReference in child.Children.Where(c => c.TagName == "SEE"))
                    {
                        seeReference.OuterHtml = seeReference.GetAttribute("cref")?.Split(".").Last() ?? string.Empty;
                    }

                    result.Summaries.TryAdd(member.GetAttribute("name")!, HttpUtility.HtmlDecode(JoinInOneLine(child.InnerHtml)));
                }
                else if (child.TagName is "PARAM" && child.NextSibling?.TextContent is { Length: > 0 } text)
                {

                    result.Params.TryAdd($"{child.GetAttribute("name")}-{member.GetAttribute("name")!}", HttpUtility.HtmlDecode(JoinInOneLine(text)));
                }
            }
        }

        return result;
    }

    private static string JoinInOneLine(string text) => string.Join(" ", text.Split("\n").Select(line => line.Trim())).Trim();
}
