using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MudBlazor.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Relewise.BlazorApps.Pages;
using Relewise.BlazorApps.Shared;
using Relewise.BlazorApps.TypeEditors;
using Relewise.BlazorApps.XmlSummaries;
using Relewise.Client.DataTypes;
using Relewise.Client.Responses.Search;

namespace Relewise.BlazorApps.Tests;

[TestClass]
public class BatchResponseRenderingTests
{
    [TestMethod]
    [DataRow(2)]
    [DataRow(128)]
    public async Task OpeningBatchResponsesRendersHeadersWithoutProductDetails(int productsPerResponse)
    {
        var previous = (StaticDatasetStorage.DatasetId, StaticDatasetStorage.ApiKey);
        try
        {
            StaticDatasetStorage.DatasetId = "";
            StaticDatasetStorage.ApiKey = "";
            await using var context = new BunitContext();
            context.Services.AddMudServices();
            context.Services.AddSingleton<IJSInProcessRuntime>(new RequestEditHistoryTests.MemoryRuntime());
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddSingleton(DocumentationCacheTests.CreateClient(request => Task.FromResult(DocumentationCacheTests.Response(request))));
            context.Services.AddSingleton<DocumentationCache>();
            var page = context.Render<RequestExplorer>();
            await page.Find("textarea").InputAsync(new() { Value = """
                {"$type":"Relewise.Client.Requests.Search.SearchRequestCollection, Relewise.Client",
                 "Requests":[{"$type":"Relewise.Client.Requests.Search.ProductSearchRequest, Relewise.Client","Term":"fixture"}]}
                """ });
            await page.FindAll("button").Single(button => button.TextContent.Trim() == "Run request").ClickAsync(new());
            var constructor = page.FindComponent<SearchConstructor>();
            string requestBefore = JsonConvert.SerializeObject(constructor.Instance.State.Request);
            var responseJson = new JObject
            {
                ["Responses"] = new JArray(Enumerable.Range(0, 2).Select(batch => new JObject
                {
                    ["$type"] = "Relewise.Client.Responses.Search.ProductSearchResponse, Relewise.Client",
                    ["Results"] = new JArray(Enumerable.Range(0, productsPerResponse).Select(index => new JObject
                    {
                        ["ProductId"] = $"fixture-{batch}-{index}",
                        ["Variant"] = new JObject { ["VariantId"] = "fixture-variant", ["Assortments"] = new JArray(Enumerable.Range(0, 15)) }
                    }))
                }))
            };
            var response = responseJson.ToObject<SearchResponseCollection>(JsonSerializer.Create(new() { TypeNameHandling = TypeNameHandling.Auto }))!;
            foreach (var result in response.Responses.Cast<ProductSearchResponse>().SelectMany(item => item.Results))
                result.Data = new() { ["fixture-list"] = new DataValue(Enumerable.Range(0, 64).Select(value => $"fixture-value-{value}").ToList()) };
            string before = JsonConvert.SerializeObject(response);
            constructor.Instance.State.Response = response;
            page.Render();
            var panel = constructor.Find(".request-response-panel");
            var array = constructor.FindComponent<ArrayEditor<SearchResponse>>();
            Assert.HasCount(0, array.FindAll(".array-entries"));
            await array.Find(".array-title").ClickAsync(new());
            Assert.HasCount(productsPerResponse * 2, panel.QuerySelectorAll(".result-toggle[aria-expanded='false']").ToArray());
            Assert.HasCount(0, page.FindComponents<ObjectEditor<ProductResult>>());
            Assert.HasCount(0, panel.QuerySelectorAll("input").ToArray());
            StringAssert.Contains(panel.TextContent, "fixture-0-0");

            await array.Find(".result-toggle").ClickAsync(new());
            Assert.HasCount(1, page.FindComponents<ObjectEditor<ProductResult>>());
            Assert.HasCount(1, panel.QuerySelectorAll(".result-details").ToArray());
            var dictionary = array.FindComponent<DataValueDictionaryEditor>();
            await dictionary.Find(".dictionary-title").ClickAsync(new());
            Assert.AreEqual(DataValue.DataValueTypes.StringList, dictionary.Instance.Value!["fixture-list"].Type);
            Assert.IsFalse(dictionary.Markup.Contains("Failed to initialize"), dictionary.Markup);
            Assert.HasCount(0, dictionary.FindAll(".list-entries"));
            Assert.AreEqual("false", dictionary.FindComponent<ListEditor<string>>().Find(".list-title").GetAttribute("aria-expanded"));
            page.Render();
            Assert.HasCount(1, panel.QuerySelectorAll(".result-details").ToArray());
            Assert.AreEqual(before, JsonConvert.SerializeObject(response));
            Assert.AreEqual(requestBefore, JsonConvert.SerializeObject(constructor.Instance.State.Request));
            Assert.HasCount(1, constructor.Find(".request-editor-panel").QuerySelectorAll(".object-top-level > .object-properties").ToArray());
            if (productsPerResponse == 2)
            {
                await constructor.Find("button[aria-label='Expand all response fields']").ClickAsync(new());
                Assert.HasCount(4, constructor.FindAll(".result-details"));
                await constructor.Find("button[aria-label='Collapse all response fields']").ClickAsync(new());
                Assert.HasCount(0, constructor.FindAll(".result-details"));
                array = constructor.FindComponent<ArrayEditor<SearchResponse>>();
                await array.Find(".array-title").ClickAsync(new());
                Assert.HasCount(4, constructor.FindAll(".result-toggle[aria-expanded='false']"));
            }
        }
        finally
        {
            (StaticDatasetStorage.DatasetId, StaticDatasetStorage.ApiKey) = previous;
        }
    }
}
