using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MudBlazor.Services;
using Newtonsoft.Json;
using Relewise.BlazorApps.TypeEditors;
using Relewise.Client.DataTypes;

namespace Relewise.BlazorApps.Tests;

[TestClass]
public class ArrayEditorTests
{
    public enum SearchKind { Product = 1, ProductCategory = 2 }

    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.Services.AddMudServices();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        return context;
    }

    [TestMethod]
    public async Task ProductVariantRowsEditIdsAndPreserveNullSemantics()
    {
        await using var context = CreateContext();
        ProductAndVariantId[]? result = null;
        var component = context.Render<ArrayEditor<ProductAndVariantId>>(p => p
            .Add(c => c.Value, new[] { new ProductAndVariantId("p1", "v1"), new ProductAndVariantId("p2", null) })
            .Add(c => c.OpenDetails, true)
            .Add(c => c.Setter, value => result = (ProductAndVariantId[]?)value));
        Assert.AreEqual(2, component.FindAll(".array-pair").Count);
        component.FindAll("input")[0].Input("changed");
        component.FindAll("input")[1].Input("");
        Assert.AreEqual("changed", result![0].ProductId);
        Assert.IsNull(result[0].VariantId);
        Assert.AreEqual("p2", result[1].ProductId);
        var restored = JsonConvert.DeserializeObject<ProductAndVariantId[]>(JsonConvert.SerializeObject(result));
        Assert.AreEqual("changed", restored![0].ProductId);
        Assert.IsNull(restored[0].VariantId);
        component.Find("button[aria-label='Remove item 1']").Click();
        Assert.AreEqual("p2", result![0].ProductId);
    }

    [TestMethod]
    public async Task EnumRowsKeepPriorityOrderAndCollapseAcrossParentUpdates()
    {
        await using var context = CreateContext();
        SearchKind[]? result = null;
        var component = context.Render<ArrayEditor<SearchKind>>(p => p
            .Add(c => c.Value, new[] { SearchKind.Product, SearchKind.ProductCategory })
            .Add(c => c.OpenDetails, true)
            .Add(c => c.Setter, value => result = (SearchKind[]?)value));
        CollectionAssert.AreEqual(new[] { "1", "2" }, component.FindAll(".array-index").Select(e => e.TextContent).ToArray());
        await component.InvokeAsync(() => component.Instance.Update(0, SearchKind.ProductCategory));
        CollectionAssert.AreEqual(new[] { SearchKind.ProductCategory, SearchKind.ProductCategory }, result);
        component.Find(".array-title").Click();
        component.Render(p => p.Add(c => c.Value, result));
        Assert.AreEqual("false", component.Find(".array-box").GetAttribute("data-expanded"));
        Assert.AreEqual(0, component.FindAll(".array-row").Count);
    }

    [TestMethod]
    public async Task CreateAddRemoveClearPreserveArrayStates()
    {
        await using var context = CreateContext();
        ProductAndVariantId[]? result = null;
        var component = context.Render<ArrayEditor<ProductAndVariantId>>(p => p
            .Add(c => c.Value, null)
            .Add(c => c.Setter, value => result = (ProductAndVariantId[]?)value));
        component.Find(".array-null").Click();
        Assert.AreEqual(0, result!.Length);
        Assert.AreEqual("true", component.Find(".array-box").GetAttribute("data-expanded"));
        component.Find(".array-entries button").Click();
        Assert.AreEqual(1, result.Length);
        Assert.IsNotNull(result[0]);
        component.Find("button[aria-label='Remove item 1']").Click();
        Assert.AreEqual(0, result.Length);
        component.Find("button[aria-label='Set array to null']").Click();
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task ReadOnlyGuardsAllMutations()
    {
        await using var context = CreateContext();
        var component = context.Render<ArrayEditor<ProductAndVariantId>>(p => p
            .Add(c => c.Value, new[] { new ProductAndVariantId("p1", "v1") })
            .Add(c => c.OpenDetails, true)
            .Add(c => c.ReadOnly, true)
            .Add(c => c.Setter, _ => Assert.Fail("Read-only array changed.")));
        Assert.AreEqual(0, component.FindAll("button[aria-label^='Remove item']").Count);
        await component.InvokeAsync(() =>
        {
            component.Instance.Create();
            component.Instance.Add();
            component.Instance.Remove(0);
            component.Instance.UpdateId(0, "changed", false);
            component.Instance.Update(0, null);
            component.Instance.Clear();
        });
        Assert.AreEqual("p1", component.Instance.Value![0].ProductId);
    }
}

