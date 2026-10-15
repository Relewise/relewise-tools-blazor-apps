using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MudBlazor;
using MudBlazor.Services;
using Relewise.BlazorApps.Components;

namespace Relewise.BlazorApps.Tests;

[TestClass]
public class ConfigurationDataGridTests
{
    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.Services.AddMudServices();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        return context;
    }

    [TestMethod]
    public async Task FilterDraftsApplyExplicitlyAndClearRestoresTheList()
    {
        await using var context = CreateContext();
        ConfigurationListItem<string>[] items =
        [
            new("first", "Seasonal promotion", "Filter", "Summer", "", true),
            new("second", "Archive", "Filter", "Winter", "", false)
        ];
        var component = context.Render<ConfigurationDataGrid<string>>(p => p.Add(c => c.Items, items));
        var grid = component.FindComponent<MudDataGrid<ConfigurationListItem<string>>>();
        Assert.AreEqual(50, grid.Instance.RowsPerPage);
        Assert.AreEqual(SortMode.None, grid.Instance.SortMode);
        Assert.IsFalse(grid.Instance.Filterable);
        await component.FindComponent<RListToolbar>().InvokeAsync(() =>
            component.FindComponent<RListToolbar>().Instance.ToggleFilters.InvokeAsync());
        var name = component.FindComponents<RTextField>().Single(field => field.Instance.Label == "Name contains");
        await name.InvokeAsync(() => name.Instance.ValueChanged.InvokeAsync("seasonal"));
        Assert.HasCount(2, grid.Instance.FilteredItems);
        await component.FindAll("button").Single(button => button.TextContent.Trim() == "Apply filters").ClickAsync(new());
        Assert.HasCount(1, grid.Instance.FilteredItems);
        Assert.AreEqual("first", grid.Instance.FilteredItems.Single().Value);
        await component.FindAll("button").Single(button => button.TextContent.Trim() == "Clear").ClickAsync(new());
        Assert.HasCount(2, grid.Instance.FilteredItems);
    }

    [TestMethod]
    public async Task SearchResetsPaginationAndToolbarKeepsRefreshAndCreateActions()
    {
        await using var context = CreateContext();
        var items = Enumerable.Range(1, 60).Select(index => new ConfigurationListItem<int>(index,
            $"Rule {index}", "Filter", "Group", index == 60 ? "Distinct description" : "", true)).ToArray();
        int refreshes = 0, creates = 0;
        var component = context.Render<ConfigurationDataGrid<int>>(p => p.Add(c => c.Items, items)
            .Add(c => c.OnRefresh, () => refreshes++).Add(c => c.OnCreate, () => creates++));
        var grid = component.FindComponent<MudDataGrid<ConfigurationListItem<int>>>();
        await grid.InvokeAsync(() => grid.Instance.NavigateTo(MudBlazor.Page.Next));
        Assert.AreEqual(1, grid.Instance.CurrentPage);
        var toolbar = component.FindComponent<RListToolbar>();
        await toolbar.InvokeAsync(() => toolbar.Instance.SearchChanged.InvokeAsync("distinct"));
        Assert.AreEqual(0, grid.Instance.CurrentPage);
        Assert.AreEqual(60, grid.Instance.FilteredItems.Single().Value);
        await component.FindAll("button").Single(button => button.TextContent.Trim() == "Refresh").ClickAsync(new());
        await component.FindAll("button").Single(button => button.TextContent.Trim() == "Create new").ClickAsync(new());
        Assert.AreEqual(1, refreshes);
        Assert.AreEqual(1, creates);
    }
}
