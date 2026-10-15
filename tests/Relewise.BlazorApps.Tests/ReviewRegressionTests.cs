using System.Net;
using System.Reflection;
using Bunit;
using Bunit.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MudBlazor;
using MudBlazor.Services;
using Newtonsoft.Json;
using Relewise.BlazorApps.Components;
using Relewise.BlazorApps.Pages;
using Relewise.BlazorApps.Shared;
using Relewise.BlazorApps.TypeEditors;
using Relewise.Client.DataTypes;
using Relewise.Client.Requests.Recommendations;
using Relewise.Client.Requests.Search;
using Relewise.Client;
using Relewise.Client.Requests.Filters;
using Relewise.Client.Requests.Queries;

namespace Relewise.BlazorApps.Tests;

[TestClass]
public class ReviewRegressionTests
{
    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.Services.AddMudServices();
        context.Services.AddSingleton<IJSInProcessRuntime>(new RequestEditHistoryTests.MemoryRuntime());
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        return context;
    }

    [TestMethod]
    public async Task RenamingEarlierStringKeyDoesNotLoseEitherCommittedEntry()
    {
        await using var context = CreateContext();
        var validation = new EditorValidationState();
        var value = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["a"] = "first", ["b"] = "second" };
        var editor = context.Render<StringDictionaryEditor>(p => p.AddCascadingValue(validation)
            .Add(c => c.Value, value).Add(c => c.OpenDetails, true).Add(c => c.Setter, _ => { }));
        editor.FindAll("input[data-testid='dictionary-key']")[0].Input("B");
        Assert.IsFalse(validation.IsValid);
        Assert.AreEqual("{\"a\":\"first\",\"b\":\"second\"}", JsonConvert.SerializeObject(value));
        editor.FindAll("input[data-testid='dictionary-key']")[0].Input("c");
        Assert.IsTrue(validation.IsValid);
        Assert.AreEqual("first", value["c"]);
        Assert.AreEqual("second", value["b"]);
        Assert.AreEqual(2, value.Count);
    }

    [TestMethod]
    public async Task RenamingEarlierDataKeyAndClearingValuePreservesCommittedJsonUntilValid()
    {
        await using var context = CreateContext();
        var validation = new EditorValidationState();
        var value = new Dictionary<string, DataValue> { ["a"] = new("first"), ["b"] = new("second") };
        string committed = JsonConvert.SerializeObject(value);
        var editor = context.Render<DataValueDictionaryEditor>(p => p.AddCascadingValue(validation)
            .Add(c => c.Value, value).Add(c => c.OpenDetails, true).Add(c => c.Setter, _ => { }));
        editor.FindAll("input[data-testid='dictionary-key']")[0].Input("b");
        Assert.IsFalse(validation.IsValid);
        Assert.AreEqual(committed, JsonConvert.SerializeObject(value));
        editor.FindAll("input[data-testid='dictionary-key']")[0].Input("a");
        editor.FindComponents<DataValueEditor>()[0].FindComponent<StringEditor>().Find("input").Input("");
        Assert.IsFalse(validation.IsValid);
        Assert.AreEqual(committed, JsonConvert.SerializeObject(value));
        editor.FindComponents<DataValueEditor>()[0].FindComponent<StringEditor>().Find("input").Input("replacement");
        Assert.IsTrue(validation.IsValid);
        Assert.AreEqual("replacement", value["a"].Value);
        Assert.AreEqual("second", value["b"].Value);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task RootRequestSwitchDoesNotAutoPostAndReconcilesClients(bool startWithSearch)
    {
        var previous = (StaticDatasetStorage.DatasetId, StaticDatasetStorage.ApiKey, StaticDatasetStorage.ServerUrl);
        try
        {
            StaticDatasetStorage.DatasetId = Guid.NewGuid().ToString();
            StaticDatasetStorage.ApiKey = "synthetic-api-key";
            StaticDatasetStorage.ServerUrl = "https://fixture.invalid";
            await using var context = CreateContext();
            context.ComponentFactories.AddStub<SearchConstructor>();
            context.ComponentFactories.AddStub<RecommendationConstructor>();
            var page = context.Render<RequestExplorer>();
            string type = startWithSearch ? "Search.ProductSearchRequest" : "Recommendations.PopularProductsRequest";
            await page.Find("textarea").InputAsync(new() { Value = $$"""{"$type":"Relewise.Client.Requests.{{type}}, Relewise.Client"}""" });
            await page.FindAll("button").Single(button => button.TextContent.Trim() == "Run request").ClickAsync(new());
            object replacement = startWithSearch ? new PopularProductsRequest() : new ProductSearchRequest();
            if (startWithSearch)
            {
                var original = page.FindComponent<Stub<SearchConstructor>>();
                Assert.IsTrue(original.Instance.Parameters.Get(c => c.AutoPost));
                await original.InvokeAsync(() => original.Instance.Parameters.Get(c => c.RequestChanged).InvokeAsync(replacement));
                var switched = page.FindComponent<Stub<RecommendationConstructor>>();
                Assert.IsFalse(switched.Instance.Parameters.Get(c => c.AutoPost));
                Assert.IsNotNull(switched.Instance.Parameters.Get(c => c.Recommender));
                Assert.AreSame(replacement, switched.Instance.Parameters.Get(c => c.State).Request);
            }
            else
            {
                var original = page.FindComponent<Stub<RecommendationConstructor>>();
                Assert.IsTrue(original.Instance.Parameters.Get(c => c.AutoPost));
                await original.InvokeAsync(() => original.Instance.Parameters.Get(c => c.RequestChanged).InvokeAsync(replacement));
                var switched = page.FindComponent<Stub<SearchConstructor>>();
                Assert.IsFalse(switched.Instance.Parameters.Get(c => c.AutoPost));
                Assert.IsNotNull(switched.Instance.Parameters.Get(c => c.Searcher));
                Assert.AreSame(replacement, switched.Instance.Parameters.Get(c => c.State).Request);
            }
            Assert.IsFalse(page.FindAll("button").Any(button => button.TextContent.Trim() == "Connect"));
        }
        finally
        {
            (StaticDatasetStorage.DatasetId, StaticDatasetStorage.ApiKey, StaticDatasetStorage.ServerUrl) = previous;
        }
    }

    [TestMethod]
    public async Task FailedComparisonCanReturnToSelectorsAndCompareAnotherPair()
    {
        await using var context = CreateContext();
        int metadataRequests = 0;
        byte[] assembly = await File.ReadAllBytesAsync(typeof(Relewise.Client.ClientBase).Assembly.Location);
        using var client = DocumentationCacheTests.CreateClient(request =>
        {
            if (request.RequestUri!.AbsolutePath.Contains("/versions/"))
            {
                metadataRequests++;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""
                    [{"version":"3.0.0","published":"2026-03-01T00:00:00Z"},
                     {"version":"2.0.0","published":"2026-02-01T00:00:00Z"},
                     {"version":"1.0.0","published":"2026-01-01T00:00:00Z"}]
                    """) });
            }
            return Task.FromResult(request.RequestUri.AbsolutePath.Contains("1.0.0")
                ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(assembly) });
        });
        context.Services.AddSingleton(new NugetClient(client));
        context.Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>().NavigateTo("/Versions?from=1.0.0&to=2.0.0");
        var page = context.Render<Versions>();
        page.WaitForAssertion(() => Assert.HasCount(1, page.FindAll(".mud-alert")));
        await page.FindAll("button").Single(button => button.TextContent.Trim() == "Select versions").ClickAsync(new());
        Assert.HasCount(2, page.FindComponents<MudAutocomplete<string>>());
        Assert.HasCount(0, page.FindAll(".mud-alert"));
        await page.InvokeAsync(() => { page.Instance.LeftVersion = "2.0.0"; page.Instance.RightVersion = "3.0.0"; });
        await page.FindAll("button").Single(button => button.TextContent.Trim() == "Compare selected versions").ClickAsync(new());
        page.WaitForAssertion(() => Assert.HasCount(5, page.FindComponents<VersionChangesPanel>()));
        Assert.AreEqual(1, metadataRequests);
    }

    [TestMethod]
    public async Task InvalidDictionaryDraftBlocksEveryDatasetSaveBeforeConnectingOrSending()
    {
        var previous = (StaticDatasetStorage.DatasetId, StaticDatasetStorage.ApiKey);
        try
        {
            StaticDatasetStorage.DatasetId = "";
            StaticDatasetStorage.ApiKey = "";
            await using var context = CreateContext();
            const string error = "Fixture dictionary draft is incomplete.";
            var productPage = context.Render<Products>();
            var product = new Products.ProductAndVariantEditContext(new Product("fixture"), false);
            product.Validation.SetError(this, error);
            await productPage.InvokeAsync(() => (Task)Method(typeof(Products), "Update").Invoke(productPage.Instance, [product])!);
            Assert.AreEqual(error, context.Services.GetRequiredService<ISnackbar>().ShownSnackbars.Last().Message);
            foreach (var (pageType, methods) in new[]
            {
                (typeof(Contents), new[] { "Update" }),
                (typeof(MerchandisingRules), new[] { "Update", "Copy", "Create" }),
                (typeof(Triggers), new[] { "Update", "Create" })
            })
            {
                var page = context.Render<Microsoft.AspNetCore.Components.DynamicComponent>(p => p.Add(c => c.Type, pageType));
                var instance = page.Instance.Instance!;
                var validation = (EditorValidationState)pageType.GetField("editorValidation", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(instance)!;
                validation.SetError(this, error);
                foreach (string method in methods)
                {
                    await page.InvokeAsync(() => (Task)Method(pageType, method).Invoke(instance, null)!);
                    Assert.AreEqual(error, context.Services.GetRequiredService<ISnackbar>().ShownSnackbars.Last().Message, $"{pageType.Name}.{method}");
                }
            }
        }
        finally
        {
            (StaticDatasetStorage.DatasetId, StaticDatasetStorage.ApiKey) = previous;
        }
    }

    private static MethodInfo Method(Type type, string name) => type.GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!;

    [TestMethod]
    public async Task SwitchingSearchTypeKeepsLastResponseAndConsumesInitialAutoPost()
    {
        var previous = (StaticDatasetStorage.DatasetId, StaticDatasetStorage.ApiKey, StaticDatasetStorage.ServerUrl);
        try
        {
            StaticDatasetStorage.DatasetId = Guid.NewGuid().ToString();
            StaticDatasetStorage.ApiKey = "fixture";
            StaticDatasetStorage.ServerUrl = "https://fixture.invalid";
            await using var context = CreateContext();
            context.ComponentFactories.AddStub<SearchConstructor>();
            var page = context.Render<RequestExplorer>();
            await page.Find("textarea").InputAsync(new() { Value = """{"$type":"Relewise.Client.Requests.Search.ProductSearchRequest, Relewise.Client"}""" });
            await page.FindAll("button").Single(button => button.TextContent.Trim() == "Run request").ClickAsync(new());
            var editor = page.FindComponent<Stub<SearchConstructor>>();
            var state = editor.Instance.Parameters.Get(c => c.State);
            var response = JsonConvert.DeserializeObject<Relewise.Client.Responses.Search.ProductSearchResponse>("""{"Results":[],"Hits":0}""")!;
            await editor.InvokeAsync(() => editor.Instance.Parameters.Get(c => c.PostingChanged).InvokeAsync(true));
            Assert.IsFalse(page.FindComponent<Stub<SearchConstructor>>().Instance.Parameters.Get(c => c.AutoPost));
            await editor.InvokeAsync(() => editor.Instance.Parameters.Get(c => c.PostingChanged).InvokeAsync(false));
            state.Response = response;
            var replacement = new ContentSearchRequest();
            await editor.InvokeAsync(() => editor.Instance.Parameters.Get(c => c.RequestChanged).InvokeAsync(replacement));
            var switched = page.FindComponent<Stub<SearchConstructor>>();
            Assert.IsFalse(switched.Instance.Parameters.Get(c => c.AutoPost));
            Assert.AreSame(replacement, switched.Instance.Parameters.Get(c => c.State).Request);
            Assert.AreSame(response, switched.Instance.Parameters.Get(c => c.State).Response);
        }
        finally
        {
            (StaticDatasetStorage.DatasetId, StaticDatasetStorage.ApiKey, StaticDatasetStorage.ServerUrl) = previous;
        }
    }

    [TestMethod]
    public async Task ProductSearchQueriesAllPagesAndUsesServerTotalsAndFirstPage()
    {
        var previous = (StaticDatasetStorage.DatasetId, StaticDatasetStorage.ApiKey);
        try
        {
            StaticDatasetStorage.DatasetId = "";
            StaticDatasetStorage.ApiKey = "";
            await using var context = CreateContext();
            List<ProductQuery> requests = [];
            using var http = DocumentationCacheTests.CreateClient(async message =>
            {
                var query = JsonConvert.DeserializeObject<ProductQuery>(await message.Content!.ReadAsStringAsync(),
                    new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto })!;
                requests.Add(query);
                bool searching = query.Filters.Items.OfType<OrFilter>().Any();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(searching
                        ? """{"Products":[{"ProductId":"off-page-target"}],"TotalNumberOfResults":1,"Statistics":{"ServerTimeInMs":0}}"""
                        : """{"Products":[{"ProductId":"initial-page"}],"TotalNumberOfResults":101,"Statistics":{"ServerTimeInMs":0}}""",
                        System.Text.Encoding.UTF8, "application/json")
                };
            });
            var dataAccessor = new DataAccessor(Guid.NewGuid(), "fixture", "https://fixture.invalid")
            {
                OverriddenHttpClient = http, UseFastBinarySerializer = false, UseJsonCompression = false
            };
            var page = context.Render<Products>();
            typeof(Products).GetField("dataAccessor", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(page.Instance, dataAccessor);
            typeof(Products).GetField("tracker", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(page.Instance,
                new Tracker(Guid.NewGuid(), "fixture", "https://fixture.invalid"));
            page.Render();
            page.WaitForAssertion(() => Assert.HasCount(1, requests));
            var grid = page.FindComponent<MudDataGrid<Products.ProductAndVariantEditContext>>();
            page.WaitForAssertion(() => Assert.HasCount(1, grid.Instance.ServerItems,
                string.Join(" ", context.Services.GetRequiredService<ISnackbar>().ShownSnackbars.Select(snackbar => snackbar.Message))));
            await grid.InvokeAsync(() => grid.Instance.NavigateTo(MudBlazor.Page.Next));
            page.WaitForAssertion(() => Assert.AreEqual(50, requests.Last().SkipNumberOfResults));
            var toolbar = page.FindComponent<RListToolbar>();
            await toolbar.InvokeAsync(() => toolbar.Instance.SearchChanged.InvokeAsync("target"));
            page.WaitForAssertion(() => Assert.AreEqual("off-page-target", grid.Instance.ServerItems.Single().Product.Id));
            Assert.AreEqual(0, grid.Instance.CurrentPage);
            Assert.AreEqual(0, requests.Last().SkipNumberOfResults);
            var search = requests.Last().Filters.Items.OfType<OrFilter>().Single();
            Assert.AreEqual("target", search.Filters.OfType<ProductIdFilter>().Single().ProductIds.Single());
            Assert.AreEqual("target", search.Filters.OfType<VariantIdFilter>().Single().VariantIds.Single());
            Assert.HasCount(1, search.Filters.OfType<ProductDisplayNameFilter>());
            Assert.AreEqual(1, grid.Instance.GetFilteredItemsCount());
            await toolbar.InvokeAsync(() => toolbar.Instance.SearchChanged.InvokeAsync(""));
            page.WaitForAssertion(() => Assert.AreEqual("initial-page", grid.Instance.ServerItems.Single().Product.Id));
            Assert.HasCount(0, requests.Last().Filters.Items.OfType<OrFilter>());
            Assert.AreEqual(101, grid.Instance.GetFilteredItemsCount());
        }
        finally
        {
            (StaticDatasetStorage.DatasetId, StaticDatasetStorage.ApiKey) = previous;
        }
    }

    [TestMethod]
    public async Task ContentSaveReceivesDictionaryValidationAndRecoversAfterCorrection()
    {
        var previous = (StaticDatasetStorage.DatasetId, StaticDatasetStorage.ApiKey);
        try
        {
            StaticDatasetStorage.DatasetId = "";
            StaticDatasetStorage.ApiKey = "";
            await using var context = CreateContext();
            using var documentation = DocumentationCacheTests.CreateClient(request => Task.FromResult(DocumentationCacheTests.Response(request)));
            context.Services.AddSingleton(documentation);
            context.Services.AddSingleton<Relewise.BlazorApps.XmlSummaries.DocumentationCache>();
            int writes = 0;
            using var http = DocumentationCacheTests.CreateClient(_ =>
            {
                writes++;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"Statistics":{"ServerTimeInMs":0}}""", System.Text.Encoding.UTF8, "application/json")
                });
            });
            var tracker = new Tracker(Guid.NewGuid(), "fixture", "https://fixture.invalid")
            {
                OverriddenHttpClient = http, UseFastBinarySerializer = false, UseJsonCompression = false
            };
            var content = new Content("fixture") { Data = new() { ["name"] = new("original") } };
            var page = context.Render<Contents>();
            typeof(Contents).GetField("content", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(page.Instance, content);
            typeof(Contents).GetField("tracker", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(page.Instance, tracker);
            page.Render();
            var dictionary = page.FindComponent<DataValueDictionaryEditor>();
            dictionary.Find(".dictionary-title").Click();
            dictionary.FindComponents<DataValueEditor>()[0].FindComponent<StringEditor>().Find("input").Input("");
            await page.InvokeAsync(() => (Task)Method(typeof(Contents), "Update").Invoke(page.Instance, null)!);
            Assert.AreEqual(0, writes);
            Assert.AreEqual("original", content.Data["name"].Value);
            dictionary.FindComponents<DataValueEditor>()[0].FindComponent<StringEditor>().Find("input").Input("corrected");
            await page.InvokeAsync(() => (Task)Method(typeof(Contents), "Update").Invoke(page.Instance, null)!);
            Assert.AreEqual(1, writes);
            Assert.AreEqual("corrected", content.Data["name"].Value);
        }
        finally
        {
            (StaticDatasetStorage.DatasetId, StaticDatasetStorage.ApiKey) = previous;
        }
    }
}
