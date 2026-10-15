using System.Net;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MudBlazor.Services;
using Relewise.BlazorApps.Shared;
using Relewise.BlazorApps.TypeEditors;
using Relewise.Client;
using Relewise.Client.Requests;
using Relewise.Client.Requests.Recommendations;
using Relewise.Client.Requests.Search;
using Relewise.Client.Responses;
using Relewise.Client.Responses.Search;
using Relewise.Client.Search;
using Newtonsoft.Json;
using Relewise.BlazorApps.XmlSummaries;

namespace Relewise.BlazorApps.Tests;

[TestClass]
public class RequestExecutionTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task RerunActionsRenderInsideRequestHeader(bool search)
    {
        await using var context = new BunitContext();
        context.Services.AddMudServices();
        context.Services.AddSingleton<IJSInProcessRuntime>(new UnusedRuntime());
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddSingleton(DocumentationCacheTests.CreateClient(request => Task.FromResult(DocumentationCacheTests.Response(request))));
        context.Services.AddSingleton<DocumentationCache>();
        object request = search ? new ProductSearchRequest() : new PopularProductsRequest();
        var component = context.Render<DynamicComponent>(p => p
            .Add(c => c.Type, search ? typeof(SearchConstructor) : typeof(RecommendationConstructor))
            .Add(c => c.Parameters, new Dictionary<string, object>
            {
                ["Request"] = request, ["RequestType"] = request.GetType(), ["Connect"] = (Func<bool>)(() => false),
                ["OpenDetails"] = true, ["ShowHeader"] = false, ["ActionsInHeader"] = true,
                ["PostButtonText"] = "Re-run Request", [search ? "Searcher" : "Recommender"] = null!
            }));
        component.WaitForAssertion(() => Assert.IsTrue(component.FindAll(".request-editor-panel .object-top-level-header button")
            .Any(button => button.TextContent.Trim() == "Re-run Request")));
        Assert.IsFalse(component.FindAll(".request-editor-panel > button")
            .Any(button => button.TextContent.Trim() == "Re-run Request"));
    }

    [TestMethod]
    public async Task TroubleshootingUsesSharedHeadingWithoutLegacyControls()
    {
        await using var context = new BunitContext();
        context.Services.AddMudServices();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var component = context.Render<Troubleshooter>(p => p
            .Add(c => c.Request, new ProductSearchRequest()).Add(c => c.Stale, true));
        Assert.HasCount(1, component.FindAll(".diagnostics-header h3"));
        Assert.HasCount(1, component.FindAll(".mud-alert"));
        Assert.HasCount(0, component.FindAll("button"));
        Assert.HasCount(0, component.FindAll("h2"));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task TroubleshootingReplacesResponseInSelectedLayout(bool sideBySide)
    {
        await using var context = new BunitContext();
        context.Services.AddMudServices();
        context.Services.AddSingleton<IJSInProcessRuntime>(new UnusedRuntime());
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.ComponentFactories.AddStub<ObjectEditor<SearchRequest>>();
        context.ComponentFactories.AddStub<ObjectEditor<SearchResponse>>("<div data-testid='normal-response'>Response</div>");
        var response = JsonConvert.DeserializeObject<ProductSearchResponse>("{\"Results\":[]}")!;
        var state = new SearchConstructor.EditorState { Request = new ProductSearchRequest(), RequestInitialized = true, Response = response };
        var component = context.Render<SearchConstructor>(p => p
            .Add(c => c.State, state).Add(c => c.RequestType, typeof(ProductSearchRequest))
            .Add(c => c.OpenDetails, true).Add(c => c.ShowHeader, false).Add(c => c.SideBySide, sideBySide)
            .Add(c => c.Connect, () => false)
            .Add(c => c.ResultContent, "<div data-testid='diagnostics'>Diagnostics</div>")
            .Add(c => c.RequestActions, "<button data-testid='diagnostics-again'>Troubleshoot again</button>"));
        Assert.HasCount(1, component.FindAll(".request-response-panel [data-testid=diagnostics]"));
        Assert.HasCount(0, component.FindAll("[data-testid=normal-response]"));
        Assert.HasCount(1, component.FindAll(".request-editor-actions [data-testid=diagnostics-again]"));
        Assert.HasCount(sideBySide ? 1 : 0, component.FindAll(".request-response-layout.side-by-side"));
        component.Render(p => p.Add(c => c.ResultContent, (RenderFragment?)null));
        Assert.HasCount(0, component.FindAll("[data-testid=diagnostics]"));
        Assert.HasCount(1, component.FindAll(".request-response-panel [data-testid=normal-response]"));
        Assert.AreSame(response, state.Response);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task RerunPostsEditedObjectAndReplacesResponse(bool search)
    {
        await using var context = new BunitContext();
        context.Services.AddMudServices();
        context.Services.AddSingleton<IJSInProcessRuntime>(new RequestEditHistoryTests.MemoryRuntime());
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.ComponentFactories.AddStub<ObjectEditor<LicensedRequest>>();
        context.ComponentFactories.AddStub<ObjectEditor<SearchRequest>>();
        context.ComponentFactories.AddStub<ObjectEditor<RecommendationResponse>>();
        context.ComponentFactories.AddStub<ObjectEditor<SearchResponse>>();
        var postedBodies = new List<string>();
        using var http = DocumentationCacheTests.CreateClient(async request =>
        {
            postedBodies.Add(await request.Content!.ReadAsStringAsync());
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(search
                    ? "{\"Results\":[],\"Hits\":0,\"Statistics\":{\"ServerTimeInMs\":0}}"
                    : "{\"Recommendations\":[],\"Statistics\":{\"ServerTimeInMs\":0}}",
                    System.Text.Encoding.UTF8, "application/json")
            };
        });
        ClientBase client = search
            ? new Searcher(Guid.NewGuid(), "synthetic-api-key", "https://fixture.invalid")
            : new Recommender(Guid.NewGuid(), "synthetic-api-key", "https://fixture.invalid");
        client.OverriddenHttpClient = http;
        client.UseFastBinarySerializer = false;
        client.UseJsonCompression = false;
        object request = search ? new ProductSearchRequest() : new PopularProductsRequest();
        var component = context.Render<DynamicComponent>(p => p
            .Add(c => c.Type, search ? typeof(SearchConstructor) : typeof(RecommendationConstructor))
            .Add(c => c.Parameters, new Dictionary<string, object>
            {
                [search ? "Searcher" : "Recommender"] = client, ["Connect"] = (Func<bool>)(() => true),
                ["Request"] = request, ["RequestType"] = request.GetType(), ["OpenDetails"] = true,
                ["AutoPost"] = true, ["ShowHeader"] = false, ["PostButtonText"] = "Re-run Request"
            }));
        component.WaitForAssertion(() =>
        {
            Assert.HasCount(1, postedBodies);
            if (search) Assert.IsNotNull(component.FindComponent<SearchConstructor>().Instance.State.Response);
            else Assert.IsNotNull(component.FindComponent<RecommendationConstructor>().Instance.State.Response);
            Assert.IsFalse(component.FindAll("button").Single(button => button.TextContent.Trim() == "Re-run Request").HasAttribute("disabled"));
        });
        object edited = search ? new ProductSearchRequest { Term = "Changed fixture" }
            : new PopularProductsRequest { SinceMinutesAgo = 123 };
        object? previousResponse = search ? component.FindComponent<SearchConstructor>().Instance.State.Response
            : component.FindComponent<RecommendationConstructor>().Instance.State.Response;
        await component.InvokeAsync(() =>
        {
            if (search)
                component.FindComponent<Bunit.TestDoubles.Stub<ObjectEditor<SearchRequest>>>().Instance.Parameters.Get(c => c.Setter)(edited);
            else
                component.FindComponent<Bunit.TestDoubles.Stub<ObjectEditor<LicensedRequest>>>().Instance.Parameters.Get(c => c.Setter)(edited);
        });
        Assert.HasCount(1, postedBodies);
        object? preservedResponse = search ? component.FindComponent<SearchConstructor>().Instance.State.Response
            : component.FindComponent<RecommendationConstructor>().Instance.State.Response;
        Assert.AreSame(previousResponse, preservedResponse);
        await component.InvokeAsync(() => component.FindAll("button").Single(button => button.TextContent.Trim() == "Re-run Request").ClickAsync(new()));
        component.WaitForAssertion(() => Assert.HasCount(2, postedBodies));
        if (search) StringAssert.Contains(postedBodies[1], "Changed fixture");
        else Assert.AreEqual(123, Newtonsoft.Json.Linq.JObject.Parse(postedBodies[1]).Value<int>("SinceMinutesAgo"));
        Assert.HasCount(0, component.FindAll("[role=alert]"));
        object? newResponse = search ? component.FindComponent<SearchConstructor>().Instance.State.Response
            : component.FindComponent<RecommendationConstructor>().Instance.State.Response;
        Assert.AreNotSame(previousResponse, newResponse);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ResponseHasOneHeadingAndRetainsExpansionActions(bool search)
    {
        await using var context = new BunitContext();
        context.Services.AddMudServices();
        context.Services.AddSingleton<IJSInProcessRuntime>(new UnusedRuntime());
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddSingleton(DocumentationCacheTests.CreateClient(request => Task.FromResult(DocumentationCacheTests.Response(request))));
        context.Services.AddSingleton<DocumentationCache>();
        context.ComponentFactories.AddStub<ObjectEditor<LicensedRequest>>();
        context.ComponentFactories.AddStub<ObjectEditor<SearchRequest>>();
        object request = search ? new ProductSearchRequest() : new PopularProductsRequest();
        object response = search
            ? JsonConvert.DeserializeObject<ProductSearchResponse>("{\"Results\":[]}")!
            : JsonConvert.DeserializeObject<ProductRecommendationResponse>("{\"Recommendations\":[]}")!;
        object state = search
            ? new SearchConstructor.EditorState { Request = (SearchRequest)request, RequestInitialized = true, Response = (SearchResponse)response }
            : new RecommendationConstructor.EditorState { Request = (LicensedRequest)request, RequestInitialized = true, Response = (TimedResponse)response };
        var component = context.Render<DynamicComponent>(p => p
            .Add(c => c.Type, search ? typeof(SearchConstructor) : typeof(RecommendationConstructor))
            .Add(c => c.Parameters, new Dictionary<string, object>
            {
                ["State"] = state, ["RequestType"] = request.GetType(), ["Connect"] = (Func<bool>)(() => false),
                ["OpenDetails"] = true, ["ShowHeader"] = false, ["ShowPostButton"] = false,
                [search ? "Searcher" : "Recommender"] = null!
            }));
        component.WaitForAssertion(() => Assert.HasCount(1, component.FindAll("h3")));
        Assert.HasCount(0, component.FindAll(".response-header h3"));
        Assert.HasCount(2, component.FindAll(".response-header button"));
        Assert.HasCount(1, component.FindAll(".object-top-level-header .response-header"));
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    [DataRow(true, true)]
    public async Task AutoPostRunsOnceAndShowsResponseOrFailure(bool search, bool fail)
    {
        await using var context = new BunitContext();
        context.Services.AddMudServices();
        context.Services.AddSingleton<IJSInProcessRuntime>(new UnusedRuntime());
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.ComponentFactories.AddStub<ObjectEditor<LicensedRequest>>();
        context.ComponentFactories.AddStub<ObjectEditor<SearchRequest>>();
        context.ComponentFactories.AddStub<ObjectEditor<RecommendationResponse>>();
        context.ComponentFactories.AddStub<ObjectEditor<SearchResponse>>();
        int attempts = 0;
        using var http = DocumentationCacheTests.CreateClient(_ =>
        {
            attempts++;
            return Task.FromResult(new HttpResponseMessage(fail ? HttpStatusCode.BadRequest : HttpStatusCode.OK)
            {
                Content = new StringContent(fail ? "{}" : search
                    ? "{\"Results\":[],\"Hits\":0,\"Statistics\":{\"ServerTimeInMs\":0}}"
                    : "{\"Recommendations\":[],\"Statistics\":{\"ServerTimeInMs\":0}}",
                    System.Text.Encoding.UTF8, "application/json")
            });
        });
        ClientBase client = search
            ? new Searcher(Guid.NewGuid(), "synthetic-api-key", "https://fixture.invalid")
            : new Recommender(Guid.NewGuid(), "synthetic-api-key", "https://fixture.invalid");
        client.OverriddenHttpClient = http;
        client.UseFastBinarySerializer = false;
        client.UseJsonCompression = false;
        var statusChanges = new List<bool>();
        var parameters = new Dictionary<string, object>
        {
            [search ? "Searcher" : "Recommender"] = client,
            ["Connect"] = (Func<bool>)(() => true),
            ["Request"] = search ? new ProductSearchRequest() : new PopularProductsRequest(),
            ["RequestType"] = search ? typeof(ProductSearchRequest) : typeof(PopularProductsRequest),
            ["OpenDetails"] = true,
            ["AutoPost"] = true,
            ["ShowHeader"] = false,
            ["ShowPostButton"] = false,
            ["PostingChanged"] = EventCallback.Factory.Create<bool>(this, (bool value) => statusChanges.Add(value))
        };
        var component = context.Render<DynamicComponent>(p => p
            .Add(c => c.Type, search ? typeof(SearchConstructor) : typeof(RecommendationConstructor))
            .Add(c => c.Parameters, parameters));

        component.WaitForAssertion(() =>
        {
            Assert.AreEqual(1, attempts);
            CollectionAssert.AreEqual(new[] { true, false }, statusChanges);
            Assert.HasCount(fail ? 1 : 0, component.FindAll("[role=alert]"), component.FindAll("[role=alert]").FirstOrDefault()?.TextContent);
            if (!fail)
            {
                if (search) Assert.IsNotNull(component.FindComponent<Bunit.TestDoubles.Stub<ObjectEditor<SearchResponse>>>());
                else Assert.IsNotNull(component.FindComponent<Bunit.TestDoubles.Stub<ObjectEditor<RecommendationResponse>>>());
            }
            Assert.IsFalse(component.FindAll("button").Any(button => button.TextContent.Trim() == "Post"));
        });
        parameters["SideBySide"] = true;
        component.Render(p => p.Add(c => c.Parameters, parameters));
        Assert.HasCount(1, component.FindAll(".request-response-layout.side-by-side"));
        Assert.AreEqual(1, attempts);
    }

    private sealed class UnusedRuntime : IJSInProcessRuntime
    {
        public TValue Invoke<TValue>(string identifier, params object?[]? args) => throw new InvalidOperationException(identifier);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => throw new InvalidOperationException(identifier);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => throw new InvalidOperationException(identifier);
    }
}
