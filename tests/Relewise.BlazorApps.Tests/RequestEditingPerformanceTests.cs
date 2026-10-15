using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MudBlazor;
using MudBlazor.Services;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using Relewise.BlazorApps.Pages;
using Relewise.BlazorApps.Shared;
using Relewise.BlazorApps.TypeEditors;
using Relewise.BlazorApps.XmlSummaries;
using Relewise.Client.DataTypes.Search.Facets.Queries;
using Relewise.Client.Requests.Search;
using Relewise.Client.Requests;
using Relewise.Client.Requests.Recommendations;
using Relewise.Client.Responses.Search;

namespace Relewise.BlazorApps.Tests;

[TestClass]
public class RequestEditingPerformanceTests
{
    [TestMethod]
    public async Task EditingComplexRequestDoesNotRenderUnrelatedFacetsAndSavesOneSnapshot()
    {
        var previous = (StaticDatasetStorage.DatasetId, StaticDatasetStorage.ApiKey);
        try
        {
            StaticDatasetStorage.DatasetId = "";
            StaticDatasetStorage.ApiKey = "";
            await using var context = new BunitContext();
            var runtime = new CountingRuntime();
            context.Services.AddMudServices();
            context.Services.AddSingleton<IJSInProcessRuntime>(runtime);
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            using var http = DocumentationCacheTests.CreateClient(request => Task.FromResult(DocumentationCacheTests.Response(request)));
            context.Services.AddSingleton(http);
            context.Services.AddSingleton<DocumentationCache>();
            context.ComponentFactories.Add(type => type == typeof(SearchConstructor), _ => (CountedSearchConstructor)Activator.CreateInstance(typeof(CountedSearchConstructor))!);
            context.ComponentFactories.Add(type => type == typeof(ObjectEditor<Facet>), _ => (CountedFacetEditor)Activator.CreateInstance(typeof(CountedFacetEditor))!);
            context.ComponentFactories.Add(type => type == typeof(ListEditor<Facet>), _ => (CountedFacetList)Activator.CreateInstance(typeof(CountedFacetList))!);
            var popovers = context.Render<MudPopoverProvider>();
            var page = context.Render<RequestExplorer>();
            var json = new JObject
            {
                ["$type"] = "Relewise.Client.Requests.Search.ProductSearchRequest, Relewise.Client",
                ["Term"] = "fixture",
                ["Facets"] = new JObject
                {
                    ["Items"] = new JArray(Enumerable.Range(0, 10).Select(index => new JObject
                    {
                        ["$type"] = "Relewise.Client.DataTypes.Search.Facets.Queries.ProductDataStringValueFacet, Relewise.Client",
                        ["Key"] = $"fixture.key.{index}", ["DataSelectionStrategy"] = "Product", ["Field"] = "Data"
                    }))
                },
                ["Skip"] = 0, ["Take"] = 24
            };
            await page.Find("textarea").InputAsync(new() { Value = json.ToString() });
            await page.FindAll("button").Single(button => button.TextContent.Trim() == "Run request").ClickAsync(new());
            var root = page.FindComponent<ObjectEditor<SearchRequest>>();
            root.FindComponent<ObjectEditor<ProductFacetQuery>>().Find("summary").Click();
            var list = root.FindComponent<ListEditor<Facet>>();
            list.Find(".list-title").Click();
            var facets = list.FindComponents<ObjectEditor<Facet>>();
            Assert.HasCount(10, facets);
            foreach (var facet in facets) facet.Find("summary").Click();
            var state = page.FindComponent<SearchConstructor>().Instance.State;
            var response = JsonConvert.DeserializeObject<ProductSearchResponse>("""{"Results":[{"ProductId":"fixture-product"}],"Hits":1}""")!;
            state.Response = response;
            page.Render();
            int constructorRenders = ((CountedSearchConstructor)page.FindComponent<SearchConstructor>().Instance).Renders;
            int listRenders = ((CountedFacetList)list.Instance).Renders;
            int[] facetRenders = facets.Select(facet => ((CountedFacetEditor)facet.Instance).Renders).ToArray();
            runtime.Writes = 0;

            root.Find(".object-properties > .object-property:first-child input").Input("edited term");

            Assert.AreEqual(constructorRenders, ((CountedSearchConstructor)page.FindComponent<SearchConstructor>().Instance).Renders);
            CollectionAssert.AreEqual(facetRenders, facets.Select(facet => ((CountedFacetEditor)facet.Instance).Renders).ToArray());
            Assert.AreEqual(2, runtime.Writes, "History and lastRequest should each be written once per edit.");
            Assert.AreSame(response, state.Response);
            StringAssert.Contains(page.Find(".request-response-panel").TextContent, "fixture-product");
            var restored = JObject.Parse(await Models.FromGzipAsync(runtime.Storage["lastRequest"]));
            Assert.AreEqual("edited term", restored.Value<string>("Term"));
            Assert.HasCount(10, (JArray)restored["Facets"]!["Items"]!);
            StringAssert.Contains(restored.Value<string>("$type")!, nameof(ProductSearchRequest));

            facets[0].FindComponent<StringEditor>().Find("input").Input("fixture.changed");

            Assert.AreEqual(listRenders, ((CountedFacetList)list.Instance).Renders);
            CollectionAssert.AreEqual(facetRenders, facets.Select(facet => ((CountedFacetEditor)facet.Instance).Renders).ToArray());
            restored = JObject.Parse(await Models.FromGzipAsync(runtime.Storage["lastRequest"]));
            Assert.AreEqual("fixture.changed", restored["Facets"]!["Items"]![0]!.Value<string>("Key"));
            Assert.AreEqual("fixture.key.1", restored["Facets"]!["Items"]![1]!.Value<string>("Key"));

            page.Find(".request-source-section > summary").Click();
            var source = JObject.Parse(page.Find("textarea").GetAttribute("value")!);
            Assert.AreEqual("edited term", source.Value<string>("Term"));
            Assert.AreEqual("fixture.changed", source["Facets"]!["Items"]![0]!.Value<string>("Key"));
            Assert.HasCount(1, RequestEditHistory.Load(runtime));

            await root.Find(".object-header-actions .mud-menu button").ClickAsync(new());
            await popovers.FindAll(".mud-menu-item").Single(item => item.TextContent.Trim() == nameof(ContentSearchRequest)).ClickAsync(new());
            var switched = page.FindComponent<SearchConstructor>().Instance;
            Assert.AreEqual(typeof(ContentSearchRequest), switched.RequestType);
            Assert.IsInstanceOfType<ContentSearchRequest>(switched.State.Request);
            Assert.IsFalse(switched.AutoPost);
            Assert.AreSame(response, switched.State.Response);
        }
        finally
        {
            (StaticDatasetStorage.DatasetId, StaticDatasetStorage.ApiKey) = previous;
        }
    }

