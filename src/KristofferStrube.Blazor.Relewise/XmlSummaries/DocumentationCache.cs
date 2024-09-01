using AngleSharp;
using Relewise.Client;
using System.Net.Http.Json;
using System.Reflection;
using System.Threading;
using System.Web;

namespace KristofferStrube.Blazor.Relewise.XmlSummaries;

public class DocumentationCache
{
    private XmlDocumentation? xmlDocumentation;
    private CommunityDocumentation? communityDocumentation;

    static SemaphoreSlim semaphoreSlim = new SemaphoreSlim(1, 1);

    public async Task<(XmlDocumentation xml, CommunityDocumentation community)> GetAsync()
    {
        if (xmlDocumentation is null || communityDocumentation is null)
        {
            await semaphoreSlim.WaitAsync();
            if (xmlDocumentation is null)
            {
                xmlDocumentation = await getAsync();
            }
            if (communityDocumentation is null)
            {
                communityDocumentation = await GetCommunityDocumentation();
            }
        }

        return (xmlDocumentation, communityDocumentation);
    }

    private static async Task<XmlDocumentation> getAsync()
    {
        var httpClient = new HttpClient();

        var assembly = Assembly.GetAssembly(typeof(ClientBase));

        var content = await httpClient.GetStringAsync($"https://kristoffer-strube.dk/API/NugetXMLDocs/Relewise.Client/{assembly!.GetName().Version!.ToString()[..^2]}");

        XmlDocumentation result = new();

        IBrowsingContext context = BrowsingContext.New();
        var document = await context.OpenAsync(req => req.Content(content.Replace("/>", "></SEE>")));
        foreach (var seeReference in document.QuerySelectorAll("see"))
        {
            if (seeReference.GetAttribute("cref") is { } cref)
            {
                seeReference.OuterHtml = cref.Split(".").Last();
            }
            else if (seeReference.GetAttribute("langword") is { } langword)
            {
                seeReference.OuterHtml = langword;
            }
            else
            {
                seeReference.OuterHtml = "\"could not resolve reference\"";
            }
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

    public async Task<CommunityDocumentation> GetCommunityDocumentation()
    {
        var httpClient = new HttpClient();

        return new CommunityDocumentation(await httpClient.GetFromJsonAsync<List<CommunityDocumentationEntry>>("https://kristoffer-strube.dk/API/communitydocs/list"));
    }

    private static string JoinInOneLine(string text) => string.Join(" ", text.Split("\n").Select(line => line.Trim())).Trim();
}
