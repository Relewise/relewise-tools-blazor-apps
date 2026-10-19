using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MudBlazor.Services;
using Relewise.BlazorApps.Shared;
using Relewise.BlazorApps.Shared.FilterColumnEditors;
using Relewise.Client.Requests.Filters;

namespace Relewise.BlazorApps.Tests;

[TestClass]
public class ColumnFilterControlTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task FilterButtonsOpenEditAndRemoveTheColumnFilter(bool displayName)
    {
        await using var context = new BunitContext();
        context.Services.AddMudServices();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        Type editor = displayName ? typeof(DisplayNameColumnEditor) : typeof(ProductIdColumnEditor);
        Column column = displayName
            ? new Column<ProductDisplayNameFilter>(editor, typeof(object), _ => null)
            : new Column<ProductIdFilter>(editor, typeof(object), _ => null);
        var component = context.Render<DynamicComponent>(p => p
            .Add(c => c.Type, editor)
            .Add(c => c.Parameters, new Dictionary<string, object> { ["Column"] = column }));
        string filterLabel = displayName ? "Filter by display name" : "Filter by product ID";
        string removeLabel = displayName ? "Remove display name filter" : "Remove product ID filter";

        await component.Find($"button[aria-label='{filterLabel}']").ClickAsync(new());
        Assert.IsNotNull(column.Filter);
        Assert.AreEqual("true", component.Find($"button[aria-label='{filterLabel}']").GetAttribute("aria-expanded"));
        var original = column.Filter;
        if (displayName) component.FindAll("input")[1].Input("en");
        component.Find("input").Input("fixture");
        Assert.AreNotSame(original, column.Filter);
        await component.Find($"button[aria-label='{removeLabel}']").ClickAsync(new());
        Assert.IsNull(column.Filter);
        Assert.HasCount(0, component.FindAll("input"));
        Assert.AreEqual("false", component.Find($"button[aria-label='{filterLabel}']").GetAttribute("aria-expanded"));
    }
}
