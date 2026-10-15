using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MudBlazor;
using MudBlazor.Services;
using Newtonsoft.Json;
using Relewise.BlazorApps.TypeEditors;
using Relewise.BlazorApps.XmlSummaries;
using Relewise.Client.DataTypes;
using Relewise.Client.DataTypes.Search.Facets.Queries;
using Relewise.Client.Requests.Search;

namespace Relewise.BlazorApps.Tests;

[TestClass]
public class ListEditorTests
{
    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.Services.AddMudServices();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddSingleton(DocumentationCacheTests.CreateClient(request => Task.FromResult(DocumentationCacheTests.Response(request))));
        context.Services.AddSingleton<DocumentationCache>();
        return context;
    }

    [TestMethod]
    public async Task StringDraftBecomesOneItemAndKeepsItsEditorWhileTyping()
    {
        await using var context = CreateContext();
        List<string>? result = null;
        var component = context.Render<ListEditor<string>>(p => p
            .Add(c => c.Value, null)
            .Add(c => c.Setter, value => result = (List<string>?)value));
        component.Find(".list-null").Click();
        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.Count);
        var input = component.Find("input");
        var editor = component.FindComponent<StringEditor>().Instance;
        input.Input("a");
        Assert.AreSame(editor, component.FindComponents<StringEditor>()[0].Instance);
        input.Input("alpha");
        CollectionAssert.AreEqual(new[] { "alpha" }, result);
        Assert.AreEqual(2, component.FindAll("input").Count);
        Assert.AreEqual("[\"alpha\"]", JsonConvert.SerializeObject(result));
        component.FindAll("input")[1].Input("alpha");
        CollectionAssert.AreEqual(new[] { "alpha", "alpha" }, result);
    }

    [TestMethod]
    public async Task LoadedNullAndEmptyStringsSurviveDraftAndRemoval()
    {
        await using var context = CreateContext();
        var value = new List<string> { null!, "", "last" };
        var component = context.Render<ListEditor<string>>(p => p.Add(c => c.Value, value)
            .Add(c => c.OpenDetails, true).Add(c => c.Setter, _ => { }));
        Assert.AreEqual(4, component.FindAll("input").Count);
        Assert.AreEqual("[null,\"\",\"last\"]", JsonConvert.SerializeObject(value));
        var lastEditor = component.FindComponents<StringEditor>()[2].Instance;
        component.Find("button[aria-label='Remove item 1']").Click();
        Assert.AreSame(lastEditor, component.FindComponents<StringEditor>()[1].Instance);
        component.FindAll("input")[1].Input("edited");
        CollectionAssert.AreEqual(new[] { "", "edited" }, value);
    }

    [TestMethod]
    public async Task CollapseSurvivesParentRerenderAndActionsDoNotToggleIt()
    {
        await using var context = CreateContext();
        var value = new List<int>();
        var component = context.Render<ListEditor<int>>(p => p.Add(c => c.Value, value).Add(c => c.Setter, _ => { }));
        Assert.AreEqual("true", component.Find(".list-box").GetAttribute("data-expanded"));
        component.Find("button[aria-label='Copy list as JSON']").Click();
        Assert.AreEqual("true", component.Find(".list-box").GetAttribute("data-expanded"));
        component.Find(".list-title").Click();
        component.Render(p => p.Add(c => c.Value, value));
        Assert.AreEqual("false", component.Find(".list-box").GetAttribute("data-expanded"));
    }

    [TestMethod]
    public async Task ReadOnlyListHasNoDraftAndGuardsPublicMutations()
    {
        await using var context = CreateContext();
        var value = new List<string> { null!, "", "one" };
        var component = context.Render<ListEditor<string>>(p => p.Add(c => c.Value, value)
            .Add(c => c.OpenDetails, true).Add(c => c.ReadOnly, true)
            .Add(c => c.Setter, _ => Assert.Fail("Read-only mutation")));
        Assert.AreEqual(3, component.FindAll("input").Count);
        Assert.IsTrue(component.FindAll("input").All(input => input.HasAttribute("disabled")));
        component.Instance.Add();
        component.Instance.Remove(0);
        component.Instance.Create();
        Assert.AreEqual(3, value.Count);
        Assert.AreEqual(0, component.FindAll("button[aria-label^='Remove']").Count);
    }

    [TestMethod]
    public async Task AddingPrimitiveAndNestedItemsUsesExactTypesAndRealDefaults()
    {
        await using var context = CreateContext();
        CheckAdd(context, 0);
        CheckAdd(context, false);
        CheckAdd(context, Guid.Empty);
        CheckAdd(context, default(DateTime));
        CheckAdd(context, default(DateTimeOffset));
        CheckAdd<int?>(context, null);
        CheckAdd(context, default(DayOfWeek));
        var value = new List<List<string>>();
        var component = context.Render<ListEditor<List<string>>>(p => p.Add(c => c.Value, value).Add(c => c.Setter, _ => { }));
        await component.InvokeAsync(component.Instance.Add);
        component.Render();
        Assert.HasCount(1, value);
        Assert.IsNotNull(value[0]);
        Assert.AreEqual("true", component.FindAll(".list-box")[1].GetAttribute("data-expanded"));
    }

    private static void CheckAdd<T>(BunitContext context, T expected)
    {
        var value = new List<T>();
        var component = context.Render<ListEditor<T>>(p => p.Add(c => c.Value, value).Add(c => c.Setter, _ => { }));
        component.InvokeAsync(component.Instance.Add).GetAwaiter().GetResult();
        component.Render();
        Assert.HasCount(1, value);
        Assert.AreEqual(expected, value[0]);
        Assert.HasCount(1, component.FindAll(".list-row"));
        Assert.HasCount(0, component.FindAll("code"));
    }

    [TestMethod]
    public async Task InlineWrappersKeepItemRemovalAndObjectItemsOpenOnAdd()
    {
        await using var context = CreateContext();
        var currencies = new List<Currency> { new("EUR") };
        var currencyEditor = context.Render<ListEditor<Currency>>(p => p.Add(c => c.Value, currencies)
            .Add(c => c.OpenDetails, true).Add(c => c.Setter, _ => { }));
        currencyEditor.Find("button[aria-label='Remove item 1']").Click();
        Assert.HasCount(0, currencies);
        var value = new List<ProductFacetQuery>();
        var component = context.Render<ListEditor<ProductFacetQuery>>(p => p.Add(c => c.Value, value)
            .Add(c => c.Setter, _ => { }));
        await component.InvokeAsync(component.Instance.Add);
        component.Render();
        Assert.HasCount(1, value);
        Assert.IsTrue(component.Find("details").HasAttribute("open"));
        component.Find("button[aria-label='Remove item 1']").Click();
        Assert.HasCount(0, value);
    }

    [TestMethod]
    public async Task FacetMenuUsesRequestRestrictionsAndCreatesSelectedTypeDirectly()
    {
        await using var context = CreateContext();
        var popovers = context.Render<MudPopoverProvider>();
        var value = new List<Facet>();
        var component = context.Render<ListEditor<Facet>>(p => p.Add(c => c.Value, value)
            .Add(c => c.AncestorTypes, [typeof(ProductSearchRequest)])
            .Add(c => c.Setter, _ => { }));
        Assert.HasCount(0, popovers.FindAll(".mud-menu-item"));
        component.Find(".mud-menu button").Click();
        var items = popovers.FindAll(".mud-menu-item");
        Assert.IsTrue(items.Any(item => item.TextContent.Trim() == "BrandFacet"));
        Assert.IsFalse(items.Any(item => item.TextContent.Trim() == "ContentDataStringValueFacet"));
        items.Single(item => item.TextContent.Trim() == "BrandFacet").Click();
        Assert.HasCount(1, value);
        Assert.IsInstanceOfType<BrandFacet>(value[0]);
        Assert.IsTrue(component.Find("details").HasAttribute("open"));
        Assert.HasCount(1, component.FindAll("button[aria-label='Remove item 1']"));
    }

    [TestMethod]
    public async Task CopiedListRetainsPolymorphicMetadataAndRealNulls()
    {
        await using var context = CreateContext();
        var value = new List<Facet> { (Facet)Settings.Editors.Last().InitValue(typeof(BrandFacet))!, null! };
        var component = context.Render<ListEditor<Facet>>(p => p.Add(c => c.Value, value)
            .Add(c => c.Setter, _ => { }));
        component.Find("button[aria-label='Copy list as JSON']").Click();
        var invocation = context.JSInterop.Invocations["navigator.clipboard.writeText"].Single();
        string json = (string)invocation.Arguments[0]!;
        var restored = JsonConvert.DeserializeObject<List<Facet>>(json,
            new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto });
        Assert.IsNotNull(restored);
        Assert.HasCount(2, restored);
        Assert.IsInstanceOfType<BrandFacet>(restored[0]);
        Assert.IsNull(restored[1]);
    }
}