    [TestMethod]
    public async Task StringListStillAddsAndRemovesRowsWithoutParentRenders()
    {
        await using var context = new BunitContext();
        context.Services.AddMudServices();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var value = new List<string> { "first" };
        var list = context.Render<CountedStringList>(p => p.Add(c => c.Value, value)
            .Add(c => c.OpenDetails, true).Add(c => c.Setter, _ => { }));
        int renders = list.Instance.Renders;
        list.FindComponents<StringEditor>()[0].Find("input").Input("changed");
        Assert.AreEqual(renders, list.Instance.Renders);
        Assert.AreEqual("changed", value[0]);
        list.FindComponents<StringEditor>()[1].Find("input").Input("second");
        Assert.HasCount(2, value);
        Assert.HasCount(3, list.FindComponents<StringEditor>());
        list.Find("button[aria-label='Remove item 1']").Click();
        Assert.HasCount(1, value);
        Assert.AreEqual("second", value[0]);
        Assert.HasCount(2, list.FindComponents<StringEditor>());
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task StandaloneConstructorsStillSaveEditedRequests(bool search)
    {
        await using var context = new BunitContext();
        var runtime = new CountingRuntime();
        context.Services.AddMudServices();
        context.Services.AddSingleton<IJSInProcessRuntime>(runtime);
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.ComponentFactories.AddStub<ObjectEditor<SearchRequest>>();
        context.ComponentFactories.AddStub<ObjectEditor<LicensedRequest>>();
        object request = search ? new ProductSearchRequest() : new PopularProductsRequest();
        var component = context.Render<DynamicComponent>(p => p
            .Add(c => c.Type, search ? typeof(SearchConstructor) : typeof(RecommendationConstructor))
            .Add(c => c.Parameters, new Dictionary<string, object>
            {
                ["Request"] = request, ["RequestType"] = request.GetType(), ["OpenDetails"] = true,
                ["Connect"] = (Func<bool>)(() => false), [search ? "Searcher" : "Recommender"] = null!
            }));
        await component.InvokeAsync(() =>
        {
            if (search)
            {
                ((ProductSearchRequest)request).Term = "edited";
                component.FindComponent<Stub<ObjectEditor<SearchRequest>>>().Instance.Parameters.Get(c => c.Setter)(request);
            }
            else
            {
                ((PopularProductsRequest)request).SinceMinutesAgo = 123;
                component.FindComponent<Stub<ObjectEditor<LicensedRequest>>>().Instance.Parameters.Get(c => c.Setter)(request);
            }
        });
        Assert.AreEqual(2, runtime.Writes);
        var saved = JObject.Parse(await Models.FromGzipAsync(runtime.Storage["lastRequest"]));
        if (search) Assert.AreEqual("edited", saved.Value<string>("Term"));
        else Assert.AreEqual(123, saved.Value<int>("SinceMinutesAgo"));
    }

    public class CountedSearchConstructor : SearchConstructor
    {
        public int Renders { get; private set; }
        protected override void OnAfterRender(bool firstRender) => Renders++;
    }

    public class CountedFacetEditor : ObjectEditor<Facet>
    {
        public int Renders { get; private set; }
        protected override void OnAfterRender(bool firstRender) => Renders++;
    }

    public class CountedFacetList : ListEditor<Facet>
    {
        public int Renders { get; private set; }
        protected override void OnAfterRender(bool firstRender) => Renders++;
    }

    public class CountedStringList : ListEditor<string>
    {
        public int Renders { get; private set; }
        protected override void OnAfterRender(bool firstRender) => Renders++;
    }

    private sealed class CountingRuntime : IJSInProcessRuntime
    {
        private readonly RequestEditHistoryTests.MemoryRuntime inner = new();
        public Dictionary<string, string> Storage => inner.Storage;
        public int Writes { get; set; }
        public TValue Invoke<TValue>(string identifier, params object?[]? args)
        {
            if (identifier == "localStorage.setItem") Writes++;
            return inner.Invoke<TValue>(identifier, args);
        }
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => throw new NotSupportedException();
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => throw new NotSupportedException();
    }
}
