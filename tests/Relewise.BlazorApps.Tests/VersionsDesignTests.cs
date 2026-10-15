using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MudBlazor;
using MudBlazor.Services;
using Relewise.BlazorApps.Components;
using Relewise.BlazorApps.Pages;
using Relewise.BlazorApps.Shared;
using System.Net;

namespace Relewise.BlazorApps.Tests;

[TestClass]
public class VersionsDesignTests
{
    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.Services.AddMudServices();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        return context;
    }

    [TestMethod]
    public async Task DefaultsSelectTheTwoLatestStableReleasesWithoutStartingAComparison()
    {
        await using var context = CreateContext();
        int downloads = 0;
        using var client = DocumentationCacheTests.CreateClient(request =>
        {
            if (!request.RequestUri!.AbsolutePath.Contains("/versions/")) downloads++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""
                [{"version":"3.0.0-preview","published":"2026-04-01T00:00:00Z"},
                 {"version":"2.0.0","published":"2026-03-01T00:00:00Z"},
                 {"version":"1.0.0","published":"2026-02-01T00:00:00Z"}]
                """) });
        });
        context.Services.AddSingleton(new NugetClient(client));
        var component = context.Render<Versions>();
        component.WaitForAssertion(() => Assert.HasCount(2, component.FindComponents<MudAutocomplete<string>>()));
        Assert.AreEqual("1.0.0", component.Instance.LeftVersion);
        Assert.AreEqual("2.0.0", component.Instance.RightVersion);
        Assert.IsTrue(component.Instance.ReadyToCompare);
        Assert.AreEqual(0, downloads);
        Assert.HasCount(1, component.FindAll("button").Where(button => button.TextContent.Contains("Compare")));
    }

    [TestMethod]
    public async Task SharedComparisonStillLoadsResultsAndCanReturnToSelection()
    {
        await using var context = CreateContext();
        byte[] assembly = await File.ReadAllBytesAsync(typeof(Relewise.Client.ClientBase).Assembly.Location);
        using var client = DocumentationCacheTests.CreateClient(request => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = request.RequestUri!.AbsolutePath.Contains("/versions/")
                ? new StringContent("""[{"version":"2.0.0","published":"2026-03-01T00:00:00Z"},{"version":"1.0.0","published":"2026-02-01T00:00:00Z"}]""")
                : new ByteArrayContent(assembly)
        }));
        context.Services.AddSingleton(new NugetClient(client));
        context.Services.GetRequiredService<NavigationManager>().NavigateTo("/Versions?from=1.0.0&to=2.0.0");
        var component = context.Render<Versions>();
        component.WaitForAssertion(() => Assert.HasCount(5, component.FindComponents<VersionChangesPanel>()));
        Assert.AreEqual("1.0.0", component.Instance.LeftVersion);
        Assert.AreEqual("2.0.0", component.Instance.RightVersion);
        await component.FindAll("button").Single(button => button.TextContent.Trim() == "Select versions").ClickAsync(new());
        Assert.HasCount(2, component.FindComponents<MudAutocomplete<string>>());
    }

    [TestMethod]
    public async Task PropertyChangesShowBothTypesInSeparateColumns()
    {
        await using var context = CreateContext();
        var component = context.Render<MudExpansionPanels>(p => p.AddChildContent<VersionChangesPanel>(panel => panel
            .Add(c => c.Title, "Changed property types")
            .Add(c => c.Rows, new[] { new VersionChangeRow("FixtureRequest", "Take", "int", "long") })
            .Add(c => c.ShowMember, true).Add(c => c.ShowPrevious, true).Add(c => c.ShowCurrent, true)));
        Assert.HasCount(1, component.FindAll("tbody tr.mud-table-row"));
        Assert.AreEqual("int", component.Find(".previous-type").TextContent);
        Assert.AreEqual("long", component.Find(".current-type").TextContent);
    }

    [TestMethod]
    public async Task ComparisonSectionsShowAllChangesWithoutPagination()
    {
        await using var context = CreateContext();
        var changes = Enumerable.Range(1, 75).Select(index => new VersionChangeRow($"FixtureType{index}")).ToArray();
        var component = context.Render<MudExpansionPanels>(p => p.AddChildContent<VersionChangesPanel>(panel => panel
            .Add(c => c.Title, "Added types").Add(c => c.Rows, changes)));
        Assert.HasCount(75, component.FindAll("tbody tr.mud-table-row"));
        Assert.HasCount(0, component.FindComponents<MudDataGridPager<VersionChangeRow>>());
    }

    [TestMethod]
    public async Task EmptySectionsAreHiddenAndCountsArePartOfTheHeading()
    {
        await using var context = CreateContext();
        var component = context.Render<MudExpansionPanels>(p => p.AddChildContent<VersionChangesPanel>(panel => panel
            .Add(c => c.Title, "Added types").Add(c => c.Rows, Array.Empty<VersionChangeRow>())));
        Assert.HasCount(0, component.FindComponents<MudExpansionPanel>());
        component.Render(p => p.AddChildContent<VersionChangesPanel>(panel => panel
            .Add(c => c.Title, "Added types").Add(c => c.Rows, new[] { new VersionChangeRow("FixtureType") })));
        Assert.HasCount(1, component.FindComponents<MudExpansionPanel>());
        Assert.AreEqual("Added types (1)", component.Find(".changes-heading").TextContent);
        Assert.HasCount(0, component.FindComponents<MudChip<string>>());
        component.Render(p => p.AddChildContent<VersionChangesPanel>(panel => panel
            .Add(c => c.Title, "Added types").Add(c => c.Rows, Array.Empty<VersionChangeRow>())));
        Assert.HasCount(0, component.FindComponents<MudExpansionPanel>());
    }
}
