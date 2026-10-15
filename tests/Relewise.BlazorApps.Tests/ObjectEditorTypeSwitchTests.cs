using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MudBlazor.Services;
using Relewise.BlazorApps.TypeEditors;
using Relewise.BlazorApps.XmlSummaries;
using Relewise.Client.Requests.Filters;
using Relewise.Client.Requests.PopularityMultiplierSelectors;

namespace Relewise.BlazorApps.Tests;

[TestClass]
public class ObjectEditorTypeSwitchTests
{
    [TestMethod]
    public async Task NestedTypeMenuSharesTheActionRowAndSwitchesWithoutASelectField()
    {
        await using var context = CreateContext();
        var popovers = context.Render<MudBlazor.MudPopoverProvider>();
        object? changed = null;
        var component = context.Render<ObjectEditor<Filter>>(p => p
            .Add(c => c.Value, Activator.CreateInstance(typeof(ProductDataFilter)))
            .Add(c => c.OpenDetails, true)
            .Add(c => c.AncestorTypes, new[] { typeof(object) })
            .Add(c => c.Setter, value => changed = value));

        var header = component.Find("summary");
        Assert.AreEqual("ProductDataFilter", header.QuerySelector(".object-type-name")!.TextContent.Trim());
        Assert.IsNull(header.QuerySelector(".mud-select"));
        var menu = component.Find("summary .object-nested-actions .mud-menu button");
        await menu.ClickAsync(new());
        var item = popovers.FindAll(".mud-menu-item").Single(item => item.TextContent.Trim() == "ProductIdFilter");
        await item.ClickAsync(new());
        Assert.IsInstanceOfType<ProductIdFilter>(changed);
        Assert.IsTrue(component.Find("details").HasAttribute("open"));
        Assert.AreEqual("ProductIdFilter", component.Find("summary .object-type-name").TextContent.Trim());
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task SinglePopularityMultiplierTypeHasNoSwitchControls(bool nested)
    {
        await using var context = CreateContext();
        var value = Activator.CreateInstance(typeof(DataKeyPopularityMultiplierSelector))!;
        var component = context.Render<ObjectEditor<PopularityMultiplierSelector>>(p => p
            .Add(c => c.Value, value)
            .Add(c => c.OpenDetails, true)
            .Add(c => c.AncestorTypes, nested ? [typeof(object)] : [])
            .Add(c => c.Setter, _ => { }));

        Assert.IsFalse(Header(component, nested).TextContent.Contains("Switch type"));
        Assert.IsNull(Header(component, nested).QuerySelector("select"));
        Assert.AreSame(value, component.Instance.Value);
        Assert.HasCount(1, component.FindAll("input"));
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    [DataRow(true, true)]
    public async Task SwitchVisibilityUsesAlternativesRatherThanOptionCount(bool nested, bool readOnly)
    {
        await using var context = CreateContext();
        var types = typeof(Filter).Assembly.GetTypes()
            .Where(type => type.IsAssignableTo(typeof(Filter)) && !type.IsAbstract && !type.IsGenericType)
            .OrderBy(type => type.FullName).Take(2).ToArray();
        var value = Activator.CreateInstance(types[0])!;
        var component = context.Render<ObjectEditor<Filter>>(p => p
            .Add(c => c.Value, value)
            .Add(c => c.OpenDetails, true)
            .Add(c => c.ReadOnly, readOnly)
            .Add(c => c.AllowedDerivedTypes, [types[0]])
            .Add(c => c.AncestorTypes, nested ? [typeof(object)] : [])
            .Add(c => c.Setter, _ => { }));

        Assert.IsFalse(Header(component, nested).TextContent.Contains("Switch type"));

        component.Render(p => p.Add(c => c.AllowedDerivedTypes, [types[1]]));
        Assert.AreEqual(!readOnly, Header(component, nested).TextContent.Contains("Switch type"));

        component.Render(p => p.Add(c => c.AllowedDerivedTypes, Array.Empty<Type>()));
        Assert.IsFalse(Header(component, nested).TextContent.Contains("Switch type"));
        Assert.AreSame(value, component.Instance.Value);
    }

    private static AngleSharp.Dom.IElement Header<T>(IRenderedComponent<ObjectEditor<T>> component, bool nested)
        => component.Find(nested ? "summary" : ".object-top-level-header");

    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.Services.AddMudServices();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddSingleton(DocumentationCacheTests.CreateClient(request => Task.FromResult(DocumentationCacheTests.Response(request))));
        context.Services.AddSingleton<DocumentationCache>();
        return context;
    }
}
