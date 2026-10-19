using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MudBlazor.Services;
using Newtonsoft.Json;
using Relewise.BlazorApps.Shared;
using Relewise.BlazorApps.TypeEditors;
using Relewise.BlazorApps.XmlSummaries;
using Relewise.Client;
using Relewise.Client.Requests;
using Relewise.Client.Requests.Recommendations;
using Relewise.Client.Requests.Search;
using Relewise.Client.Responses;
using Relewise.Client.Responses.Search;
using Relewise.Client.Search;

namespace Relewise.BlazorApps.Tests;

[TestClass]
public class ResponseHeaderTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task EachNewResponseStartsWithCollapsedDetails(bool search)
    {
        await using var context = new BunitContext();
        context.Services.AddMudServices();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddSingleton<IJSInProcessRuntime>(new UnusedInProcessRuntime());
        context.Services.AddSingleton(DocumentationCacheTests.CreateClient(request => Task.FromResult(DocumentationCacheTests.Response(request))));
        context.Services.AddSingleton<DocumentationCache>();
        int attempts = 0;
        using var http = DocumentationCacheTests.CreateClient(_ =>
        {
            attempts++;
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(search
                    ? """{"Results":[{"ProductId":"fixture-1"},{"ProductId":"fixture-2"}],"Hits":2,"Statistics":{"ServerTimeInMs":0}}"""
                    : """{"Recommendations":[{"ProductId":"fixture-1"},{"ProductId":"fixture-2"}],"Statistics":{"ServerTimeInMs":0}}""",
                    System.Text.Encoding.UTF8, "application/json")
            });
        });
        ClientBase client = search
            ? new Searcher(Guid.NewGuid(), "synthetic-api-key", "https://fixture.invalid")
            : new Recommender(Guid.NewGuid(), "synthetic-api-key", "https://fixture.invalid");
        client.OverriddenHttpClient = http;
        client.UseFastBinarySerializer = false;
        client.UseJsonCompression = false;
        object request = search ? new ProductSearchRequest() : new PopularProductsRequest();
        string before = JsonConvert.SerializeObject(request);
        var parameters = new Dictionary<string, object>
        {
            [search ? "Searcher" : "Recommender"] = client, ["Connect"] = (Func<bool>)(() => true),
            ["Request"] = request, ["RequestType"] = request.GetType(), ["OpenDetails"] = true,
            ["AutoPost"] = true, ["ShowHeader"] = false, ["PostButtonText"] = "Re-run Request"
        };
        var component = context.Render<DynamicComponent>(p => p
            .Add(c => c.Type, search ? typeof(SearchConstructor) : typeof(RecommendationConstructor))
            .Add(c => c.Parameters, parameters));

        void AssertCollapsedResponse(int runs)
        {
            component.WaitForAssertion(() =>
            {
                Assert.AreEqual(runs, attempts);
                Assert.HasCount(0, component.FindAll("[role=alert]"), component.FindAll("[role=alert]").FirstOrDefault()?.TextContent);
                Assert.HasCount(2, component.FindAll(".request-response-panel .result-toggle[aria-expanded='false']"));
                Assert.HasCount(0, component.FindAll(".request-response-panel .result-details"));
                StringAssert.Contains(component.Find(".request-response-panel").TextContent, "fixture-1");
                Assert.HasCount(1, component.FindAll(".request-editor-panel .object-top-level > .object-properties"));
                Assert.AreEqual(before, JsonConvert.SerializeObject(request));
            });
        }

        AssertCollapsedResponse(1);
        await component.Find("button[aria-label='Expand all response fields']").ClickAsync(new());
        component.WaitForAssertion(() => Assert.HasCount(2, component.FindAll(".result-details")));
        parameters["SideBySide"] = true;
        component.Render(p => p.Add(c => c.Parameters, parameters));
        Assert.HasCount(2, component.FindAll(".result-details"));
        await component.FindAll("button").Single(button => button.TextContent.Trim() == "Re-run Request").ClickAsync(new());
        AssertCollapsedResponse(2);
        await component.Find(".result-toggle").ClickAsync(new());
        Assert.HasCount(1, component.FindAll(".result-details"));
        await component.FindAll("button").Single(button => button.TextContent.Trim() == "Re-run Request").ClickAsync(new());
        AssertCollapsedResponse(3);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ResponseButtonsExpandAndCollapseAllEvenAfterManualToggles(bool search)
    {
        await using var context = new BunitContext();
        context.Services.AddMudServices();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddSingleton<IJSInProcessRuntime>(new UnusedInProcessRuntime());
        context.Services.AddSingleton(DocumentationCacheTests.CreateClient(request => Task.FromResult(DocumentationCacheTests.Response(request))));
        context.Services.AddSingleton<DocumentationCache>();
        context.ComponentFactories.AddStub<ObjectEditor<LicensedRequest>>();
        context.ComponentFactories.AddStub<ObjectEditor<SearchRequest>>();
        object request = search ? JsonConvert.DeserializeObject<ProductSearchRequest>("{}")! : JsonConvert.DeserializeObject<PopularProductsRequest>("{}")!;
        object response = search
            ? JsonConvert.DeserializeObject<ProductSearchResponse>("""{"Results":[{"ProductId":"fixture-1"},{"ProductId":"fixture-2"}]}""")!
            : JsonConvert.DeserializeObject<ProductRecommendationResponse>("""{"Recommendations":[{"ProductId":"fixture-1"},{"ProductId":"fixture-2"}]}""")!;
        string before = JsonConvert.SerializeObject(response);
        object state = search
            ? new SearchConstructor.EditorState { Request = (SearchRequest)request, RequestInitialized = true, Response = (SearchResponse)response }
            : new RecommendationConstructor.EditorState { Request = (LicensedRequest)request, RequestInitialized = true, Response = (TimedResponse)response };
        var component = context.Render<DynamicComponent>(p => p
            .Add(c => c.Type, search ? typeof(SearchConstructor) : typeof(RecommendationConstructor))
            .Add(c => c.Parameters, new Dictionary<string, object>
            {
                ["State"] = state, ["RequestType"] = request.GetType(), ["Connect"] = (Func<bool>)(() => false),
                ["OpenDetails"] = true, ["ShowHeader"] = false, [search ? "Searcher" : "Recommender"] = null!
            }));
        component.WaitForAssertion(() => Assert.HasCount(2, component.FindAll(".response-header button")));
        Assert.HasCount(0, component.FindAll("input[type=checkbox]"));
        await component.Find("button[aria-label='Expand all response fields']").ClickAsync(new());
        component.WaitForAssertion(() => Assert.HasCount(2, component.FindAll(".result-details")));
        await component.Find(".result-toggle").ClickAsync(new());
        Assert.HasCount(1, component.FindAll(".result-details"));
        await component.Find("button[aria-label='Expand all response fields']").ClickAsync(new());
        component.WaitForAssertion(() => Assert.HasCount(2, component.FindAll(".result-details")));
        await component.Find("button[aria-label='Collapse all response fields']").ClickAsync(new());
        component.WaitForAssertion(() => Assert.HasCount(0, component.FindAll(".result-details")));
        await component.Find(".result-toggle").ClickAsync(new());
        Assert.HasCount(1, component.FindAll(".result-details"));
        await component.Find("button[aria-label='Collapse all response fields']").ClickAsync(new());
        component.WaitForAssertion(() => Assert.HasCount(0, component.FindAll(".result-details")));
        Assert.AreEqual(before, JsonConvert.SerializeObject(response));
    }

    private sealed class UnusedInProcessRuntime : IJSInProcessRuntime
    {
        public TValue Invoke<TValue>(string identifier, params object?[]? args) => throw new InvalidOperationException("Unexpected synchronous browser access.");
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => throw new InvalidOperationException("Unexpected browser access.");
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => throw new InvalidOperationException("Unexpected browser access.");
    }
}
