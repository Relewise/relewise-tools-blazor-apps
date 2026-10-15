using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MudBlazor.Services;
using Relewise.BlazorApps.Pages;
using Relewise.BlazorApps.Shared;

namespace Relewise.BlazorApps.Tests;

[TestClass]
public class RequestExplorerConnectionTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ConnectPreparesParsedRequestWithoutExecutingIt(bool search)
    {
        var previous = (StaticDatasetStorage.DatasetId, StaticDatasetStorage.ApiKey, StaticDatasetStorage.ServerUrl);
        try
        {
            StaticDatasetStorage.DatasetId = "";
            StaticDatasetStorage.ApiKey = "";
            StaticDatasetStorage.ServerUrl = "https://fixture.invalid";
            await using var context = new BunitContext();
            context.Services.AddMudServices();
            context.Services.AddSingleton<IJSInProcessRuntime>(new RequestEditHistoryTests.MemoryRuntime());
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.ComponentFactories.AddStub<SearchConstructor>();
            context.ComponentFactories.AddStub<RecommendationConstructor>();
            var page = context.Render<RequestExplorer>();
            string type = search ? "Search.ProductSearchRequest" : "Recommendations.PopularProductsRequest";
            await page.Find("textarea").InputAsync(new() { Value = $$"""{"$type":"Relewise.Client.Requests.{{type}}, Relewise.Client"}""" });
            await page.FindAll("button").Single(button => button.TextContent.Trim() == "Run request").ClickAsync(new());
            await page.FindAll("button").Single(button => button.TextContent.Trim() == "Connect").ClickAsync(new());
            Assert.HasCount(1, page.FindAll("[role='alert']"));

            StaticDatasetStorage.DatasetId = Guid.NewGuid().ToString();
            StaticDatasetStorage.ApiKey = "synthetic-api-key";
            await page.FindAll("button").Single(button => button.TextContent.Trim() == "Connect").ClickAsync(new());
            Assert.HasCount(0, page.FindAll("[role='alert']"));
            if (search)
            {
                var editor = page.FindComponent<Bunit.TestDoubles.Stub<SearchConstructor>>();
                Assert.IsNotNull(editor.Instance.Parameters.Get(c => c.Searcher));
                Assert.IsFalse(editor.Instance.Parameters.Get(c => c.AutoPost));
            }
            else
            {
                var editor = page.FindComponent<Bunit.TestDoubles.Stub<RecommendationConstructor>>();
                Assert.IsNotNull(editor.Instance.Parameters.Get(c => c.Recommender));
                Assert.IsFalse(editor.Instance.Parameters.Get(c => c.AutoPost));
            }
        }
        finally
        {
            (StaticDatasetStorage.DatasetId, StaticDatasetStorage.ApiKey, StaticDatasetStorage.ServerUrl) = previous;
        }
    }
}
