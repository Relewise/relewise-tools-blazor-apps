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
using Relewise.Client.Requests;
using Relewise.Client.Requests.Recommendations;
using Relewise.Client.Requests.Search;
using Relewise.Client.Responses;
using Relewise.Client.Responses.Search;

namespace Relewise.BlazorApps.Tests;

[TestClass]
public class ResponseHeaderTests
{
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
